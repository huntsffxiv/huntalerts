using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Singletons;
using HuntAlerts.Services;
using HuntAlerts.Windows;

namespace HuntAlerts
{
    public sealed class HuntAlerts : IDalamudPlugin
    {
        public string Name => "Hunt Alerts";
        private const string CommandName = "/huntalerts";

        public static Configuration C { get; private set; } = null!;

        public WindowSystem WindowSystem = new("HuntAlerts");

        public HuntAlerts(IDalamudPluginInterface pluginInterface)
        {
            ECommonsMain.Init(pluginInterface, this);
            C = ConfigMigrator.LoadOrMigrate();
            C.Initialize(Svc.PluginInterface);

            Service.ConfigWindow = new ConfigWindow();
            WindowSystem.AddWindow(Service.ConfigWindow);
            Service.NotifyWindow = new();
            WindowSystem.AddWindow(Service.NotifyWindow);
            Service.HuntListWindow = new();
            WindowSystem.AddWindow(Service.HuntListWindow);

            Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens a list of previous hunt alerts"
            });

            Svc.Commands.AddHandler("/huntalerts settings", new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the HuntAlerts options"
            });

            Svc.PluginInterface.UiBuilder.Draw += DrawUI;
            Svc.PluginInterface.UiBuilder.OpenConfigUi += Service.OpenConfig;

            SingletonServiceManager.Initialize(typeof(Service));
            C.Save();
        }

        public void Dispose()
        {
            WindowSystem.RemoveAllWindows();
            Service.ConfigWindow.Dispose();

            Svc.Commands.RemoveHandler(CommandName);
            ECommonsMain.Dispose();
            C = null!;
            Service.ConfigWindow = null!;
            Service.NotifyWindow = null!;
            Service.HuntListWindow = null!;
        }

        private void OnCommand(string command, string args)
        {
            if (args.EqualsIgnoreCaseAny("settings", "s"))
            {
                Service.ConfigWindow.IsOpen = true;
            }
            else if (args.EqualsIgnoreCaseAny("debug", "d"))
            {
                Service.ConfigWindow.OpenDebug();
            }
            else
            {
                Service.HuntListWindow.IsOpen = true;
            }
        }

        private void DrawUI()
        {
            WindowSystem.Draw();
        }
    }
}
