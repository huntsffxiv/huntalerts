using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.DalamudServices;
using ECommons.Logging;
using HuntAlerts.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace HuntAlerts.Services;

#pragma warning disable IDE1006, IDE0040, IDE0044
public class MessageCacheManager : IDisposable
{
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private const int VK_CONTROL = 0x11;
    private const int Capacity   = 50;

    private readonly DalamudLinkPayload[] PayloadList = new DalamudLinkPayload[Capacity];
    private readonly HuntTrainMessage?[]  Messages    = new HuntTrainMessage?[Capacity];
    private int CommandCount = 0;

    public MessageCacheManager()
    {
        for (var i = 0u; i < Capacity; i++)
        {
            PayloadList[i] = Svc.Chat.AddChatLinkHandler(i, ProcessLinkPayload);
        }
        LoadFromDisk();
    }

    private void LoadFromDisk()
    {
        var saved = HistoryStore.TryLoad();
        if (saved == null) return;

        var slots = saved.Slots;
        var copy  = Math.Min(slots.Length, Capacity);
        for (var i = 0; i < copy; i++)
        {
            Messages[i] = slots[i];
        }

        CommandCount = saved.Capacity == Capacity ? saved.CommandCount : copy;
        PluginLog.Verbose($"HistoryStore: restored {Messages.Count(m => m != null)} events; CommandCount={CommandCount}.");
    }

    private void SaveToDisk()
    {
        HistoryStore.Save(Capacity, CommandCount, Messages);
    }

    public DalamudLinkPayload AddMessage(HuntTrainMessage message)
    {
        var nextCommand = CommandCount % Capacity;
        CommandCount++;
        Messages[nextCommand] = message;
        SaveToDisk();
        return PayloadList[nextCommand];
    }

    void ProcessLinkPayload(uint cmd, SeString str)
    {
        if (cmd >= Capacity || Messages[cmd] == null) return;

        var lifestreamInstalled = Svc.PluginInterface.InstalledPlugins.FirstOrDefault(x => x.InternalName == "Lifestream")?.IsLoaded == true;
        var ctrlHeld = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
        var ctrlClickTeleport = HuntAlerts.C.ctrlclickTeleport
                             && lifestreamInstalled
                             && HuntAlerts.C.LifestreamIntegration;

        var msg = Messages[cmd]!;

        if (ctrlHeld && ctrlClickTeleport)
        {
            PluginLog.Verbose("Ctrl-click teleport from chat link.");
            Utilities.ExecuteTeleport(
                msg.huntWorld, msg.startLocation, msg.startLocationAetheryteId,
                msg.startZone, msg.locationCoords, msg.instance,
                msg.openmaponArrival, msg.lifestreamEnabled);
        }
        else
        {
            Service.NotifyWindow.IsOpen        = true;
            Service.NotifyWindow.CurrentMessage = msg;
        }
    }

    public List<HuntTrainMessage> GetOrderedMessages()
    {
        var ordered = new List<HuntTrainMessage>();
        var stored  = Math.Min(CommandCount, Capacity);

        for (var i = 1; i <= stored; i++)
        {
            var idx = (CommandCount - i) % Capacity;
            if (idx < 0) idx += Capacity;
            var msg = Messages[idx];
            if (msg != null) ordered.Add(msg);
        }
        ordered.Reverse();
        return ordered;
    }

    public void Dispose()
    {
        Svc.Chat.RemoveChatLinkHandler();
    }
}
