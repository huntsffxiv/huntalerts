using ECommons.Automation.NeoTaskManager;
using HuntAlerts.Messaging;
using HuntAlerts.Windows;

namespace HuntAlerts.Services;

public static class Service
{
    public static IPCManager IPCManager { get; private set; } = null!;
    public static MessageCacheManager MessageCacheManager { get; private set; } = null!;
    public static HuntSocketConnection HuntSocketConnection { get; private set; } = null!;
    public static SnoozeManager Snooze { get; private set; } = null!;
    public static TaskManager TaskManager { get; private set; } = null!;
    public static ConfigWindow ConfigWindow { get; internal set; } = null!;
    public static NotifyWindow NotifyWindow { get; internal set; } = null!;
    public static HuntListWindow HuntListWindow { get; internal set; } = null!;

    public static void OpenConfig() => ConfigWindow.IsOpen = true;
}
