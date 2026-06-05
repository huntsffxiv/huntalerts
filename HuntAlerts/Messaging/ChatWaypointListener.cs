using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.DalamudServices;
using ECommons.DalamudServices.Legacy;
using ECommons.Logging;
using HuntAlerts.Helpers;
using System;

namespace HuntAlerts.Messaging;

public static class ChatWaypointListener
{
    public static void Enable()  => Svc.Chat.ChatMessage += OnChatMessage;
    public static void Disable() => Svc.Chat.ChatMessage -= OnChatMessage;

    private static void OnChatMessage(IChatMessage message)
    {
        try
        {
            var config = HuntAlerts.C;
            if (config == null || !config.WorldArrowEnabled || !config.ArrowChatPickup) return;

            if (!IsAllowedChannel(message.Type, config)) return;

            foreach (var payload in message.Message.Payloads)
            {
                if (payload is not MapLinkPayload map) continue;

                var tt = map.TerritoryType.RowId;
                ArrowWaypoint.Set(tt, map.XCoord, map.YCoord, "chat");
                PluginLog.Verbose($"[WorldArrow] picked up chat flag: tt={tt} coords=({map.XCoord:F1},{map.YCoord:F1}) from {message.Type}.");
                break;
            }
        }
        catch (Exception ex)
        {
            PluginLog.Verbose($"Chat waypoint pickup failed: {ex.Message}");
        }
    }

    private static bool IsAllowedChannel(XivChatType type, Configuration config)
    {
        return type switch
        {
            XivChatType.Shout                              => config.ArrowChatPickupShout,
            XivChatType.Yell                               => config.ArrowChatPickupYell,
            XivChatType.Party or XivChatType.CrossParty    => config.ArrowChatPickupParty,
            _                                              => false,
        };
    }
}
