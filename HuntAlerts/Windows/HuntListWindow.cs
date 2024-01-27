using Dalamud.Interface.Windowing;
using ECommons.Automation;
using ECommons.DalamudServices;
using HuntAlerts.Helpers;
using ImGuiNET;
using System;
using System.Numerics;




namespace HuntAlerts.Windows;
public class HuntListWindow : Window
{
    internal TaskManager TaskManager;
    // ...
    public MessageCacheManager MessageCacheManager;

    public HuntListWindow() : base("HuntAlerts List", ImGuiWindowFlags.None)
    {
        ImGui.SetNextWindowSize(new Vector2(400, 300)); // Set your desired initial width and height here
    }

    public override void Draw()
    {
            // Get the ordered messages (oldest to newest)
            HuntTrainMessage[] orderedMessages = MessageCacheManager.GetOrderedMessages();

            // Reverse the array to get the newest to oldest
            Array.Reverse(orderedMessages);

            // Loop through the array and display each message
            foreach (var message in orderedMessages)
            {
                Svc.Chat.Print(message.Message);
                if (message != null) // Check if the message is not null
                {
                    // Display the message
                    // You can format the message as you like, here's a simple example:
                    ImGui.Text($"{message.Message}"); // Assuming HuntTrainMessage has a meaningful ToString implementation
                
                }
            }

            // Close the ImGui window
            ImGui.End();

    }


}
