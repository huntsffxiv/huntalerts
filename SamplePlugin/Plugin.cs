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

namespace HuntAlerts
{
    public sealed partial class Plugin : IDalamudPlugin
    {
        public string Name => "Hunt Alert";
        private const string CommandName = "/huntalerts";
        private ClientWebSocket _webSocket;
        private IChatGui _chatGui;
        private CancellationTokenSource _cancellationTokenSource;

        //public string serverURI = "ws://huntrelay.eastus.cloudapp.azure.com:6789";
        public string serverURI = "ws://localhost:6789";


        [PluginService] private static IChatGui ChatGui { get; set; }  // Use Dalamud's IoC container to get the IChatGui service

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



            ConfigWindow = new ConfigWindow(this);
            
            WindowSystem.AddWindow(ConfigWindow);
            NotifyWindow = new();
            WindowSystem.AddWindow(NotifyWindow);

            this.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "A useful message to display in /xlhelp"
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


        private static string ReplaceTimestampsWithLocalTime(string input)
        {
            // Regex pattern to find timestamps
            string pattern = @"<t:(\d+):R>";

            // Replace each match in the input string
            return Regex.Replace(input, pattern, match =>
            {
                // Extract the Unix timestamp from the match
                long unixTimestamp = long.Parse(match.Groups[1].Value);

                // Convert Unix timestamp to DateTime
                DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).ToLocalTime().DateTime;

                // Format the DateTime as needed, e.g., "MM/dd/yyyy HH:mm:ss"
                return dateTime.ToString("g"); // or any other format
            });
        }

        private static string RemoveDiscordEmojis(string input)
        {
            // Regex pattern to find Discord emojis
            string emojiPattern = @"<a?:(\w+):\d+>";

            // Replace each match (Discord emoji) with an empty string
            return Regex.Replace(input, emojiPattern, "");
        }

        private static string convertTime(long epochTime)
        {

            // Convert Unix timestamp to DateTime
            DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(epochTime).ToLocalTime().DateTime;

            // Format the DateTime as needed, e.g., "MM/dd/yyyy HH:mm:ss"
            PluginLog.Verbose($"Posted Time:  {epochTime}");
            string convertedTime = dateTime.ToString("hh:mm tt"); // or any other format
            PluginLog.Verbose($"converted time: {convertedTime}");
            return convertedTime;
        }

