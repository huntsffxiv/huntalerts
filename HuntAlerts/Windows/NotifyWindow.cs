using Dalamud.Interface.Windowing;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using HuntAlerts.Helpers;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;



namespace HuntAlerts.Windows;
public class NotifyWindow : Window
{
    public HuntTrainMessage CurrentMessage;

    public NotifyWindow() : base("HuntAlerts Notification", ImGuiWindowFlags.None)
    {
    }

    public override void PreDraw()
    {
        ImGui.SetNextWindowSize(new(400, 200), ImGuiCond.FirstUseEver);
    }

    public override void Draw()
    {
        var entry = CurrentMessage;
        if (entry != null)
        {
            string message = entry.Message;
            string world = entry.huntWorld;
            string currentworldName = entry.currentworldName;
            string currentregionName = entry.currentregionName;
            string huntregionname = entry.huntregionName;
            string startLocation = entry.startLocation;
            uint startLocationAetheryteId = entry.startLocationAetheryteId;
            string startZone = entry.startZone;
            int instance = entry.instance;
            bool lifestreamEnabled = entry.lifestreamEnabled;
            string locationCoords = entry.locationCoords;
            bool openmaponArrival = entry.openmaponArrival;

            if (currentregionName == huntregionname && lifestreamEnabled && startLocationAetheryteId != 0)
            {
                ImGuiEx.RightFloat(() =>
                {
                    if (ImGui.Button($"Teleport to Hunt"))
                    {
                        PluginLog.Verbose($"Attempting to use lifestream teleport");
                        Utilities.ExecuteTeleport(world, startLocation, startLocationAetheryteId, startZone, locationCoords, instance, openmaponArrival, lifestreamEnabled);
                    }
                });
            }

            ImGui.PushTextWrapPos();
            ImGui.TextUnformatted(message);
            ImGui.PopTextWrapPos();

            if (locationCoords != "")
            {
                if (ImGui.Button($"Flag on Map"))
                {
                    Utilities.FlagOnMap(locationCoords, startZone);
                }
            }

            if (ImGui.Button("Open PartyFinder"))
            {
                Utilities.OpenPartyFinder();
            }
        }
        else
        {
            ImGui.Text($"Could not find requested entry");
        }
    }



}
