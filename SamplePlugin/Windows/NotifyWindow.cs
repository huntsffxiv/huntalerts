using Dalamud.Interface.Windowing;
using ImGuiNET;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HuntAlert.Windows;
public class NotifyWindow : Window
{
    public Dictionary<string, string> Cache = [];
    public string CurrentPayload = "";
    public NotifyWindow() : base("HuntAlert notification", ImGuiWindowFlags.AlwaysAutoResize)
    {
    }

    public override void Draw()
    {
        if(Cache.TryGetValue(CurrentPayload, out var message))
        {
            ImGui.TextUnformatted(message);
        }
        else
        {
            ImGui.Text($"Could not find requested entry");
        }
    }
}