        private async void StartReceiving(CancellationToken cancellationToken)
        {

            var worldDataCenterMap = new Dictionary<string, string>
            {
                // Aether
                { "Adamantoise", "Aether" },
                { "Cactuar", "Aether" },
                { "Faerie", "Aether" },
                { "Gilgamesh", "Aether" },
                { "Jenova", "Aether" },
                { "Midgardsormr", "Aether" },
                { "Sargatanas", "Aether" },
                { "Siren", "Aether" },

                // Crystal
                { "Balmung", "Crystal" },
                { "Brynhildr", "Crystal" },
                { "Coeurl", "Crystal" },
                { "Diabolos", "Crystal" },
                { "Goblin", "Crystal" },
                { "Malboro", "Crystal" },
                { "Mateus", "Crystal" },
                { "Zalera", "Crystal" },

                // Primal
                { "Behemoth", "Primal" },
                { "Excalibur", "Primal" },
                { "Exodus", "Primal" },
                { "Famfrit", "Primal" },
                { "Hyperion", "Primal" },
                { "Lamia", "Primal" },
                { "Leviathan", "Primal" },
                { "Ultros", "Primal" },

                // Dynamis
                { "Halicarnassus", "Dynamis" },
                { "Maduin", "Dynamis" },
                { "Marilith", "Dynamis" },
                { "Seraph", "Dynamis" },

                // Light
                { "Cerberus", "Chaos" },
                { "Louisoix", "Chaos" },
                { "Moogle", "Chaos" },
                { "Omega", "Chaos" },
                { "Phantom", "Chaos" },
                { "Ragnarok", "Chaos" },
                { "Sagittarius", "Chaos" },
                { "Spriggan", "Chaos" },

                // Chaos
                { "Alpha", "Light" },
                { "Lich", "Light" },
                { "Odin", "Light" },
                { "Phoenix", "Light" },
                { "Raiden", "Light" },
                { "Shiva", "Light" },
                { "Twintania", "Light" },
                { "Zodiark", "Light" },

                // Add mappings for all worlds in their respective data centers
            };


            try
            {
                
                var buffer = new byte[2048];
                while (_webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {

                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                    // Process received message
                    var messageString = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    try
                    {
                        
                        var huntMessage = JsonConvert.DeserializeObject<HuntMessage>(messageString);

                        PluginLog.Verbose($"New train data received: Kind:" + huntMessage.Kind + " | World:" + huntMessage.World);

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

                        // Create a dictionary mapping hunt types to their corresponding configuration flags
                        var huntTypeConfigMap = new Dictionary<string, bool>
                        {
                            { "Shadowbringers", this.Configuration.ShadowbringersHunts },
                            { "Centurio", this.Configuration.CenturioHunts },
                            { "Endwalker", this.Configuration.EndwalkerHunts },
                            // Add more mappings as necessary
                        };

                        bool logPrinted = false;
                        foreach (var huntType in huntTypeConfigMap.Keys)
                        {
                            var isCorrectHuntType = huntMessage.Kind.Contains(huntType) && huntTypeConfigMap[huntType];
                            var isDataCenterEnabled = worldDataCenterMap.TryGetValue(huntMessage.World, out var dataCenter) &&
                                                      IsDataCenterEnabled(dataCenter);

                            bool isWorldMatch = true;
                            if (currentworldOnly && huntMessage.World != currentworldName)
                            {
                                isWorldMatch = false; // Hunt is not in the player's current world
                                if (!logPrinted)
                                {
                                    PluginLog.Verbose("Current World Only option is enabled and player is not on the hunt world currently, supressing notification");
                                    logPrinted = true;
                                }
                                
                            }
                            if (homeworldOnly && huntMessage.World != homeworldName)
                            {
                                isWorldMatch = false; // Hunt is not in the player's home world
                                if (!logPrinted)
                                {
                                    PluginLog.Verbose("Home World Only option is enabled and hunt is not for player's home world, supressing notification");
                                    logPrinted = true;
                                }
                            }

                            // Ensure the data center is still checked if neither world-specific setting is enabled
                            bool isDataCenterCheckRequired = !currentworldOnly && !homeworldOnly;
                            if (isDataCenterCheckRequired && !isDataCenterEnabled)
                            {
                                isWorldMatch = false; // Data center is not enabled
                                if (!logPrinted)
                                {
                                    PluginLog.Verbose("Datacenter that hunt is for is not enabled in settings, supressing notification");
                                    logPrinted = true;
                                }
                            }

                            if (isCorrectHuntType && isWorldMatch)
                            {
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
                                messageContent = "Hunt: " + huntMessage.Kind + Environment.NewLine + "World: " + huntMessage.World + Environment.NewLine + "Posted: "+ convertTime(huntMessage.Posted_Epoch) + Environment.NewLine + Environment.NewLine + messageContent;

                                // Wordwrap really long lines
                                int maxlineLength = this.Configuration.MaxLineLength;
                                messageContent = WordWrap(messageContent,maxlineLength);

                                // Code to handle the hunt
                                // Since the handling code is the same for all hunts, place it here
                                var message = new SeStringBuilder().Add(LinkPayload).AddText("New " + huntMessage.Kind + " train starting soon on " + huntMessage.World + "!!").Add(RawPayload.LinkTerminator).Build();
                                ChatGui.Print(new() { Message = message });
                                var msg = RemoveSymbolsRegex().Replace(message.ToString(), "");
                                PluginLog.Debug($"Adding cache entry {msg}");
                                NotifyWindow.Cache[msg] = messageContent;

                                // Play sound effect if one is set
                                if (this.Configuration.soundEffect != 0)
                                {
                                    UIModule.PlayChatSoundEffect((uint)this.Configuration.soundEffect); // Play the selected sound effect
                                }

                                // Break out of the loop once a matching hunt type is found and handled
                                break;
                            }
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
                    //PluginLog.Error($"WebSocket connection error: {ex}");
                    // If cancellation hasn't been requested, this is an actual error.
                    PluginLog.Warning($"Lost connection to the server");
                    // Start reconnection logic.
                    await ReconnectWebSocket();
                }
            }
        }

        public void Test()
        {

            var message = new SeStringBuilder().Add(LinkPayload).AddText($"New test train starting soon on test !! {Environment.TickCount64}").Add(RawPayload.LinkTerminator).Build();
            ChatGui.Print(new() { Message = message });
            var msg = RemoveSymbolsRegex().Replace(message.ToString(), "");
            PluginLog.Debug($"Adding cache entry {msg}");
            NotifyWindow.Cache[msg] = $"Content {Environment.TickCount64}";
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
