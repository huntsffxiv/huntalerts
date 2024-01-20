using Dalamud.Interface.Windowing;
using ECommons.Logging;
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
using System.Threading;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Dalamud.Interface.Utility;
using ECommons.ImGuiMethods;
using ECommons.Automation;
using ECommons;
using Lumina.Excel.GeneratedSheets;
using ECommons.ExcelServices;

namespace HuntAlerts.Windows;
public class NotifyWindow : Window
{
    public Dictionary<string, (string Message, string huntKind, string huntWorld, string currentworldName, string currentregionName, string huntregionName, string Posted_Time,string startLocation, string startZone,string locationCoords, bool teleporterEnabled,bool lifestreamEnabled)> Cache = new Dictionary<string, (string, string, string, string, string, string, string, string, string, string, bool, bool)>();
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

            
            
            if (currentregionName == huntregionname)
            {

                if (currentworldName != world)
                {
                    if (lifestreamEnabled)
                    {
                        
                        ImGuiEx.RightFloat(() =>
                        {
                            if (ImGui.Button($"Teleport to Hunt"))
                            {
                                // Code to execute when the button is pressed
                                PluginLog.Verbose($"Attempting to use lifestream to travel to {world}");
                                //Svc.Commands.ProcessCommand($"/li {world}");
                                ExecuteCommandWithLoop(world, startLocation, startZone, teleporterEnabled, lifestreamEnabled);
                            }
                        });
                    }
                }
                else
                {
                    if (teleporterEnabled)
                    {
                        if ((startLocation != null && startLocation != "invalid") || (startZone != null && startZone != "invalid"))
                        {
                            ImGuiEx.RightFloat(() =>
                            {
                                if (ImGui.Button($"Teleport to Hunt"))
                                {
                                    // Code to execute when the button is pressed
                                    if (startLocation != "invalid")
                                    {
                                        PluginLog.Verbose($"Attempting to use teleporter to travel to {startLocation}");
                                        Svc.Commands.ProcessCommand($"/tp {startLocation}");
                                    }
                                    else if (startZone != "invalid")
                                    {
                                        PluginLog.Verbose($"Attempting to use teleporter to travel to {startZone}");
                                        Svc.Commands.ProcessCommand($"/tpm {startZone}");
                                        Svc.Chat.Print("Couldn't determine exact starting point so taking you to the zone instead");
                                    }
                                }
                            });
                        }
                    }
                }
            }
            if (locationCoords != "")
            {
                    if (ImGui.Button($"Flag on Map"))
                    {
                        // Code to execute when the button is pressed
                        FlagOnMap(locationCoords, startZone);
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


    private CancellationTokenSource _cancellationTokenSource;
    private bool _isTaskRunning = false;
    private async void ExecuteCommandWithLoop(string world, string startLocation, string startZone, bool teleporterEnabled, bool lifestreamEnabled)
    {
        if (_isTaskRunning)
        {
            _cancellationTokenSource.Cancel(); // Cancel the current task if running
            _isTaskRunning = false;
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;
        _isTaskRunning = true;

        try
        {
            
            string currentworldName = "";
            currentworldName = Svc.ClientState.LocalPlayer.CurrentWorld.GameData.Name;

            if (lifestreamEnabled && currentworldName != world)
            {
                if (currentworldName != world)
                {
                    // Execute initial command
                    Svc.Commands.ProcessCommand($"/li {world}");
                }
            }else
            {
                if (currentworldName != world)
                {
                    Svc.Chat.Print("Can't teleport to hunt world without the Lifestream plugin being enabled as you are off world.");
                    return;
                }
            }

            if (teleporterEnabled)
            {
                // Start loop
                var startTime = DateTime.Now;
                while (!token.IsCancellationRequested && (DateTime.Now - startTime).TotalSeconds <= 240)
                {

                    // Check character's current world and logged in status here
                    // if condition met, break loop and run another command
                    if (Svc.ClientState.IsLoggedIn && Svc.ClientState.LocalPlayer != null)
                    {
                        currentworldName = Svc.ClientState.LocalPlayer.CurrentWorld.GameData.Name;
                        PluginLog.Verbose($"Player is logged in. Currentworld: " + currentworldName);
                        
                        if (currentworldName == world)
                        {
                            var targetableStartTime = DateTime.Now;
                            // Loop until the player is targetable or until canceled
                            while (!token.IsCancellationRequested && (DateTime.Now - targetableStartTime).TotalSeconds <= 60) // Inner loop timeout (e.g., 60 seconds)
                            {
                                if (Svc.ClientState.LocalPlayer?.IsTargetable == true)
                                {
                                    // Player is targetable, execute the command
                                    // Code to execute when the button is pressed
                                    if (startLocation != "invalid")
                                    {
                                        PluginLog.Verbose($"Player is on hunt world, starting teleport to hunt location. Currentworld: " + currentworldName + "StartLocation: " + startLocation);
                                        Svc.Commands.ProcessCommand($"/tp {startLocation}");
                                        return;
                                    }
                                    else if (startZone != "invalid")
                                    {
                                        PluginLog.Verbose($"Player is on hunt world, starting teleport to hunt location. Currentworld: " + currentworldName + "StartZone: "+ startZone);
                                        Svc.Commands.ProcessCommand($"/tpm {startZone}");
                                        return;
                                    }
                                    
                                }

                                // Wait a bit before checking again
                                await Task.Delay(1000, token); // Check every second, for example
                            }  
                        }

                    }
                    else
                    {
                        PluginLog.Verbose($"Player is still transfering");
                    }



                    await Task.Delay(5000, token); // Wait for 5 seconds
                }
            }
        }
        catch (TaskCanceledException)
        {
            // Handle cancellation
        }
        finally
        {
            _isTaskRunning = false;
        }

        // Additional code to execute after loop ends
    }

    private void FlagOnMap(string locationCoords,string startZone)
    {
        // Code to execute when the button is pressed
        PluginLog.Verbose($"Attempting to flag coords {startZone} {locationCoords} on Map");
        uint tt;
        var (x, y) = (locationCoords.Split(',').Select(s => float.Parse(s.Trim())).ToArray() is float[] coords) ? (coords[0], coords[1]) : (0f, 0f);

        if (Svc.Data.GetExcelSheet<TerritoryType>().TryGetFirst(x => x.TerritoryIntendedUse == (uint)TerritoryIntendedUseEnum.Open_World && (x.PlaceName.Value?.Name.ExtractText() ?? "").EqualsIgnoreCase(startZone), out var value))
        {
            tt = value.RowId; //is territory id
            MapManager.OpenMapWithMarker(tt, x, y);
        }
    }


}
