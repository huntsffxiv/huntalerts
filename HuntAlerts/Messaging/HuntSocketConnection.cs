using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
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
using SocketIOClient;

namespace HuntAlerts.Messaging;

public sealed class HuntSocketConnection : IDisposable
{
    public const string DefaultServerUri = "wss://huntalerts.pro:24842";

    public enum ConnectionState { Unknown, Connecting, Connected, Disconnected, Reconnecting, Error }

    private SocketIOClient.SocketIO? socket;
    private CancellationTokenSource? cancellationTokenSource;
    private readonly ConcurrentDictionary<(string Kind, string World), DateTime> recentMessagesCache = [];

    public ConnectionState SocketState { get; private set; } = ConnectionState.Unknown;
    public DateTime? LastStateChangeUtc { get; private set; }
    public int ReconnectAttemptCount { get; private set; }
    public string? LastConnectionError { get; private set; }

    public record ConnectionLogEntry(DateTime TimestampUtc, string Level, string Message);

    private readonly List<ConnectionLogEntry> connectionLog = [];
    public IReadOnlyList<ConnectionLogEntry> ConnectionLog => connectionLog;

    public string ServerUri { get; }

    public HuntSocketConnection(string? serverUri = null)
    {
        ServerUri = serverUri ?? DefaultServerUri;
        Connect();
    }

    private void SetSocketState(ConnectionState s)
    {
        SocketState = s;
        LastStateChangeUtc = DateTime.UtcNow;
    }

    private void LogConnection(string level, string message)
    {
        connectionLog.Add(new ConnectionLogEntry(DateTime.UtcNow, level, message));
        while (connectionLog.Count > 50) connectionLog.RemoveAt(0);
    }

