using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Logging;
using HuntAlerts.Helpers;
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
        
        public NotifyWindow NotifyWindow;

        public HuntListWindow HuntListWindow;

        public static HuntAlerts P; //have a static instance accessible from anywhere
        public MessageCacheManager MessageCacheManager;

        public HuntAlerts(
            DalamudPluginInterface pluginInterface
        )
        {
            P = this;
            ECommonsMain.Init(pluginInterface, this);
            this.Configuration = Svc.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(Svc.PluginInterface);


            ConfigWindow = new ConfigWindow(this);
            WindowSystem.AddWindow(ConfigWindow);
            NotifyWindow = new();
            WindowSystem.AddWindow(NotifyWindow);
            HuntListWindow = new();
            WindowSystem.AddWindow(HuntListWindow);

            Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens a list of previous hunt alerts"
            });

            Svc.Commands.AddHandler("/huntalerts settings", new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the HuntAlerts options"
            });





            Svc.PluginInterface.UiBuilder.Draw += DrawUI;
            Svc.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            InitializeWebSocket();
            MessageCacheManager = new();
        }
        public async void Dispose()
        {
            // Dispose of websocket
            try
            {

                // Close the WebSocket connection synchronously
                CloseWebSocketAsync().GetAwaiter().GetResult();

                // First, signal the cancellation
                _cancellationTokenSource?.Cancel();


                _webSocket?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                PluginLog.Error($"{ex}");
            }

            this.WindowSystem.RemoveAllWindows();

            ConfigWindow.Dispose();

            Svc.PluginInterface.RemoveChatLinkHandler();

            Svc.Commands.RemoveHandler(CommandName);
            MessageCacheManager.Dispose();
            ECommonsMain.Dispose();
            P = null; 
        }
        private void OnCommand(string command, string args)
        {
            if(args.EqualsIgnoreCaseAny("settings", "s"))
            {
                ConfigWindow.IsOpen = true;
            }
            else
            {
                HuntListWindow.IsOpen = true;
                //DisplayMessagesNewestToOldest();
            }
            // in response to the slash command, just display our main ui
            
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
