using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using HuntAlert.Windows;
using System.Net.WebSockets;
using System.Threading;
using System;
using Newtonsoft.Json;
using System.Text;
using System.Collections.Generic;
using static HuntAlert.Plugin;
using FFXIVClientStructs.FFXIV.Client.System;
using Dalamud.Logging;
using System.Threading.Tasks;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Ipc.Exceptions;
using System.Text.RegularExpressions;

namespace HuntAlert
{
    public sealed partial class Plugin : IDalamudPlugin
    {
        public string Name => "Hunt Alert";
        private const string CommandName = "/huntalert";
        private ClientWebSocket _webSocket;
        private IChatGui _chatGui;
        private CancellationTokenSource _cancellationTokenSource;


        [PluginService] private static IChatGui ChatGui { get; set; }  // Use Dalamud's IoC container to get the IChatGui service

        private DalamudPluginInterface PluginInterface { get; init; }
        private ICommandManager CommandManager { get; init; }
        private IDataManager Data { get; init; }
        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("HuntAlert");

        private ConfigWindow ConfigWindow { get; init; }

        DalamudLinkPayload LinkPayload;
        NotifyWindow NotifyWindow;

        public Plugin(
            DalamudPluginInterface pluginInterface,
            ICommandManager commandManager, IDataManager data)
        {
            this.PluginInterface = pluginInterface;
            this.CommandManager = commandManager;
            this.Data = data;

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

        private async void InitializeWebSocket()
        {
            _webSocket = new ClientWebSocket();
            try
            {
                _cancellationTokenSource = new CancellationTokenSource();
                // Connect to WebSocket
                await _webSocket.ConnectAsync(new Uri("ws://huntrelay.eastus.cloudapp.azure.com:6789"), CancellationToken.None);
                PluginLog.Information("Connected to WebSocket.");
                // Start listening for messages
                StartReceiving(_cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                PluginLog.Error($"WebSocket connection error: {ex}");
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
                    await Task.Delay(retryInterval); // Wait before reconnecting
                    _webSocket.Dispose(); // Dispose the old instance
                    _webSocket = new ClientWebSocket(); // Create a new instance
                    await _webSocket.ConnectAsync(new Uri("ws://huntrelay.eastus.cloudapp.azure.com:6789"), CancellationToken.None);
                    PluginLog.Information("Reconnected to WebSocket.");
                    StartReceiving(_cancellationTokenSource.Token); // Start listening for messages again
                }
                catch (Exception ex)
                {
                    PluginLog.Error($"WebSocket reconnection error: {ex}");
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
            public long PostedEpoch { get; set; }
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
                _ => false,
            };
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
                // Add mappings for all worlds in their respective data centers
            };


            try
            {
                var buffer = new byte[2048];
                while (_webSocket.State == WebSocketState.Open)
                {
                    if (_webSocket.CloseStatus.HasValue || cancellationToken.IsCancellationRequested)
                    {
                        // Clean up or close connections here
                        PluginLog.Information("Cancellation requested, stopping WebSocket listener.");
                        return;
                    }

                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    // Process received message
                    var messageString = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    try
                    {
                        var huntMessage = JsonConvert.DeserializeObject<HuntMessage>(messageString);


                        // Create a dictionary mapping hunt types to their corresponding configuration flags
                        var huntTypeConfigMap = new Dictionary<string, bool>
                        {
                            { "Shadowbringers", this.Configuration.ShadowbringersHunts },
                            { "Centurio", this.Configuration.CenturioHunts },
                            { "Endwalker", this.Configuration.EndwalkerHunts },
                            // Add more mappings as necessary
                        };


                        foreach (var huntType in huntTypeConfigMap.Keys)
                        {
                            var isCorrectHuntType = huntMessage.Kind.Contains(huntType) && huntTypeConfigMap[huntType];
                            var isDataCenterEnabled = worldDataCenterMap.TryGetValue(huntMessage.World, out var dataCenter) &&
                                                       IsDataCenterEnabled(dataCenter);

                            if (isCorrectHuntType && isDataCenterEnabled)
                            {
                                PluginLog.Debug($"EndwalkerHunts setting: {this.Configuration.EndwalkerHunts}");
                                PluginLog.Debug($"ShadowbringersHunts setting: {this.Configuration.ShadowbringersHunts}");
                                PluginLog.Debug($"CenturioHunts setting: {this.Configuration.CenturioHunts}");

                                // Code to handle the hunt
                                // Since the handling code is the same for all hunts, place it here
                                var message = new SeStringBuilder().Add(LinkPayload).AddText("New " + huntMessage.Kind + " train starting soon on " + huntMessage.World + "!!").Add(RawPayload.LinkTerminator).Build();
                                ChatGui.Print(new() { Message = message });
                                var msg = RemoveSymbolsRegex().Replace(message.ToString(), "");
                                PluginLog.Debug($"Adding cache entry {msg}");
                                NotifyWindow.Cache[msg] = huntMessage.Content;

                                // Break out of the loop once a matching hunt type is found and handled
                                break;
                            }
                        }


                    }
                    catch (JsonException ex)
                    {
                        // Handle JSON parsing error
                        ChatGui.Print("Plugin had issues parsing json");
                        PluginLog.Warning($"{ex}");
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

        public void Dispose()
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