    private void Connect()
    {
        cancellationTokenSource = new CancellationTokenSource();
        socket = new SocketIOClient.SocketIO(ServerUri, new SocketIOOptions
        {
            Reconnection = true,
            ReconnectionAttempts = int.MaxValue,
            ReconnectionDelay = 1000,
            ReconnectionDelayMax = 300000,
            RandomizationFactor = 0.5,
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
        });

        socket.OnConnected += (_, _) =>
        {
            SetSocketState(ConnectionState.Connected);
            ReconnectAttemptCount = 0;
            LogConnection("OK", "Connected.");
            PluginLog.Information("Connected to SocketIO.");
        };
        socket.OnDisconnected += (_, reason) =>
        {
            SetSocketState(ConnectionState.Disconnected);
            LogConnection("WARN", $"Disconnected: {reason}");
            PluginLog.Information($"Disconnected from SocketIO: {reason}");
        };
        socket.OnReconnectAttempt += (_, e) =>
        {
            SetSocketState(ConnectionState.Reconnecting);
            ReconnectAttemptCount = e;
            LogConnection("INFO", $"Reconnecting (attempt {e})...");
            PluginLog.Information($"Reconnecting... Attempt: {e}");
        };
        socket.OnReconnectError += (_, e) =>
        {
            LastConnectionError = e?.ToString();
            LogConnection("ERROR", $"Reconnect error: {e?.Message ?? e?.ToString()}");
            PluginLog.Warning($"Reconnection error: {e}");
        };

        socket.On("event", async response =>
        {
            var messageString = response.GetValue<string>();
            await ProcessMessage(messageString);
        });

        SetSocketState(ConnectionState.Connecting);
        LogConnection("INFO", $"Connecting to {ServerUri}...");
        socket.ConnectAsync().ContinueWith(task =>
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
            await socket!.DisconnectAsync();
        }
        catch (Exception ex)
        {
            LogConnection("WARN", $"Disconnect during reconnect raised: {ex.Message}");
        }
        try
        {
            await socket!.ConnectAsync();
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
                var alertMessage = messageString["Alert:".Length..].Trim();
                var formatted = "HuntAlerts Admin Broadcast\n" + alertMessage;
                var message = new SeStringBuilder().AddUiForeground(16).AddText(formatted).AddUiForegroundOff().Build();
                PrintChat(message);
                return Task.CompletedTask;
            }

            var huntMessage = JsonConvert.DeserializeObject<HuntMessage>(messageString);
            if (huntMessage == null) return Task.CompletedTask;

            if (!Svc.ClientState.IsLoggedIn) return Task.CompletedTask;

            _ = new TickScheduler(() =>
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
        _ = new TickScheduler(() =>
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
        var config = HuntAlerts.C;

        PluginLog.Verbose($"New train: Kind={hm.Kind} World={hm.World}");

        if (Service.Snooze.IsSnoozed)
        {
            PluginLog.Verbose($"Train suppressed: snoozed for {Math.Ceiling(Service.Snooze.SnoozeRemaining.TotalMinutes)}m more.");
            return;
        }

        var dedupeKey = (hm.Kind, hm.World);
        if (config.SuppressDuplicates &&
            recentMessagesCache.TryGetValue(dedupeKey, out var last) &&
            DateTime.Now - last < TimeSpan.FromMinutes(2))
        {
            PluginLog.Verbose("Train suppressed by dedup window.");
            return;
        }
        recentMessagesCache[dedupeKey] = DateTime.Now;

        if (!HuntAlertFilter.IsTrainGroupEnabled(config, hm.Kind))
        {
            PluginLog.Verbose($"Train kind '{hm.Kind}' not enabled.");
            return;
        }

        var playerCtx = SnapshotPlayer();
        var huntDc = HuntAlertFilter.DatacenterOf(hm.World);

        switch (config.Scope)
        {
            case ScopeMode.AllConfigured:
                if (huntDc == null || !HuntAlertFilter.IsDataCenterEnabled(config, huntDc))
                {
                    PluginLog.Verbose($"Train DC '{huntDc}' not enabled.");
                    return;
                }
                if (!HuntAlertFilter.IsWorldEnabled(config, hm.World))
                {
                    PluginLog.Verbose($"Train world '{hm.World}' not enabled.");
                    return;
                }
                break;
            case ScopeMode.CurrentDatacenterOnly:
                if (playerCtx.CurrentDatacenter == null || playerCtx.CurrentDatacenterName != huntDc)
                {
                    PluginLog.Verbose("Train not on player's current DC.");
                    return;
                }
                break;
            case ScopeMode.CurrentWorldOnly:
                if (playerCtx.CurrentWorldName != hm.World)
                {
                    PluginLog.Verbose("Train not on player's current world.");
                    return;
                }
                break;
            case ScopeMode.HomeWorldOnly:
                if (playerCtx.HomeWorldName != hm.World)
                {
                    PluginLog.Verbose("Train not on player's home world.");
                    return;
                }
                break;
        }

        var content = HuntMessageFormatting.ReplaceTimestampsWithLocalTime(hm.Content ?? "");
        content = HuntMessageFormatting.RemoveDiscordEmojis(content);

        var aetheryteName = hm.AetheryteName;
        uint aetheryteId = 0;
        uint startTerritoryTypeId = 0;
        string coordsStr = "";
        Vector2? mapLocationCoords = null;
        var startZone = hm.LocationName ?? "";

        try
        {
            var (cx, cy) = HuntMessageParsing.ExtractCoordinates(content);
            if (cx is not null && cy is not null)
            {
                coordsStr = $"{(float)cx}, {(float)cy}";
                mapLocationCoords = new Vector2((float)cx, (float)cy);
            }

            if (TryGetOpenWorldTerritory(hm.LocationName, out var tt))
            {
                startTerritoryTypeId = tt;
                if (aetheryteName == "invalid" || string.IsNullOrEmpty(aetheryteName))
                {
                    var (id, name) = (cx, cy) is (float ccx, float ccy)
                        ? MapManager.GetNearestAetheryte(tt, ccx, ccy)
                        : MapManager.GetZonePrimaryAetheryte(tt);
                    aetheryteId = id;
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
                        aetheryteId = id;
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
                    aetheryteId = anywhere.Value.RowId;
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

        var lifestreamHooked = config.LifestreamIntegration && IsLifestreamLoaded();
        var openMapOnArrival = config.OpenMapOnArrival;
        var postedLocal = HuntMessageFormatting.ConvertTime(hm.Posted_Epoch);
        var huntRegion = huntDc != null && WorldData.TryGetWorld(hm.World, out var hwInfo) ? WorldData.RegionLabel(hwInfo.Region) : "";
        var currentRegion = playerCtx.CurrentRegionName;

        var formattedDetail =
            $"Kind: Hunt Train{Environment.NewLine}Hunt: {hm.Kind}{Environment.NewLine}" +
            $"Start Zone: {startZone}{Environment.NewLine}Aetherite: {aetheryteName}{Environment.NewLine}" +
            $"World: {hm.World}{Environment.NewLine}Posted: {postedLocal}{Environment.NewLine}{Environment.NewLine}" +
            content;

        var huntWorldId = TryGetWorld(hm.World, out var huntWorld) ? huntWorld.RowId : 0u;
        var huntRegionRowId = huntWorld.DataCenter.ValueNullable?.Region.RowId ?? 0u;
        var currentRegionRowId = playerCtx.CurrentRegion?.RowId ?? 0u;

        var htMessage = new HuntTrainMessage(
            formattedDetail, hm.Type, hm.Kind, hm.World,
            playerCtx.CurrentWorldName, currentRegion, huntRegion,
            postedLocal, hm.Posted_Epoch, aetheryteName, aetheryteId, startZone, instance: 1,
            coordsStr, openMapOnArrival, lifestreamHooked,
            currentRegionRowId: currentRegionRowId, huntRegionRowId: huntRegionRowId);
        var typedAlert = new HuntAlertMessage(
            formattedDetail,
            hm.Type ?? "",
            hm.Kind ?? "",
            huntWorldId,
            playerCtx.CurrentWorld?.RowId ?? 0,
            currentRegionRowId,
            huntRegionRowId,
            DateTimeOffset.FromUnixTimeSeconds(hm.Posted_Epoch),
            hm.Posted_Epoch,
            aetheryteId,
            startTerritoryTypeId,
            1,
            mapLocationCoords,
            0);

        var link = Service.MessageCacheManager.AddMessage(htMessage);
        Service.IPCManager.OnHuntTrainMessageReceived(htMessage);
        Service.IPCManager.OnHuntAlertMessageReceived(typedAlert);

        PrintChat(BuildLinkedLine(link, $"{hm.Kind} train starting on {hm.World}! (Click for info)", config.TextColor));

        if (config.SoundEffect != 0)
            UIGlobals.PlayChatSoundEffect((uint)config.SoundEffect);
    }

    private void HandleSRankEvent(HuntMessage hm)
    {
        var config = HuntAlerts.C;

        if (Service.Snooze.IsSnoozed)
        {
            PluginLog.Verbose($"S Rank suppressed: snoozed for {Math.Ceiling(Service.Snooze.SnoozeRemaining.TotalMinutes)}m more.");
            return;
        }

        if (!config.SRankEnabled)
        {
            PluginLog.Verbose("S Rank disabled globally.");
            return;
        }

        if (!HuntAlertFilter.IsSRankGroupEnabled(config, hm.Kind))
        {
            PluginLog.Verbose($"S Rank kind '{hm.Kind}' not enabled.");
            return;
        }

        var playerCtx = SnapshotPlayer();
        var huntDc = HuntAlertFilter.DatacenterOf(hm.World);

        switch (config.SRankScope)
        {
            case ScopeMode.AllConfigured:
                if (huntDc == null || !HuntAlertFilter.IsSRankDataCenterEnabled(config, huntDc))
                {
                    PluginLog.Verbose($"S Rank DC '{huntDc}' not enabled.");
                    return;
                }
                if (!HuntAlertFilter.IsSRankWorldEnabled(config, hm.World))
                {
                    PluginLog.Verbose($"S Rank world '{hm.World}' not enabled.");
                    return;
                }
                break;
            case ScopeMode.CurrentDatacenterOnly:
                if (playerCtx.CurrentDatacenter == null || playerCtx.CurrentDatacenterName != huntDc)
                {
                    PluginLog.Verbose("S Rank not on player's current DC.");
                    return;
                }
                break;
            case ScopeMode.CurrentWorldOnly:
                if (playerCtx.CurrentWorldName != hm.World)
                {
                    PluginLog.Verbose("S Rank not on player's current world.");
                    return;
                }
                break;
            case ScopeMode.HomeWorldOnly:
                if (playerCtx.HomeWorldName != hm.World)
                {
                    PluginLog.Verbose("S Rank not on player's home world.");
                    return;
                }
                break;
        }

        var creatureName = hm.CreatureName ?? "";
        var locationName = hm.LocationName ?? "";
        var coordsStr = hm.LocationCoords ?? "";
        var aetheryteName = hm.AetheryteName ?? "";
        var instance = hm.Instance < 1 ? 1 : hm.Instance;
        var deathTime = hm.DeathTime;
        var postedLocal = HuntMessageFormatting.ConvertTime(hm.Posted_Epoch);

        var huntRegion = WorldData.TryGetWorld(hm.World, out var hwInfo) ? WorldData.RegionLabel(hwInfo.Region) : "";
        var currentRegion = playerCtx.CurrentRegionName;
        var lifestreamHooked = config.LifestreamIntegration && IsLifestreamLoaded();
        var openMapOnArrival = config.OpenMapOnArrival;

        uint aetheryteId = 0;
        uint startTerritoryTypeId = 0;
        Vector2? mapLocationCoords = null;
        string startLocation = aetheryteName;
        var (mx, my) = HuntMessageParsing.ExtractCoordinates(coordsStr);
        if (mx is not null && my is not null)
            mapLocationCoords = new Vector2((float)mx, (float)my);
        if (deathTime == 0)
        {
            if (TryGetOpenWorldTerritory(locationName, out var tt))
            {
                startTerritoryTypeId = tt;
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
                        aetheryteId = id;
                        startLocation = name;
                    }
                }
            }
            else if (!string.IsNullOrEmpty(startLocation) && startLocation != "invalid")
            {
                var anywhere = MapManager.LookupAetheryteByNameAnywhere(startLocation);
                if (anywhere is not null)
                {
                    aetheryteId = anywhere.Value.RowId;
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

            var huntWorldId = TryGetWorld(hm.World, out var huntWorld) ? huntWorld.RowId : 0u;
            var huntRegionRowId = huntWorld.DataCenter.ValueNullable?.Region.RowId ?? 0u;
            var currentRegionRowId = playerCtx.CurrentRegion?.RowId ?? 0u;

            var htMessage = new HuntTrainMessage(
                detail, hm.Type, hm.Kind, hm.World,
                playerCtx.CurrentWorldName ?? "", currentRegion, huntRegion,
                postedLocal, hm.Posted_Epoch, startLocation, aetheryteId, locationName, instance,
                coordsStr, openMapOnArrival, lifestreamHooked, creatureName,
                currentRegionRowId: currentRegionRowId, huntRegionRowId: huntRegionRowId);

            var typedAlert = new HuntAlertMessage(
                detail,
                hm.Type ?? "",
                hm.Kind ?? "",
                huntWorldId,
                playerCtx.CurrentWorld?.RowId ?? 0,
                currentRegionRowId,
                huntRegionRowId,
                DateTimeOffset.FromUnixTimeSeconds(hm.Posted_Epoch),
                hm.Posted_Epoch,
                aetheryteId,
                startTerritoryTypeId,
                instance,
                mapLocationCoords,
                Svc.Data.GetExcelSheet<BNpcName>().FirstOrNull(r => r.Singular.ExtractText().Equals(creatureName.Trim(), StringComparison.OrdinalIgnoreCase))?.RowId);

            var link = Service.MessageCacheManager.AddMessage(htMessage);
            Service.IPCManager.OnHuntTrainMessageReceived(htMessage);
            Service.IPCManager.OnHuntAlertMessageReceived(typedAlert);

            var label = instance > 1
                ? $"{hm.Kind} S Rank {creatureName} (i{instance}) spawned on {hm.World}! (Click for info)"
                : $"{hm.Kind} S Rank {creatureName} spawned on {hm.World}! (Click for info)";

            PrintChat(BuildLinkedLine(link, label, config.SRankTextColor));

            if (config.SoundEffect != 0)
                UIGlobals.PlayChatSoundEffect((uint)config.SoundEffect);
        }
        else
        {
            var label = $"{hm.Kind} S Rank {creatureName} on {hm.World} was killed at {HuntMessageFormatting.ConvertTime(deathTime)}.";
            var b = new SeStringBuilder();
            if (config.SRankKillTextColor != 0) b.AddUiForeground((ushort)config.SRankKillTextColor);
            b.AddText(label);
            if (config.SRankKillTextColor != 0) b.AddUiForegroundOff();
            PrintChat(b.Build());
        }
    }

    private readonly record struct PlayerContextSnapshot(World? CurrentWorld, World? HomeWorld)
    {
        public WorldDCGroupType? CurrentDatacenter => CurrentWorld?.DataCenter.ValueNullable;
        public WorldRegionGroup? CurrentRegion => CurrentDatacenter?.Region.ValueNullable;
        public string CurrentWorldName => CurrentWorld?.Name.ExtractText() ?? "";
        public string HomeWorldName => HomeWorld?.Name.ExtractText() ?? "";
        public string CurrentDatacenterName => CurrentDatacenter?.Name.ExtractText() ?? "";
        public string CurrentRegionName => CurrentRegion?.Name.ExtractText() ?? "";
    }

    private static PlayerContextSnapshot SnapshotPlayer()
    {
        if (!Svc.ClientState.IsLoggedIn || Svc.Objects.LocalPlayer == null)
            return new PlayerContextSnapshot(null, null);

        var cw = Svc.Objects.LocalPlayer.CurrentWorld.ValueNullable;
        var hw = Svc.Objects.LocalPlayer.HomeWorld.ValueNullable;
        var cdc = cw?.DataCenter.ValueNullable;
        var cr = cdc?.Region.ValueNullable;
        if (cw == null || hw == null || cdc == null || cr == null)
            return new PlayerContextSnapshot(null, null);

        PluginLog.Verbose($"Player ctx: world={cw.Value.Name} home={hw.Value.Name} dc={cdc.Value.Name} region={cr.Value.Name}");
        return new PlayerContextSnapshot(cw, hw);
    }

    private static bool IsLifestreamLoaded() =>
        Svc.PluginInterface.InstalledPlugins.FirstOrDefault(p => p.InternalName == "Lifestream")?.IsLoaded == true;

    private static bool TryGetOpenWorldTerritory(string? zoneName, out uint territoryType)
    {
        territoryType = 0;
        if (string.IsNullOrEmpty(zoneName)) return false;
        if (!Svc.Data.GetExcelSheet<TerritoryType>(Dalamud.Game.ClientLanguage.English).TryGetFirst(
                t => t.TerritoryIntendedUse.RowId == (uint)TerritoryIntendedUseEnum.Open_World
                  && (t.PlaceName.ValueNullable?.Name.ExtractText() ?? "").EqualsIgnoreCase(zoneName),
                out var match))
            return false;
        territoryType = match.RowId;
        return true;
    }

    private static bool TryGetWorld(string worldName, out World world)
    {
        world = default;
        if (string.IsNullOrWhiteSpace(worldName)) return false;
        return Svc.Data.GetExcelSheet<World>().TryGetFirst(w => w.IsPublic && w.Name.ExtractText().Equals(worldName, StringComparison.OrdinalIgnoreCase), out world);
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
        var config = HuntAlerts.C;
        if (config.UseDalamudChat)
            Svc.Chat.Print(new() { Message = message });
        else
            Svc.Chat.Print(new() { Message = message, Type = config.OutputChat });
    }

    public void Dispose()
    {
        try
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            socket?.Dispose();
            socket?.DisconnectAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            PluginLog.Error($"{ex}");
        }
    }
}
