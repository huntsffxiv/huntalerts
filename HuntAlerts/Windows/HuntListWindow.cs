using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ECommons.Automation;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using HuntAlerts.Helpers;
using ImGuiNET;
using System;
using System.Numerics;
using System.Windows.Forms;
using System.Collections.Generic;




namespace HuntAlerts.Windows;
public class HuntListWindow : Window
{
    internal TaskManager TaskManager;
    // ...

    public HuntListWindow() : base("HuntAlerts List", ImGuiWindowFlags.None)
    {
        ImGui.SetNextWindowSize(new Vector2(400, 300)); // Set your desired initial width and height here
    }

    public override void Draw()
    {
        try
        {
            // Get the ordered messages (oldest to newest)
            List<HuntTrainMessage> orderedMessages = HuntAlerts.P.MessageCacheManager.GetOrderedMessages();

            // Reverse the array to get the newest to oldest
            orderedMessages.Reverse();
            if (orderedMessages.Count > 0)
            {
                foreach (var message in orderedMessages)
                {
                    if (message != null)
                    {
                        string huntType = message.huntType ?? "Unknown Type";
                        string huntKind = message.huntKind ?? "Unknown Kind";
                        string huntWorld = message.huntWorld ?? "Unknown World";
                        string localPostedTime = message.Posted_Time ?? "Unknown Time";

                        if (huntType == "new_hunt")
                        {
                            huntType = "Hunt Train";
                        }
                        else if (huntType == "srank")
                        {
                            huntType = "S Rank";
                        }

                        if (huntKind == "EW") { huntKind = "Endwalker"; }
                        else if (huntKind == "SHB") { huntKind = "Shadowbringers"; }
                        else if (huntKind == "SB" || huntKind == "HW" || huntKind == "ARR") { huntKind = "Centurio"; }
                        




                        if (ImGui.TreeNode($"{localPostedTime} - {huntType} - {huntKind} - {huntWorld}"))
                        {
                            // Display other message details here
                            ImGui.Text($"{message.Message}");
                            ImGui.TreePop();
                        }
                    }
                }
            }else
            {
                ImGui.Text($"There are no cached messages.");
            }

        }
        catch(Exception ex)
        {
            PluginLog.Error("Error while displaying the huntalert list");
            PluginLog.Error(ex.Message);
            return;
        }

    }


}
