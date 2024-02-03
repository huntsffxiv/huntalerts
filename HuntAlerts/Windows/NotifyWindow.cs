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
using ImGuiNET;
using Lumina.Excel.GeneratedSheets;
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
        ImGui.SetNextWindowSize(new Vector2(400, 300)); // Set your desired initial width and height here
    }

    public override void Draw()
    {
        var entry = CurrentMessage;
        if(entry != null) 
        { 
            string message = entry.Message;
            string world = entry.huntWorld;
            string currentworldName = entry.currentworldName;
            string currentregionName = entry.currentregionName;
            string huntregionname = entry.huntregionName;
            string startLocation = entry.startLocation;
            string startZone = entry.startZone;
            string huntType = entry.huntKind;
            string postedTime = entry.Posted_Time;
            bool teleporterEnabled = entry.teleporterEnabled;
            bool lifestreamEnabled = entry.lifestreamEnabled;
            string locationCoords = entry.locationCoords;
            bool openmaponArrival = entry.openmaponArrival;

            
            
            if (currentregionName == huntregionname)
            {

                if ((lifestreamEnabled && (currentworldName != world)) || (teleporterEnabled && (currentworldName == world)))
                {
                        
                    ImGuiEx.RightFloat(() =>
                    {
                        if (ImGui.Button($"Teleport to Hunt"))
                        {
                            // Code to execute when the button is pressed
                            PluginLog.Verbose($"Attempting to use teleport/lifestream");
                            //Svc.Commands.ProcessCommand($"/li {world}");
                            Utilities.ExecuteTeleport(world, startLocation, startZone, locationCoords, openmaponArrival, teleporterEnabled, lifestreamEnabled);
                        }
                    });
                }
            }



            // If you don't set a wrap position, text wraps at the window edge
            ImGui.PushTextWrapPos();
            ImGui.TextUnformatted(message);
            // Pop the text wrap position so it doesn't affect other elements
            ImGui.PopTextWrapPos();

            if (locationCoords != "")
            {
                if (ImGui.Button($"Flag on Map"))
                {
                    // Code to execute when the button is pressed
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
