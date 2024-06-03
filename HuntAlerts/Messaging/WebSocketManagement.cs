using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.Logging;
using ECommons.Schedulers;
using FFXIVClientStructs.FFXIV.Client.UI;
using HuntAlerts.Helpers;
using HuntAlerts.Services;
using HuntAlerts.Windows;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HuntAlerts
{
    public sealed partial class HuntAlerts
    {
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cancellationTokenSource;
        private async void InitializeWebSocket()
        {
            _webSocket = new ClientWebSocket();
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();

                // Connect to WebSocket
                await _webSocket.ConnectAsync(new Uri(serverURI), _cancellationTokenSource.Token);
                PluginLog.Information("Connected to WebSocket.");
                // Start listening for messages
                StartReceiving(_cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                PluginLog.Warning("Websocket connection error");
                PluginLog.Verbose($"WebSocket connection error: {ex}");
                // Start reconnection logic
                await ReconnectWebSocket();
            }
        }
        private async void StartReceiving(CancellationToken cancellationToken)
        {
            try
            {
                // Create a dictionary
                // ping hunt types to their corresponding configuration flags
                var HuntTypeEnabledMap = new Dictionary<string, bool>
                {
                    { "Shadowbringers", this.Configuration.ShadowbringersHunts },
                    { "Centurio", this.Configuration.CenturioHunts },
                    { "Endwalker", this.Configuration.EndwalkerHunts },
                    // Add more mappings as necessary
                };

                ConcurrentDictionary<(string Kind, string World), DateTime> recentMessagesCache = [];


                var buffer = new byte[2048];
                while (_webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    // Remove entries older than 2 minutes
                    var twoMinutesAgo = DateTime.Now - TimeSpan.FromMinutes(2);
                    var keysToRemove = recentMessagesCache.Where(kvp => kvp.Value < twoMinutesAgo).Select(kvp => kvp.Key).ToList();
                    foreach (var key in keysToRemove)
                    {
                        recentMessagesCache.TryRemove(key, out _);
                    }

                    // Get received message
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);


                    // Process received message
                    var messageString = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    ProcessMessage(messageString, recentMessagesCache);
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when the task is canceled. No need to log as an error.
                PluginLog.Information("WebSocket receive task was canceled.");
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    // PluginLog.Error($"WebSocket connection error: {ex}");
                    // If cancellation hasn't been requested, this is an actual error.
                    PluginLog.Warning($"Lost connection to the server");
                    // Start reconnection logic.
                    await ReconnectWebSocket();
                }
            }
        }

        public void ProcessMessage(string messageString, ConcurrentDictionary<(string Kind, string World), DateTime> recentMessagesCache)
        {
            try
            {
                // Check if the message is an admin alert
                if (messageString.StartsWith("Alert:"))
                {
                    // Extract the alert message
                    var alertMessage = messageString.Substring("Alert:".Length).Trim();

                    var formattedAlertMessage = "HuntAlerts Admin Broadcast\n" + alertMessage;

                    var message = new SeStringBuilder().AddUiForeground((ushort)16).AddText(formattedAlertMessage).AddUiForegroundOff().Build();
                    if (!this.Configuration.UseDalamudChat)
                    {
                        Svc.Chat.Print(new() { Message = message, Type = this.Configuration.OutputChat });
                    }
                    else
                    {
                        Svc.Chat.Print(new() { Message = message });
                    }

                    // Skip the rest of the processing for this message
                    return;
                }


                bool? teleporterInstalled = Svc.PluginInterface.InstalledPlugins.FirstOrDefault(x => x.InternalName == "TeleporterPlugin")?.IsLoaded;
                bool? lifestreamInstalled = Svc.PluginInterface.InstalledPlugins.FirstOrDefault(x => x.InternalName == "Lifestream")?.IsLoaded;

                var huntMessage = JsonConvert.DeserializeObject<HuntMessage>(messageString);

                string currentdatacentername = "";
                string homeworldName = "";
                string currentworldName = "";

                if (Svc.ClientState.IsLoggedIn)
                {
                    //now working with game data and must use game thread
                    new TickScheduler(() =>
                    {
                        try
                        {
                            if (huntMessage.Type == "new_hunt")
                            {

                                PluginLog.Verbose($"New train data received: Kind:" + huntMessage.Kind + " | World:" + huntMessage.World);
                                string locationCoords = "";
                                var key = (huntMessage.Kind, huntMessage.World);
                                if (recentMessagesCache.TryGetValue(key, out var lastTimestamp))
                                {
                                    if (DateTime.Now - lastTimestamp < TimeSpan.FromMinutes(2))
                                    {
                                        // Message with same Kind and World received within last 2 minutes
                                        PluginLog.Verbose("Similar message received recently, suppressing notification");
                                        return;
                                    }
                                }

                                // Check if suppress duplicate message is enabled and record if true
                                if (this.Configuration.SuppressDuplicates)
                                {
                                    // Update the cache with the new timestamp
                                    recentMessagesCache[key] = DateTime.Now;
                                }


                                // Check if datacenter is enabled
                                bool isDataCenterEnabled = this.Configuration.WorldDatacenterMap.TryGetValue(huntMessage.World, out var dataCenter) && IsDataCenterEnabled(dataCenter);

                                // Check if the world is enabled
                                bool isWorldEnabled = IsWorldEnabled(huntMessage.World);


                                // Check if the hunt type is enabled
                                bool isHuntEnabled = IsHuntEnabled(huntMessage.Kind);


                                if (Svc.ClientState.IsLoggedIn && Svc.ClientState.LocalPlayer != null)
                                {
                                    homeworldName = Svc.ClientState.LocalPlayer.HomeWorld.GameData.Name;
                                    currentworldName = Svc.ClientState.LocalPlayer.CurrentWorld.GameData.Name;
                                    PluginLog.Verbose($"Player is logged in. Homeworld: " + homeworldName + " | Currentworld: " + currentworldName);
                                }
                                else
                                {
                                    PluginLog.Verbose($"Player is not logged in");
                                }

                                bool currentworldOnly = this.Configuration.CurrentWorldOnly;
                                bool homeworldOnly = this.Configuration.HomeWorldOnly;

                                // Checks against Current world only option
                                if (currentworldOnly && huntMessage.World != currentworldName)
                                {
                                    PluginLog.Verbose("Current World Only option is enabled and player is not on the hunt world currently, suppressing notification");
                                    return;
                                }

                                // Checks against Homeworld Only option
                                if (homeworldOnly && huntMessage.World != homeworldName)
                                {
                                    PluginLog.Verbose("Home World Only option is enabled and hunt is not for player's home world, suppressing notification");
                                    return;
                                }


                                // Check if the data center is enabled
                                if (!isDataCenterEnabled)
                                {
                                    // Data center is not enabled or unknown world
                                    PluginLog.Verbose("Datacenter is not enabled, suppressing notification");
                                    return;
                                }

                                // Check if the world is enabled
                                if (!isWorldEnabled)
                                {
                                    // World is not enabled
                                    PluginLog.Verbose("World is not enabled, suppressing notification");
                                    return;
                                }

                                // Check if the hunt type is enabled
                                if (!isHuntEnabled)
                                {
                                    // Hunt type is not enabled
                                    PluginLog.Verbose("Hunt type is not enabled, suppressing notification");
                                    return;
                                }



                                PluginLog.Debug($"EndwalkerHunts setting: {this.Configuration.EndwalkerHunts}");
                                PluginLog.Debug($"ShadowbringersHunts setting: {this.Configuration.ShadowbringersHunts}");
                                PluginLog.Debug($"CenturioHunts setting: {this.Configuration.CenturioHunts}");

                                // Format the main hunt message
                                string messageContent = huntMessage.Content;

                                // Fix timestamps from unix time to local time
                                messageContent = ReplaceTimestampsWithLocalTime(messageContent);

                                // Remove emojis from the message
                                messageContent = RemoveDiscordEmojis(messageContent);

                                // Adds header to the message
                                //messageContent = "Hunt: " + huntMessage.Kind + Environment.NewLine + "World: " + huntMessage.World + Environment.NewLine + "Posted: "+ ConvertTime(huntMessage.Posted_Epoch) + Environment.NewLine + Environment.NewLine + messageContent;


                                // Code to handle the hunt
                                // Since the handling code is the same for all hunts, place it here



                                // Get current region
                                string currentregionName = "";
                                if (Svc.ClientState.IsLoggedIn && Svc.ClientState.LocalPlayer != null)
                                {
                                    currentworldName = Svc.ClientState.LocalPlayer.CurrentWorld.GameData.Name;
                                    currentregionName = this.Configuration.DatacenterRegionMap[this.Configuration.WorldDatacenterMap[currentworldName]];
                                    currentdatacentername = this.Configuration.WorldDatacenterMap[currentworldName];

                                    PluginLog.Verbose($"Player is logged in. Homeworld: " + homeworldName + " | Currentworld: " + currentworldName + " | Currentregion: " + currentregionName);
                                }
                                else
                                {
                                    PluginLog.Verbose($"Player is not logged in");
                                }



                                // Get hunt region
                                string huntregionName = this.Configuration.DatacenterRegionMap[this.Configuration.WorldDatacenterMap[huntMessage.World]];
                                bool teleporterEnabled = this.Configuration.TeleporterIntegration && (teleporterInstalled == true);
                                bool lifestreamEnabled = this.Configuration.LifestreamIntegration && (lifestreamInstalled == true);
                                bool openmaponArrival = this.Configuration.OpenMapOnArrival;
                                string startLocation = huntMessage.AetheriteName; //ParseForStartLocation(messageContent);
                                string startZone = huntMessage.LocationName; //ParseForStartZone(messageContent);
                                string aetheriteName = huntMessage.AetheriteName;
                                string formatted_message = $"Kind: Hunt Train{Environment.NewLine}Hunt: {huntMessage.Kind}{Environment.NewLine}World: {huntMessage.World}{Environment.NewLine}Posted: {ConvertTime(huntMessage.Posted_Epoch)}{Environment.NewLine}{Environment.NewLine}" + messageContent;



                                try
                                {
                                    // Try extracting coordinates from message for start location
                                    var (coord_x, coord_y) = ExtractCoordinates(messageContent);
                                    PluginLog.Verbose($"Extracted Coordinates {coord_x}, {coord_y} from message");

                                    // Get ZoneID
                                    uint tt;
                                    if (Svc.Data.GetExcelSheet<TerritoryType>().TryGetFirst(x => x.TerritoryIntendedUse == (uint)TerritoryIntendedUseEnum.Open_World && (x.PlaceName.Value?.Name.ExtractText() ?? "").EqualsIgnoreCase(startZone), out var value))
                                    {
                                        tt = value.RowId; //is territory id
                                                          // Get Nearest Aetherite from coords
                                        if (coord_x is not null && coord_y is not null)
                                        {
                                            if (startLocation == "invalid")
                                            {
                                                startLocation = MapManager.GetNearestAetheryte(tt, (float)coord_x, (float)coord_y);
                                                PluginLog.Verbose($"Found nearest aetheryte on map id {tt} at {startLocation}");

                                            }
                                            locationCoords = $"{(float)coord_x}, {(float)coord_y}";
                                        }
                                    }


                                }
                                catch (Exception ex)
                                {
                                    PluginLog.Error("Error parsing start location aetherite.");
                                    PluginLog.Error(ex.ToString());
                                }



                                int textColor = this.Configuration.TextColor;
                                SeString message;

                                var htmessage = new HuntTrainMessage(formatted_message, huntMessage.Type, huntMessage.Kind, huntMessage.World, currentworldName, currentregionName, huntregionName, ConvertTime(huntMessage.Posted_Epoch), startLocation, startZone, locationCoords, openmaponArrival, teleporterEnabled, lifestreamEnabled);
                                var link = P.MessageCacheManager.AddMessage(htmessage);
                                Service.IPCManager.OnHuntTrainMessageReceived(htmessage);

                                if (textColor != 0)
                                {
                                    message = new SeStringBuilder().AddUiForeground((ushort)textColor).Add(link).AddText("New " + huntMessage.Kind + " train starting soon on " + huntMessage.World + "!").Add(RawPayload.LinkTerminator).AddUiForegroundOff().Build();
                                }
                                else
                                {
                                    message = new SeStringBuilder().Add(link).AddText("New " + huntMessage.Kind + " train starting soon on " + huntMessage.World + "!").Add(RawPayload.LinkTerminator).Build();
                                }


                                if (!this.Configuration.UseDalamudChat)
                                {
                                    Svc.Chat.Print(new() { Message = message, Type = this.Configuration.OutputChat });
                                }
                                else
                                {
                                    Svc.Chat.Print(new() { Message = message });
                                }



                                var msg = RemoveSymbolsRegex().Replace(message.ToString(), "");
                                PluginLog.Debug($"Adding cache entry {msg}");
                                PluginLog.Verbose($"Teleporter: {teleporterEnabled} | Lifestream: {lifestreamEnabled} | startLocation: {startLocation} | startZone: {startZone}");

                                // Play sound effect if one is set
                                if (this.Configuration.SoundEffect != 0)
                                {
                                    UIModule.PlayChatSoundEffect((uint)this.Configuration.SoundEffect); // Play the selected sound effect
                                }
                            }
                            else if (huntMessage.Type == "srank")
                            {
                                bool isSRankEnabled = this.Configuration.SRankEnabled;
                                bool srankcurrentworldOnly = this.Configuration.SRankCurrentWorld;
                                string world = huntMessage.World;
                                string kind = huntMessage.Kind;
                                string creatureName = huntMessage.CreatureName;
                                string locationName = huntMessage.LocationName;
                                string locationCoords = huntMessage.LocationCoords;
                                string aetheriteName = huntMessage.AetheriteName;
                                long deathTime = huntMessage.DeathTime;
                                long postedTime = huntMessage.Posted_Epoch;
                                string huntdatacenterName = this.Configuration.WorldDatacenterMap[huntMessage.World];
                                SeString message = "";
                                string messageContent = "";

                                if (isSRankEnabled)
                                {
                                    // Get Notification settings
                                    bool srankEndwalker = this.Configuration.EndwalkerSRank;
                                    bool srankShadowbringers = this.Configuration.ShadowbringersSRank;
                                    bool srankCenturio = this.Configuration.CenturioSRank;

                                    if ((srankEndwalker && kind == "EW") || (srankShadowbringers && kind == "SHB") || (srankCenturio && (kind == "ARR" || kind == "HW" || kind == "SB")))
                                    {
                                        // Get current region
                                        string currentregionName = "";
                                        if (Svc.ClientState.IsLoggedIn && Svc.ClientState.LocalPlayer != null)
                                        {
                                            currentworldName = Svc.ClientState.LocalPlayer.CurrentWorld.GameData.Name;
                                            currentdatacentername = this.Configuration.WorldDatacenterMap[currentworldName];
                                            currentregionName = this.Configuration.DatacenterRegionMap[this.Configuration.WorldDatacenterMap[currentworldName]];
                                            homeworldName = Svc.ClientState.LocalPlayer.HomeWorld.GameData.Name;
                                            PluginLog.Verbose($"Player is logged in. Homeworld: " + homeworldName + " | Currentworld: " + currentworldName + " | Currentregion: " + currentregionName);
                                        }
                                        else
                                        {
                                            PluginLog.Verbose($"Player is not logged in");
                                        }
                                        if (((srankcurrentworldOnly && (currentworldName == world)) || srankcurrentworldOnly == false) && (currentdatacentername == huntdatacenterName))
                                        {
                                            // Get hunt region
                                            string huntregionName = this.Configuration.DatacenterRegionMap[this.Configuration.WorldDatacenterMap[huntMessage.World]];
                                            bool teleporterEnabled = this.Configuration.TeleporterIntegration && (teleporterInstalled == true);
                                            bool lifestreamEnabled = this.Configuration.LifestreamIntegration && (lifestreamInstalled == true);
                                            bool openmaponArrival = this.Configuration.OpenMapOnArrival;
                                            string startLocation = aetheriteName;
                                            string startZone = locationName;

                                            if (deathTime == 0)
                                            {
                                                int sranktextColor = this.Configuration.SRankTextColor;
                                                //string headerText = $"Hunt: {huntType}{Environment.NewLine}World: {world}{Environment.NewLine}Posted: {postedTime}{Environment.NewLine}{Environment.NewLine}";
                                                messageContent = $"Type: S Rank{Environment.NewLine}Hunt: {kind}{Environment.NewLine}World: {world}{Environment.NewLine}Posted: {ConvertTime(postedTime)}{Environment.NewLine}Creature: {creatureName}{Environment.NewLine}{Environment.NewLine}Location: {locationName} ({locationCoords}){Environment.NewLine}Aetherite: {aetheriteName}";

                                                var htmessage = new HuntTrainMessage(messageContent, huntMessage.Type, huntMessage.Kind, huntMessage.World, currentworldName, currentregionName, huntregionName, ConvertTime(huntMessage.Posted_Epoch), startLocation, startZone, locationCoords, openmaponArrival, teleporterEnabled, lifestreamEnabled);
                                                var link = P.MessageCacheManager.AddMessage(htmessage);
                                                Service.IPCManager.OnHuntTrainMessageReceived(htmessage);

                                                if (sranktextColor != 0)
                                                {
                                                    message = new SeStringBuilder().AddUiForeground((ushort)sranktextColor).Add(link).AddText($"New {kind} S Rank {creatureName} spawned on {world}!").Add(RawPayload.LinkTerminator).AddUiForegroundOff().Build();
                                                }
                                                else
                                                {
                                                    message = new SeStringBuilder().Add(link).AddText($"New {kind} S Rank {creatureName} spawned on {world}!").Add(RawPayload.LinkTerminator).Build();
                                                }

                                                PluginLog.Verbose($"deathTime = {deathTime}");

                                                if (!this.Configuration.UseDalamudChat)
                                                {
                                                    Svc.Chat.Print(new() { Message = message, Type = this.Configuration.OutputChat });
                                                }
                                                else
                                                {
                                                    Svc.Chat.Print(new() { Message = message });
                                                }

                                                var msg = RemoveSymbolsRegex().Replace(message.ToString(), "");
                                                PluginLog.Verbose($"currentWorld: {currentworldName}  |  currentRegion: {currentregionName}  |  huntWorld: {huntMessage.World}  |  huntRegion: {huntregionName}");

                                                // Play sound effect if one is set
                                                if (this.Configuration.SoundEffect != 0)
                                                {
                                                    UIModule.PlayChatSoundEffect((uint)this.Configuration.SoundEffect); // Play the selected sound effect
                                                }
                                            }
                                            else
                                            {

                                                int sranktextColor = this.Configuration.SRankTextColor;
                                                if (sranktextColor != 0)
                                                {
                                                    message = new SeStringBuilder().AddUiForeground((ushort)sranktextColor).AddText($"S Rank {creatureName} on {world} was killed at {ConvertTime(deathTime)}.").AddUiForegroundOff().Build();
                                                }
                                                else
                                                {
                                                    message = new SeStringBuilder().AddText($"{kind} S Rank {creatureName} on {world} was killed at {ConvertTime(deathTime)}.").Build();
                                                }

                                                if (!this.Configuration.UseDalamudChat)
                                                {
                                                    Svc.Chat.Print(new() { Message = message, Type = this.Configuration.OutputChat });
                                                }
                                                else
                                                {
                                                    Svc.Chat.Print(new() { Message = message });
                                                }
                                            }
                                        }
                                        else
                                        {
                                            PluginLog.Verbose($"{kind} S Rank {creatureName} spawned on {world} but skipping due to settings or not on same datacenter");
                                        }
                                    }
                                    else
                                    {
                                        PluginLog.Verbose($"Skipping S Rank because of notification settings");
                                    }



                                }
                            }
                        }
                        catch (Exception e)
                        {
                            e.Log();
                        }
                    });
                }

            }
            catch (JsonException ex)
            {
                // Handle JSON parsing error
                PluginLog.Warning("Plugin had issues parsing json");
                PluginLog.Verbose($"Plugin had issues parsing json: {ex}");
            }
        }

        private async Task CloseWebSocketAsync()
        {
            PluginLog.Verbose($"Attempting to close websocket. State: {_webSocket.State}");
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                // Send a close message to the server
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing connection", CancellationToken.None);
                PluginLog.Information("WebSocket connection closed gracefully.");
            }
        }

        private async Task ReconnectWebSocket()
        {
            var retryInterval = 5000; // milliseconds to wait before retrying to connect
            while (!_cancellationTokenSource.IsCancellationRequested && _webSocket.State != WebSocketState.Open)
            {
                try
                {
                    PluginLog.Information("Attempting to reconnect WebSocket...");
                    await Task.Delay(retryInterval, _cancellationTokenSource.Token); // Wait before reconnecting
                    _webSocket.Dispose(); // Dispose the old instance
                    _webSocket = new ClientWebSocket(); // Create a new instance
                    await _webSocket.ConnectAsync(new Uri(serverURI), _cancellationTokenSource.Token);
                    PluginLog.Information("Reconnected to WebSocket.");
                    StartReceiving(_cancellationTokenSource.Token); // Start listening for messages again
                }
                catch (Exception ex)
                {
                    PluginLog.Warning($"Websocket reconnection error");
                    PluginLog.Verbose($"WebSocket reconnection error: {ex}");
                    // Loop will continue until connection is re-established

                }
            }
        }
    }
}
