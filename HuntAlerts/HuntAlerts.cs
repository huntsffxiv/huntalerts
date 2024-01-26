using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Logging;
using HuntAlerts.Windows;
using System;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace HuntAlerts
{
    public sealed partial class HuntAlerts : IDalamudPlugin
    {

        public string Name => "Hunt Alerts";
        private const string CommandName = "/huntalerts";
        
        public WindowSystem WindowSystem = new("HuntAlerts");

        private ConfigWindow ConfigWindow { get; init; }
        

        DalamudLinkPayload LinkPayload;
        NotifyWindow NotifyWindow;

        public HuntAlerts(
            DalamudPluginInterface pluginInterface
        )
        {
            ECommonsMain.Init(pluginInterface, this);
            this.Configuration = Svc.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(Svc.PluginInterface);


            ConfigWindow = new ConfigWindow(this);
            WindowSystem.AddWindow(ConfigWindow);
            NotifyWindow = new();
            WindowSystem.AddWindow(NotifyWindow);

            Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the HuntAlerts options"
            });

            Svc.PluginInterface.UiBuilder.Draw += DrawUI;
            Svc.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            InitializeWebSocket();

            LinkPayload = Svc.PluginInterface.AddChatLinkHandler(0, (id, s) =>
            {
                var msg = RemoveSymbolsRegex().Replace(s.ToString(), "");
                NotifyWindow.IsOpen = true;
                NotifyWindow.CurrentPayload = msg;
                PluginLog.Debug($"Opening window for message {msg}");
            });
        }
        public async void Dispose()
        {
            this.WindowSystem.RemoveAllWindows();

            ConfigWindow.Dispose();

            Svc.PluginInterface.RemoveChatLinkHandler();

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

            Svc.Commands.RemoveHandler(CommandName);
            ECommonsMain.Dispose();
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
    }
}
