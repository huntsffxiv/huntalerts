using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using HuntAlerts.Windows;
using System.Net.WebSockets;
using System.Threading;
using System;
using Newtonsoft.Json;
using System.Text;
using System.Collections.Generic;
using static HuntAlerts.Plugin;
using FFXIVClientStructs.FFXIV.Client.System;
using Dalamud.Logging;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Ipc.Exceptions;
using System.Text.RegularExpressions;
using FFXIVClientStructs.FFXIV.Client.UI;
using System.Runtime.InteropServices;
using System.Linq;
using Dalamud.Game.Text;
using System.Drawing;
using ECommons.DalamudServices;

namespace HuntAlerts
{
    public sealed partial class Plugin : IDalamudPlugin
    {
        public string Name => "Hunt Alerts";
        private const string CommandName = "/huntalerts";
        private ClientWebSocket _webSocket;
        private IChatGui _chatGui;
        private CancellationTokenSource _cancellationTokenSource;

        public string serverURI = "ws://huntrelay.eastus.cloudapp.azure.com:6789";
        //public string serverURI = "ws://localhost:6789";


        private DalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        private IDataManager Data { get; init; }
        private IClientState ClientState { get; set; }
        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("HuntAlerts");

        private ConfigWindow ConfigWindow { get; init; }

        DalamudLinkPayload LinkPayload;
        NotifyWindow NotifyWindow;

        public Plugin(
            DalamudPluginInterface pluginInterface,
            ICommandManager commandManager, IDataManager data, IClientState clientState)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.Data = data;
            this.ClientState = clientState;

            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);
            Svc.Init(pluginInterface);


            ConfigWindow = new ConfigWindow(this);
            
            WindowSystem.AddWindow(ConfigWindow);
            NotifyWindow = new();
            WindowSystem.AddWindow(NotifyWindow);

