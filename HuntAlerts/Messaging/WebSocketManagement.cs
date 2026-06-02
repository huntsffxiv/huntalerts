using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.Logging;
using ECommons.Schedulers;
using FFXIVClientStructs.FFXIV.Client.UI;
using HuntAlerts.Helpers;
using HuntAlerts.Services;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Linq;
using SocketIOClient;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HuntAlerts
{
    public sealed partial class HuntAlerts
    {
        private SocketIOClient.SocketIO _socket;
        private CancellationTokenSource _cancellationTokenSource;
        private ConcurrentDictionary<(string Kind, string World), DateTime> recentMessagesCache = new();

        public enum ConnectionState { Unknown, Connecting, Connected, Disconnected, Reconnecting, Error }

        public ConnectionState SocketState { get; private set; } = ConnectionState.Unknown;
        public DateTime? LastStateChangeUtc { get; private set; }
        public int ReconnectAttemptCount { get; private set; }
        public string? LastConnectionError { get; private set; }

        public record ConnectionLogEntry(DateTime TimestampUtc, string Level, string Message);

        private readonly List<ConnectionLogEntry> _connectionLog = new();
        public IReadOnlyList<ConnectionLogEntry> ConnectionLog => _connectionLog;

        public string ServerUri => serverURI;

        private void SetSocketState(ConnectionState s)
        {
            SocketState = s;
            LastStateChangeUtc = DateTime.UtcNow;
        }

        private void LogConnection(string level, string message)
        {
            _connectionLog.Add(new ConnectionLogEntry(DateTime.UtcNow, level, message));
            while (_connectionLog.Count > 50) _connectionLog.RemoveAt(0);
        }

        private void InitializeSocketIO()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _socket = new SocketIOClient.SocketIO(serverURI, new SocketIOOptions
            {
                Reconnection = true,
                ReconnectionAttempts = int.MaxValue,
                ReconnectionDelay = 1000,
                ReconnectionDelayMax = 300000,
                RandomizationFactor = 0.5,
                Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
            });

            _socket.OnConnected         += (_, _) =>
            {
                SetSocketState(ConnectionState.Connected);
                ReconnectAttemptCount = 0;
                LogConnection("OK", "Connected.");
                PluginLog.Information("Connected to SocketIO.");
            };
            _socket.OnDisconnected      += (_, reason) =>
            {
                SetSocketState(ConnectionState.Disconnected);
                LogConnection("WARN", $"Disconnected: {reason}");
                PluginLog.Information($"Disconnected from SocketIO: {reason}");
            };
            _socket.OnReconnectAttempt  += (_, e) =>
            {
                SetSocketState(ConnectionState.Reconnecting);
                ReconnectAttemptCount = e;
                LogConnection("INFO", $"Reconnecting (attempt {e})...");
                PluginLog.Information($"Reconnecting... Attempt: {e}");
            };
            _socket.OnReconnectError    += (_, e) =>
            {
                LastConnectionError = e?.ToString();
                LogConnection("ERROR", $"Reconnect error: {e?.Message ?? e?.ToString()}");
                PluginLog.Warning($"Reconnection error: {e}");
            };

            _socket.On("event", async response =>
            {
                var messageString = response.GetValue<string>();
                await ProcessMessage(messageString);
            });

            SetSocketState(ConnectionState.Connecting);
            LogConnection("INFO", $"Connecting to {serverURI}...");
            _socket.ConnectAsync().ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    SetSocketState(ConnectionState.Error);
                    LastConnectionError = task.Exception?.GetBaseException().Message;
                    LogConnection("ERROR", $"Initial connect failed: {LastConnectionError}");
                    PluginLog.Warning("SocketIO connection error");
                    PluginLog.Verbose($"SocketIO connection error: {task.Exception}");
                }
            });
        }

        public async Task ReconnectAsync()
        {
            LogConnection("INFO", "Manual reconnect requested.");
            SetSocketState(ConnectionState.Connecting);
            try
            {
                await _socket.DisconnectAsync();
            }
            catch (Exception ex)
            {
                LogConnection("WARN", $"Disconnect during reconnect raised: {ex.Message}");
            }
            try
            {
                await _socket.ConnectAsync();
            }
            catch (Exception ex)
            {
                SetSocketState(ConnectionState.Error);
                LastConnectionError = ex.Message;
                LogConnection("ERROR", $"Reconnect failed: {ex.Message}");
                PluginLog.Warning($"Manual reconnect failed: {ex}");
            }
        }

        public Task ProcessMessage(string messageString)
        {
            try
            {
                if (messageString.StartsWith("Alert:"))
                {
                    var alertMessage = messageString.Substring("Alert:".Length).Trim();
                    var formatted    = "HuntAlerts Admin Broadcast\n" + alertMessage;
                    var message      = new SeStringBuilder().AddUiForeground((ushort)16).AddText(formatted).AddUiForegroundOff().Build();
                    PrintChat(message);
                    return Task.CompletedTask;
                }

                var huntMessage = JsonConvert.DeserializeObject<HuntMessage>(messageString);
                if (huntMessage == null) return Task.CompletedTask;

                if (!Svc.ClientState.IsLoggedIn) return Task.CompletedTask;

                new TickScheduler(() =>
                {
                    try
                    {
                        if (huntMessage.Type == "new_hunt")
                            HandleTrainEvent(huntMessage);
                        else if (huntMessage.Type == "srank")
                            HandleSRankEvent(huntMessage);
                    }
                    catch (Exception e)
                    {
                        e.Log();
                    }
                });
            }
            catch (JsonException ex)
            {
                PluginLog.Warning("HuntAlerts: invalid JSON payload from server.");
                PluginLog.Verbose($"json: {ex}");
            }
            return Task.CompletedTask;
        }

        public void SimulateHuntMessage(HuntMessage hm)
        {
            new TickScheduler(() =>
            {
                try
                {
                    if (hm.Type == "new_hunt") HandleTrainEvent(hm);
                    else if (hm.Type == "srank") HandleSRankEvent(hm);
                }
                catch (Exception e)
                {
                    e.Log();
                }
            });
        }

        private void HandleTrainEvent(HuntMessage hm)
        {
            PluginLog.Verbose($"New train: Kind={hm.Kind} World={hm.World}");

            if (IsSnoozed)
            {
                PluginLog.Verbose($"Train suppressed: snoozed for {Math.Ceiling(SnoozeRemaining.TotalMinutes)}m more.");
                return;
            }

            var dedupeKey = (hm.Kind, hm.World);
            if (Configuration.SuppressDuplicates &&
                recentMessagesCache.TryGetValue(dedupeKey, out var last) &&
                DateTime.Now - last < TimeSpan.FromMinutes(2))
            {
                PluginLog.Verbose("Train suppressed by dedup window.");
                return;
            }
            recentMessagesCache[dedupeKey] = DateTime.Now;

            if (!IsTrainGroupEnabled(hm.Kind))
            {
                PluginLog.Verbose($"Train kind '{hm.Kind}' not enabled.");
                return;
            }

            var playerCtx = SnapshotPlayer();
            var huntDc    = DatacenterOf(hm.World);

            switch (Configuration.Scope)
            {
                case ScopeMode.AllConfigured:
                    if (huntDc == null || !IsDataCenterEnabled(huntDc))
                    {
                        PluginLog.Verbose($"Train DC '{huntDc}' not enabled.");
                        return;
                    }
                    if (!IsWorldEnabled(hm.World))
                    {
                        PluginLog.Verbose($"Train world '{hm.World}' not enabled.");
                        return;
                    }
                    break;
                case ScopeMode.CurrentDatacenterOnly:
                    if (playerCtx.CurrentDatacenter == null || playerCtx.CurrentDatacenter != huntDc)
                    {
                        PluginLog.Verbose("Train not on player's current DC.");
                        return;
                    }
                    break;
                case ScopeMode.CurrentWorldOnly:
                    if (playerCtx.CurrentWorld != hm.World)
                    {
                        PluginLog.Verbose("Train not on player's current world.");
                        return;
                    }
                    break;
                case ScopeMode.HomeWorldOnly:
                    if (playerCtx.HomeWorld != hm.World)
                    {
                        PluginLog.Verbose("Train not on player's home world.");
                        return;
                    }
                    break;
            }

            var content = ReplaceTimestampsWithLocalTime(hm.Content ?? "");
            content     = RemoveDiscordEmojis(content);

            var aetheryteName = hm.AetheriteName;
            uint aetheryteId  = 0;
            string coordsStr  = "";
            var startZone     = hm.LocationName ?? "";

            try
            {
                var (cx, cy) = ExtractCoordinates(content);
                if (cx is not null && cy is not null) coordsStr = $"{(float)cx}, {(float)cy}";

                if (TryGetOpenWorldTerritory(hm.LocationName, out var tt))
                {
                    if (aetheryteName == "invalid" || string.IsNullOrEmpty(aetheryteName))
                    {
                        var (id, name) = (cx, cy) is (float ccx, float ccy)
                            ? MapManager.GetNearestAetheryte(tt, ccx, ccy)
                            : MapManager.GetZonePrimaryAetheryte(tt);
                        aetheryteId   = id;
                        aetheryteName = name;
                    }
                    else
                    {
                        var match = MapManager.LookupAetheryteByName(tt, aetheryteName);
                        if (match is not null)
                        {
                            aetheryteId = match.Value.RowId;
                        }
                        else
                        {
                            var (id, name) = (cx, cy) is (float ccx, float ccy)
                                ? MapManager.GetNearestAetheryte(tt, ccx, ccy)
                                : MapManager.GetZonePrimaryAetheryte(tt);
                            PluginLog.Verbose($"Aetheryte '{aetheryteName}' not in zone '{hm.LocationName}'; using zone fallback '{name}' (id {id}).");
                            aetheryteId   = id;
                            aetheryteName = name;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(aetheryteName) && aetheryteName != "invalid")
                {
                    var anywhere = MapManager.LookupAetheryteByNameAnywhere(aetheryteName);
                    if (anywhere is not null)
                    {
                        PluginLog.Verbose($"Zone '{hm.LocationName}' unrecognized; resolved aetheryte '{aetheryteName}' via global lookup (id {anywhere.Value.RowId}); deduced zone '{anywhere.Value.ZoneName}'.");
                        aetheryteId   = anywhere.Value.RowId;
                        aetheryteName = anywhere.Value.Name;
                        if (!string.IsNullOrEmpty(anywhere.Value.ZoneName))
                            startZone = anywhere.Value.ZoneName;
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"Coord / aetheryte resolution failed: {ex.Message}");
            }

            if (string.IsNullOrEmpty(aetheryteName) || aetheryteName == "invalid") aetheryteName = "Unknown";

            var lifestreamHooked = Configuration.LifestreamIntegration && IsLifestreamLoaded();
            var openMapOnArrival = Configuration.OpenMapOnArrival;
            var postedLocal      = ConvertTime(hm.Posted_Epoch);
            var huntRegion       = huntDc != null && WorldData.TryGetWorld(hm.World, out var hwInfo) ? WorldData.RegionLabel(hwInfo.Region) : "";
            var currentRegion    = playerCtx.CurrentRegion ?? "";

            var formattedDetail =
                $"Kind: Hunt Train{Environment.NewLine}Hunt: {hm.Kind}{Environment.NewLine}" +
                $"Start Zone: {startZone}{Environment.NewLine}Aetherite: {aetheryteName}{Environment.NewLine}" +
                $"World: {hm.World}{Environment.NewLine}Posted: {postedLocal}{Environment.NewLine}{Environment.NewLine}" +
                content;

            var htMessage = new HuntTrainMessage(
                formattedDetail, hm.Type, hm.Kind, hm.World,
                playerCtx.CurrentWorld ?? "", currentRegion, huntRegion,
                postedLocal, hm.Posted_Epoch, aetheryteName, aetheryteId, startZone, instance: 1,
                coordsStr, openMapOnArrival, lifestreamHooked);

            var link = P.MessageCacheManager.AddMessage(htMessage);
            Service.IPCManager.OnHuntTrainMessageReceived(htMessage);

            PrintChat(BuildLinkedLine(link, $"{hm.Kind} train starting on {hm.World}! (Click for info)", Configuration.TextColor));

            if (Configuration.SoundEffect != 0)
                UIGlobals.PlayChatSoundEffect((uint)Configuration.SoundEffect);
        }

        private void HandleSRankEvent(HuntMessage hm)
        {
            if (IsSnoozed)
            {
                PluginLog.Verbose($"S Rank suppressed: snoozed for {Math.Ceiling(SnoozeRemaining.TotalMinutes)}m more.");
                return;
            }

            if (!Configuration.SRankEnabled)
            {
                PluginLog.Verbose("S Rank disabled globally.");
                return;
            }

            if (!IsSRankGroupEnabled(hm.Kind))
            {
                PluginLog.Verbose($"S Rank kind '{hm.Kind}' not enabled.");
                return;
            }

            var playerCtx = SnapshotPlayer();
            var huntDc    = DatacenterOf(hm.World);

            switch (Configuration.SRankScope)
            {
                case ScopeMode.AllConfigured:
                    if (huntDc == null || !IsSRankDataCenterEnabled(huntDc))
                    {
                        PluginLog.Verbose($"S Rank DC '{huntDc}' not enabled.");
                        return;
                    }
                    if (!IsSRankWorldEnabled(hm.World))
                    {
                        PluginLog.Verbose($"S Rank world '{hm.World}' not enabled.");
                        return;
                    }
                    break;
                case ScopeMode.CurrentDatacenterOnly:
                    if (playerCtx.CurrentDatacenter == null || playerCtx.CurrentDatacenter != huntDc)
                    {
                        PluginLog.Verbose("S Rank not on player's current DC.");
                        return;
                    }
                    break;
                case ScopeMode.CurrentWorldOnly:
                    if (playerCtx.CurrentWorld != hm.World)
                    {
                        PluginLog.Verbose("S Rank not on player's current world.");
                        return;
                    }
                    break;
                case ScopeMode.HomeWorldOnly:
                    if (playerCtx.HomeWorld != hm.World)
                    {
                        PluginLog.Verbose("S Rank not on player's home world.");
                        return;
                    }
                    break;
            }

            var creatureName = hm.CreatureName ?? "";
            var locationName = hm.LocationName ?? "";
            var coordsStr    = hm.LocationCoords ?? "";
            var aetheryteName= hm.AetheriteName ?? "";
            var instance     = hm.Instance < 1 ? 1 : hm.Instance;
            var deathTime    = hm.DeathTime;
            var postedLocal  = ConvertTime(hm.Posted_Epoch);

            var huntRegion = WorldData.TryGetWorld(hm.World, out var hwInfo) ? WorldData.RegionLabel(hwInfo.Region) : "";
            var currentRegion = playerCtx.CurrentRegion ?? "";
            var lifestreamHooked = Configuration.LifestreamIntegration && IsLifestreamLoaded();
            var openMapOnArrival = Configuration.OpenMapOnArrival;

            uint aetheryteId = 0;
            string startLocation = aetheryteName;
            if (deathTime == 0)
            {
                if (TryGetOpenWorldTerritory(locationName, out var tt))
                {
                    if (string.IsNullOrEmpty(startLocation) || startLocation == "invalid")
                    {
                        var (id, name) = MapManager.GetZonePrimaryAetheryte(tt);
                        aetheryteId = id;
                        startLocation = name;
                    }
                    else
                    {
                        var match = MapManager.LookupAetheryteByName(tt, startLocation);
                        if (match is not null)
                        {
                            aetheryteId = match.Value.RowId;
                        }
                        else
                        {
                            var (id, name) = MapManager.GetZonePrimaryAetheryte(tt);
                            aetheryteId   = id;
                            startLocation = name;
                        }
                    }
                }
                else if (!string.IsNullOrEmpty(startLocation) && startLocation != "invalid")
                {
                    var anywhere = MapManager.LookupAetheryteByNameAnywhere(startLocation);
                    if (anywhere is not null)
                    {
                        aetheryteId   = anywhere.Value.RowId;
                        startLocation = anywhere.Value.Name;
                        if (!string.IsNullOrEmpty(anywhere.Value.ZoneName))
                            locationName = anywhere.Value.ZoneName;
                    }
                }
            }

            if (string.IsNullOrEmpty(startLocation) || startLocation == "invalid") startLocation = "Unknown";
            if (string.IsNullOrEmpty(aetheryteName) || aetheryteName == "invalid") aetheryteName = "Unknown";

            if (deathTime == 0)
            {
                var detail =
                    $"Type: S Rank{Environment.NewLine}Hunt: {hm.Kind}{Environment.NewLine}" +
                    $"World: {hm.World}{Environment.NewLine}Start Zone: {locationName}{Environment.NewLine}" +
                    $"Instance: {instance}{Environment.NewLine}Aetherite: {startLocation}{Environment.NewLine}" +
                    $"Posted: {postedLocal}{Environment.NewLine}Creature: {creatureName}{Environment.NewLine}{Environment.NewLine}" +
                    $"Location: {locationName} ({coordsStr}){Environment.NewLine}Aetherite: {aetheryteName}";

                var htMessage = new HuntTrainMessage(
                    detail, hm.Type, hm.Kind, hm.World,
                    playerCtx.CurrentWorld ?? "", currentRegion, huntRegion,
                    postedLocal, hm.Posted_Epoch, startLocation, aetheryteId, locationName, instance,
                    coordsStr, openMapOnArrival, lifestreamHooked, creatureName);

                var link = P.MessageCacheManager.AddMessage(htMessage);
                Service.IPCManager.OnHuntTrainMessageReceived(htMessage);

                var label = instance > 1
                    ? $"{hm.Kind} S Rank {creatureName} (i{instance}) spawned on {hm.World}! (Click for info)"
                    : $"{hm.Kind} S Rank {creatureName} spawned on {hm.World}! (Click for info)";

                PrintChat(BuildLinkedLine(link, label, Configuration.SRankTextColor));

                if (Configuration.SoundEffect != 0)
                    UIGlobals.PlayChatSoundEffect((uint)Configuration.SoundEffect);
            }
            else
            {
                var label = $"{hm.Kind} S Rank {creatureName} on {hm.World} was killed at {ConvertTime(deathTime)}.";
                var b = new SeStringBuilder();
                if (Configuration.SRankKillTextColor != 0) b.AddUiForeground((ushort)Configuration.SRankKillTextColor);
                b.AddText(label);
                if (Configuration.SRankKillTextColor != 0) b.AddUiForegroundOff();
                PrintChat(b.Build());
            }
        }

        private readonly record struct PlayerContextSnapshot(
            string? CurrentWorld,
            string? HomeWorld,
            string? CurrentDatacenter,
            string? CurrentRegion);

        private PlayerContextSnapshot SnapshotPlayer()
        {
            if (!Svc.ClientState.IsLoggedIn || Svc.Objects.LocalPlayer == null)
                return new PlayerContextSnapshot(null, null, null, null);

            var cw = Svc.Objects.LocalPlayer.CurrentWorld.ValueNullable?.Name.ToString();
            var hw = Svc.Objects.LocalPlayer.HomeWorld.ValueNullable?.Name.ToString();
            string? cdc = null;
            string? cr  = null;
            if (cw != null && WorldData.TryGetWorld(cw, out var info))
            {
                cdc = info.Datacenter;
                cr  = WorldData.RegionLabel(info.Region);
            }
            PluginLog.Verbose($"Player ctx: world={cw} home={hw} dc={cdc} region={cr}");
            return new PlayerContextSnapshot(cw, hw, cdc, cr);
        }

        private static bool IsLifestreamLoaded() =>
            Svc.PluginInterface.InstalledPlugins.FirstOrDefault(p => p.InternalName == "Lifestream")?.IsLoaded == true;

        private static bool TryGetOpenWorldTerritory(string zoneName, out uint territoryType)
        {
            territoryType = 0;
            if (string.IsNullOrEmpty(zoneName)) return false;
            if (!Svc.Data.GetExcelSheet<TerritoryType>().TryGetFirst(
                    t => t.TerritoryIntendedUse.RowId == (uint)TerritoryIntendedUseEnum.Open_World
                      && (t.PlaceName.ValueNullable?.Name.ExtractText() ?? "").EqualsIgnoreCase(zoneName),
                    out var match))
                return false;
            territoryType = match.RowId;
            return true;
        }

        private static SeString BuildLinkedLine(DalamudLinkPayload link, string text, int color)
        {
            var b = new SeStringBuilder();
            if (color != 0) b.AddUiForeground((ushort)color);
            b.Add(link).AddText(text).Add(RawPayload.LinkTerminator);
            if (color != 0) b.AddUiForegroundOff();
            return b.Build();
        }

        private void PrintChat(SeString message)
        {
            if (Configuration.UseDalamudChat)
                Svc.Chat.Print(new() { Message = message });
            else
                Svc.Chat.Print(new() { Message = message, Type = Configuration.OutputChat });
        }
    }
}
