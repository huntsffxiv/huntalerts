using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using ImGuiNET;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using System.Numerics;

namespace HuntAlerts.Windows;
public class NotifyWindow : Window
{
    public Dictionary<string, (string Message, string World, string currentworldName, string currentregionName, string huntregionName)> Cache = new Dictionary<string, (string, string, string, string, string)>();
    public string CurrentPayload = "";


    public NotifyWindow() : base("HuntAlerts notification", ImGuiWindowFlags.None)
    {
        ImGui.SetNextWindowSize(new Vector2(400, 300)); // Set your desired initial width and height here
    }

    public override void Draw()
    {
        if(Cache.TryGetValue(CurrentPayload, out var entry))
        {
            string message = entry.Message;
            string world = entry.World;
            string currentworldName = entry.currentworldName;
            string currentregionName = entry.currentregionName;
            string huntregionname = entry.huntregionName;


            if(currentregionName == huntregionname)
            {
                if (currentworldName != world)
                {
                    if (ImGui.Button($"Teleport to {world}"))
                    {
                        // Code to execute when the button is pressed
                        PluginLog.Verbose($"Attempting to use lifestream to travel to {world}");
                        Svc.Commands.ProcessCommand($"/li {world}");
                    }
                }else
                {
                    string startlocation = ParseForStartLocation(message);
                    if (startlocation != null && startlocation != "invalid")
                    {
                        if (ImGui.Button($"Teleport to start location"))
                        {
                            // Code to execute when the button is pressed
                            PluginLog.Verbose($"Attempting to use teleporter to travel to {startlocation}");
                            Svc.Commands.ProcessCommand($"/tp {startlocation}");
                        }
                    }
                }
            }
            // If you don't set a wrap position, text wraps at the window edge
            ImGui.PushTextWrapPos();
            ImGui.TextUnformatted(message);
            // Pop the text wrap position so it doesn't affect other elements
            ImGui.PopTextWrapPos();


        }
        else
        {
            ImGui.Text($"Could not find requested entry");
        }
    }
    public static string ParseForStartLocation(string message)
    {
        // Define a list of keywords, including multi-word keywords
        var keywords = new List<string> { "fort", "ostall", "great work", "palaka", "yedli", "castrum", "camp broken" };

        // Find which keywords are in the input string
        var foundKeywords = keywords.Where(keyword => message.ToLower().Contains(keyword)).ToList();

        // Prepare the result string
        string result;

        if (foundKeywords.Count == 1)
        {
            result = foundKeywords.First();
        }
        else
        {
            result = "invalid";
        }

        return result;
    }


}