            this.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the HuntAlerts options"
            });

            this.PluginInterface.UiBuilder.Draw += DrawUI;
            this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            InitializeWebSocket();

            LinkPayload = PluginInterface.AddChatLinkHandler(0, (id, s) =>
            {
                var msg = RemoveSymbolsRegex().Replace(s.ToString(), "");
                NotifyWindow.IsOpen = true;
                NotifyWindow.CurrentPayload = msg;
                PluginLog.Debug($"Opening window for message {msg}");
            });
        }

        public static string WordWrap(string text, int maxLineLength)
        {
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var currentLineLength = 0;
            var result = new StringBuilder();

            foreach (var word in words)
            {
                if (currentLineLength + word.Length + 1 > maxLineLength)
                {
                    result.AppendLine();
                    currentLineLength = 0;
                }

                if (currentLineLength > 0)
                {
                    result.Append(' ');
                    currentLineLength++;
                }

                result.Append(word);
                currentLineLength += word.Length;
            }

            return result.ToString();
        }
        

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



        private async Task ReconnectWebSocket()
        {
            var retryInterval = 5000; // milliseconds to wait before retrying to connect
            while (!_cancellationTokenSource.IsCancellationRequested && _webSocket.State != WebSocketState.Open)
            {
                try
                {
                    PluginLog.Information("Attempting to reconnect WebSocket...");
                    await Task.Delay(retryInterval,_cancellationTokenSource.Token); // Wait before reconnecting
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

        public class HuntMessage
        {
            public string Type { get; set; }
            public string Content { get; set; }
            public string World { get; set; }
            public string Kind { get; set; }
            public long Posted_Epoch { get; set; }
            public Dictionary<string, object> AdditionalData { get; set; }
        }

        private bool IsDataCenterEnabled(string dataCenter)
        {
            return dataCenter switch
            {
                "Aether" => this.Configuration.Aether,
                "Primal" => this.Configuration.Primal,
                "Crystal" => this.Configuration.Crystal,
                "Dynamis" => this.Configuration.Dynamis,
                "Light" => this.Configuration.Light,
                "Chaos" => this.Configuration.Chaos,
                _ => false,
            };
        }

        private bool IsWorldEnabled(string world)
        {
            return world switch
            {
                // Aether
                "Midgardsormr" => this.Configuration.MidgardsormrWorld,
                "Faerie" => this.Configuration.FaerieWorld,
                "Jenova" => this.Configuration.JenovaWorld,
                "Cactuar" => this.Configuration.CactuarWorld,
                "Sargatanas" => this.Configuration.SargatanasWorld,
                "Adamantoise" => this.Configuration.AdamantoiseWorld,
                "Siren" => this.Configuration.SirenWorld,
                "Gilgamesh" => this.Configuration.GilgameshWorld,

                // Primal
                "Behemoth" => this.Configuration.BehemothWorld,
                "Excalibur" => this.Configuration.ExcaliburWorld,
                "Exodus" => this.Configuration.ExodusWorld,
                "Famfrit" => this.Configuration.FamfritWorld,
                "Hyperion" => this.Configuration.HyperionWorld,
                "Lamia" => this.Configuration.LamiaWorld,
                "Leviathan" => this.Configuration.LeviathanWorld,
                "Ultros" => this.Configuration.UltrosWorld,

                // Crystal
                "Balmung" => this.Configuration.BalmungWorld,
                "Brynhildr" => this.Configuration.BrynhildrWorld,
                "Coeurl" => this.Configuration.CoeurlWorld,
                "Diabolos" => this.Configuration.DiabolosWorld,
                "Goblin" => this.Configuration.GoblinWorld,
                "Malboro" => this.Configuration.MalboroWorld,
                "Mateus" => this.Configuration.MateusWorld,
                "Zalera" => this.Configuration.ZaleraWorld,

                // Dynamis
                "Halicarnassus" => this.Configuration.HalicarnassusWorld,
                "Maduin" => this.Configuration.MaduinWorld,
                "Marilith" => this.Configuration.MarilithWorld,
                "Seraph" => this.Configuration.SeraphWorld,

                // Chaos
                "Cerberus" => this.Configuration.CerberusWorld,
                "Louisoix" => this.Configuration.LouisoixWorld,
                "Moogle" => this.Configuration.MoogleWorld,
                "Omega" => this.Configuration.OmegaWorld,
                "Phantom" => this.Configuration.PhantomWorld,
                "Ragnarok" => this.Configuration.RagnarokWorld,
                "Sagittarius" => this.Configuration.SagittariusWorld,
                "Spriggan" => this.Configuration.SprigganWorld,

                // Light
                "Alpha" => this.Configuration.AlphaWorld,
                "Lich" => this.Configuration.LichWorld,
                "Odin" => this.Configuration.OdinWorld,
                "Phoenix" => this.Configuration.PhoenixWorld,
                "Raiden" => this.Configuration.RaidenWorld,
                "Shiva" => this.Configuration.ShivaWorld,
                "Twintania" => this.Configuration.TwintaniaWorld,
                "Zodiark" => this.Configuration.ZodiarkWorld,
                _ => false,
            };
        }



        private bool IsHuntEnabled(string huntKinds)
        {
            // Split the huntKinds string by comma and trim any whitespace
            var huntTypes = huntKinds.Split(',').Select(h => h.Trim());

            // Check if any of the hunt types are enabled in the configuration
            foreach (var huntType in huntTypes)
            {
                switch (huntType)
                {
                    case "Endwalker":
                        if (this.Configuration.EndwalkerHunts) return true;
                        break;
                    case "Shadowbringers":
                        if (this.Configuration.ShadowbringersHunts) return true;
                        break;
                    case "Centurio":
                        if (this.Configuration.CenturioHunts) return true;
                        break;
                        // Add cases for other hunt types as necessary
                }
            }

            // Return false if none of the hunt types are enabled
            return false;
        }


        private static string ReplaceTimestampsWithLocalTime(string input)
        {
            // Regex pattern to find timestamps
            string pattern = @"<t:(\d+):(t|T|d|D|f|F|R)>";

            // Replace each match in the input string
            return Regex.Replace(input, pattern, match =>
            {
                // Extract the Unix timestamp from the match
                long unixTimestamp = long.Parse(match.Groups[1].Value);

                string time = ConvertTime(unixTimestamp);

                // Convert Unix timestamp to DateTime
                //DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).ToLocalTime().DateTime;

                // Format the DateTime as needed, e.g., "MM/dd/yyyy HH:mm:ss"
                //return dateTime.ToString("g"); // or any other format
                return time;
            });
        }

        private static string RemoveDiscordEmojis(string input)
        {
            // Regex pattern to find Discord emojis
            string emojiPattern = @"<a?:(\w+):\d+>";

            // Replace each match (Discord emoji) with an empty string
            return Regex.Replace(input, emojiPattern, "");
        }

        private static string ConvertTime(long epochTime)
        {

            // Convert Unix timestamp to DateTime
            DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(epochTime).ToLocalTime().DateTime;

            // Format the DateTime as needed, e.g., "MM/dd/yyyy HH:mm:ss"
            PluginLog.Verbose($"Posted Time:  {epochTime}");
            string convertedTime = dateTime.ToString("hh:mm tt"); // or any other format
            PluginLog.Verbose($"converted time: {convertedTime}");
            return convertedTime;
        }




        public void PrintColoredHuntMessage(HuntMessage huntMessage, string hexColor)
        {
            string messageText = $"New {huntMessage.Kind} train starting soon on {huntMessage.World}!!";
            string coloredMessage = $"[3C][{hexColor}]{messageText}[/COLOR][/3C]";

            Svc.Chat.Print(new XivChatEntry
            {
                Message = new SeString(new Payload[] { new TextPayload(coloredMessage) })
            });
        }


        private async void StartReceiving(CancellationToken cancellationToken)
        {

            


            try
            {
                // Create a dictionary mapping hunt types to their corresponding configuration flags
                var HuntTypeEnabledMap = new Dictionary<string, bool>
                {
                    { "Shadowbringers", this.Configuration.ShadowbringersHunts },
                    { "Centurio", this.Configuration.CenturioHunts },
                    { "Endwalker", this.Configuration.EndwalkerHunts },
                    // Add more mappings as necessary
                };

                Dictionary<(string Kind, string World), DateTime> recentMessagesCache = new Dictionary<(string Kind, string World), DateTime>();


                var buffer = new byte[2048];
                while (_webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    // Remove entries older than 2 minutes
                    var twoMinutesAgo = DateTime.Now - TimeSpan.FromMinutes(2);
                    var keysToRemove = recentMessagesCache.Where(kvp => kvp.Value < twoMinutesAgo).Select(kvp => kvp.Key).ToList();
                    foreach (var key in keysToRemove)
                    {
                        recentMessagesCache.Remove(key);
                    }

                    // Get received message
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                    // Process received message
                    var messageString = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    try
                    {
                        
                        var huntMessage = JsonConvert.DeserializeObject<HuntMessage>(messageString);

                        PluginLog.Verbose($"New train data received: Kind:" + huntMessage.Kind + " | World:" + huntMessage.World);

                        var key = (huntMessage.Kind, huntMessage.World);
                        if (recentMessagesCache.TryGetValue(key, out var lastTimestamp))
                        {
                            if (DateTime.Now - lastTimestamp < TimeSpan.FromMinutes(2))
                            {
                                // Message with same Kind and World received within last 2 minutes
                                PluginLog.Verbose("Similar message received recently, suppressing notification");
                                continue;
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




                        string homeworldName = "";
                        string currentworldName = "";


                        if(this.ClientState.IsLoggedIn && this.ClientState.LocalPlayer != null)
                        {
                            homeworldName = ClientState.LocalPlayer.HomeWorld.GameData.Name;
                            currentworldName = ClientState.LocalPlayer.CurrentWorld.GameData.Name;
                            PluginLog.Verbose($"Player is logged in. Homeworld: " + currentworldName + " | Currentworld: " + currentworldName);
                        }else
                        {
                            PluginLog.Verbose($"Player is not logged in");
                        }

                        bool currentworldOnly = this.Configuration.CurrentWorldOnly;
                        bool homeworldOnly = this.Configuration.HomeWorldOnly;

                        // Checks against Current world only option
                        if (currentworldOnly && huntMessage.World != currentworldName)
                        {        
                            PluginLog.Verbose("Current World Only option is enabled and player is not on the hunt world currently, suppressing notification");
                            continue;
                        }

                        // Checks against Homeworld Only option
                        if (homeworldOnly && huntMessage.World != homeworldName)
                        {
                            PluginLog.Verbose("Home World Only option is enabled and hunt is not for player's home world, suppressing notification");
                            continue;
                        }


                        // Check if the data center is enabled
                        if (!isDataCenterEnabled)
                        {
                            // Data center is not enabled or unknown world
                            PluginLog.Verbose("Datacenter is not enabled, suppressing notification");
                            continue;
                        }

                        // Check if the world is enabled
                        if (!isWorldEnabled)
                        {
                            // World is not enabled
                            PluginLog.Verbose("World is not enabled, suppressing notification");
                            continue;
                        }

                        // Check if the hunt type is enabled
                        if (!isHuntEnabled)
                        {
                            // Hunt type is not enabled
                            PluginLog.Verbose("Hunt type is not enabled, suppressing notification");
                            continue;
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

                        int textColor = this.Configuration.TextColor;
                        SeString message;
                        if (textColor != 0)
                        {
                            message = new SeStringBuilder().AddUiForeground((ushort)textColor).Add(LinkPayload).AddText("New " + huntMessage.Kind + " train starting soon on " + huntMessage.World + "!!").Add(RawPayload.LinkTerminator).AddUiForegroundOff().Build();
                        }
                        else
                        {
                            message = new SeStringBuilder().Add(LinkPayload).AddText("New " + huntMessage.Kind + " train starting soon on " + huntMessage.World + "!!").Add(RawPayload.LinkTerminator).Build();
                        }


                        // Get current region
                        string currentregionName = "";
                        if (Svc.ClientState.IsLoggedIn && Svc.ClientState.LocalPlayer != null)
                        {
                            currentworldName = Svc.ClientState.LocalPlayer.CurrentWorld.GameData.Name;
                            currentregionName = this.Configuration.DatacenterRegionMap[this.Configuration.WorldDatacenterMap[currentworldName]];
                            PluginLog.Verbose($"Player is logged in. Homeworld: " + currentworldName + " | Currentworld: " + currentworldName + " | Currentregion: "+ currentregionName);
                        }
                        else
                        {
                            PluginLog.Verbose($"Player is not logged in");
                        }

                        // Get hunt region
                        string huntregionName = this.Configuration.DatacenterRegionMap[this.Configuration.WorldDatacenterMap[huntMessage.World]];
                        bool teleporterEnabled = this.Configuration.TeleporterIntegration;
                        bool lifestreamEnabled = this.Configuration.LifestreamIntegration;
                        string startLocation = ParseForStartLocation(messageContent);
                        string startZone = ParseForStartZone(messageContent);

                        Svc.Chat.Print(new() { Message = message });
                        var msg = RemoveSymbolsRegex().Replace(message.ToString(), "");
                        PluginLog.Debug($"Adding cache entry {msg}");
                        NotifyWindow.Cache[msg] = (messageContent,huntMessage.Kind, huntMessage.World,currentworldName,currentregionName, huntregionName, ConvertTime(huntMessage.Posted_Epoch),startLocation,startZone, teleporterEnabled,lifestreamEnabled);

                        // Play sound effect if one is set
                        if (this.Configuration.SoundEffect != 0)
                        {
                            UIModule.PlayChatSoundEffect((uint)this.Configuration.SoundEffect); // Play the selected sound effect
                        }


                    }
                    catch (JsonException ex)
                    {
                        // Handle JSON parsing error
                        PluginLog.Warning("Plugin had issues parsing json");
                        PluginLog.Verbose($"Plugin had issues parsing json: {ex}");
                    }
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
        public static string ParseForStartZone(string message)
        {
            // Define a dictionary mapping keywords to corresponding values
            var keywordMap = new Dictionary<string, string>
            {
                // EW
                { "mare", "Mare Lamentorum" },
                { "thule", "Ultima Thule" },
                { "thav", "Thavnair" },
                { "elpis", "Elpis" },
                { "garlemald", "Garlemald" },
                { "laby", "Labyrinthos" },

                // SHB
                
                { "lakeland", "Lakeland" },
                { "kholusia", "Kholusia" },
                { "araeng", "Amh Araeng" },
                { "mheg", "Il Mheg" },
                { "greatwood", "The Rak'tika Greatwood" },
                { "tempest", "The Tempest" },
                

                // SB
                { "fringes", "The Fringes" },
                { "ruby sea", "The Ruby Sea" },
                { "azim", "The Azim Steppe" },
                { "lochs", "The Lochs" },
                { "peaks", "The Peaks" },


                // HW
                { "sea of clouds", "The Sea of Clouds" },
                { "azys", "Azys Lla" },
                { "forelands", "The Dravanian Forelands" },
                { "mists", "The Churning Mists" },
            };

            // Convert message to lower case for case-insensitive comparison
            string lowerMessage = message.ToLower();

            // Find which keywords are in the input string
            var foundKeywords = keywordMap.Keys.Where(keyword => lowerMessage.Contains(keyword)).ToList();

            // Prepare the result string
            string result;

            if (foundKeywords.Count > 1)
            {
                // Get the corresponding value from the dictionary
                result = keywordMap[foundKeywords.First()];
            }
            else
            {
                result = "invalid";
            }

            return result;
        }

        public static string ParseForStartLocation(string message)
        {
            // Define a dictionary mapping keywords to corresponding values
            var keywordMap = new Dictionary<string, string>
            {
                { "fort", "Fort Jobb" },
                { "foot", "Fort Jobb" },
                { "ostall", "The Ostall Imperative" },
                { "great work", "The Great Work" },
                { "palaka", "Palaka's Stand" },
                { "yedli", "Yedlihmad" },
                { "castrum", "Castrum Oriens" },
                { "camp broken", "Camp Broken Glass" },
                { "sinus", "Sinus Lacrimarum" },
                { "tertium", "Tertium" },
                { "anag", "Anagnorisis" },
                { "wonder", "The Twelve Wonders" },
                { "poie", "Poieten Oikos" },
                { "apor", "Aporia" },
                { "arche", "The Archeion" },
                { "haml", "Sharlayan Hamlet" },
                { "ondo", "The Ondo Cups" },
                { "lydha", "Lydha Lran" },
                { "slither", "Slitherbough" },
                { "fanow", "Fanow" },
                { "twine", "Twine" },
                { "mord", "Mord Souq" },
                { "inn", "The Inn at Journey's Head" },
                { "tomra", "Tomra" },
                { "wrig", "Wright" },
                { "stilltide", "Stilltide" },
                { "wole", "Wolekdorf" },
                { "peer", "The Peering Stones" },
                { "gannh", "Ala Gannha" },
                { "ghiri", "Ala Ghiri" },
                { "porta", "Porta Praetoria" },
                { "quart", "The Ala Mhigan Quarter" },
                { "ono", "Onokoro" },
                { "tama", "Tamamizu" },
                { "house", "The House of the Fierce" },
                { "dhor", "Dhoro Iloh" },
                { "reun", "Reunion" },
                { "throne", "The Dawn Throne" },

            };

            // Convert message to lower case for case-insensitive comparison
            string lowerMessage = message.ToLower();

            // Find which keywords are in the input string
            var foundKeywords = keywordMap.Keys.Where(keyword => lowerMessage.Contains(keyword)).ToList();

            // Prepare the result string
            string result;

            if (foundKeywords.Count > 0)
            {
                // Get the corresponding value from the dictionary
                result = keywordMap[foundKeywords.First()];
            }
            else
            {
                result = "invalid";
            }

            return result;
        }


        public void Test()
        {

            var message = new SeStringBuilder().Add(LinkPayload).AddText($"New test train starting soon on test !! {Environment.TickCount64}").Add(RawPayload.LinkTerminator).Build();
            Svc.Chat.Print(new() { Message = message });
            var msg = RemoveSymbolsRegex().Replace(message.ToString(), "");
            PluginLog.Debug($"Adding cache entry {msg}");
            NotifyWindow.Cache[msg] = ($"Train starting in Azim Steppe (23.1,23.5)","Endwalker","Sargatanas","Sargatanas","NA","NA","12:00 pm","yedli","invalid",true,true);
        }

        public async void Dispose()
        {
            this.WindowSystem.RemoveAllWindows();
            
            ConfigWindow.Dispose();

            PluginInterface.RemoveChatLinkHandler();

            // Dispose of websocket
            try
            {
                // First, signal the cancellation
                _cancellationTokenSource?.Cancel();

                // Give a moment for the cancellation to propagate
                Task.Delay(500).Wait();

                // Then, dispose of resources
                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                                "Disposing Plugin",
                                                _cancellationTokenSource.Token);
                }


                _webSocket?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                PluginLog.Error($"{ex}");
            }
            
            this.CommandManager.RemoveHandler(CommandName);
        }

        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just display our main ui
            ConfigWindow.IsOpen = true;
        }

        private void DrawUI()
        {
            this.WindowSystem.Draw();
        }

        public void DrawConfigUI()
        {
            ConfigWindow.IsOpen = true;
        }

        [GeneratedRegex("[^a-zA-Z0-9]")]
        private static partial Regex RemoveSymbolsRegex();
    }
}
