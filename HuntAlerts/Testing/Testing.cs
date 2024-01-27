using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.Logging;
using HuntAlerts.Windows;
using System;
using System.Drawing.Text;

namespace HuntAlerts
{
    public sealed partial class HuntAlerts
    {
        public void Test()
        {

            /*var message = new SeStringBuilder().Add(LinkPayload).AddText($"New test train starting soon on test !! {Environment.TickCount64}").Add(RawPayload.LinkTerminator).Build();
            Svc.Chat.Print(new() { Message = message });
            var msg = RemoveSymbolsRegex().Replace(message.ToString(), "");
            PluginLog.Debug($"Adding cache entry {msg}");
            NotifyWindow.Cache[msg] = ($"Train starting in Azim Steppe (23.1,23.5)", "Endwalker", "Sargatanas", "Sargatanas", "NA", "NA", "12:00 pm", "yedli", "invalid", "23.1, 23.5", true, true, true);*/
        }
    }
}
