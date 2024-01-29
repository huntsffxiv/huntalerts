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
    internal TaskManager TaskManager;
    // ...

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
                            ExecuteCommandWithLoop(world, startLocation, startZone, locationCoords, openmaponArrival, teleporterEnabled, lifestreamEnabled);
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
                    FlagOnMap(locationCoords, startZone);
                }
            }

            if (ImGui.Button("Open PartyFinder"))
            {
                OpenPartyFinder();
            }

        }
        else
        {
            ImGui.Text($"Could not find requested entry");
        }
    }


    private CancellationTokenSource _cancellationTokenSource;
    private bool _isTaskRunning = false;
    private async void ExecuteCommandWithLoop(string world, string startLocation, string startZone, string locationCoords, bool openmaponArrival, bool teleporterEnabled, bool lifestreamEnabled)
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
            bool hastoserverTransfer = false;
            string currentworldName = "";
            currentworldName = Svc.ClientState.LocalPlayer.CurrentWorld.GameData.Name;


            if (lifestreamEnabled && currentworldName != world)
            {
                if (currentworldName != world)
                {
                    hastoserverTransfer = true;
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
                while (!token.IsCancellationRequested && (DateTime.Now - startTime).TotalSeconds <= 720)
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
                                    if (startLocation != "invalid" && startLocation != "")
                                    {
                                        PluginLog.Verbose($"Player is on hunt world, starting teleport to hunt location. Currentworld: " + currentworldName + "StartLocation: " + startLocation);
                                        if (hastoserverTransfer == true)
                                        {
                                            await Task.Delay(2000, token); // wait 2 seconds to start teleport
                                        }

                                        // Check and replace start location if a city is passed in
                                        if (startLocation.ToLower().Contains("limsa")) { startLocation = "Limsa"; }
                                        if (startLocation.ToLower().Contains("gridania")) { startLocation = "gridania"; }
                                        if(startLocation.ToLower().Contains("ul'dah") || startLocation.ToLower().Contains("uldah")) { startLocation = "ul'dah"; }

                                        Svc.Commands.ProcessCommand($"/tp {startLocation}");

                                        if (openmaponArrival == true && locationCoords != "")
                                        {
                                            PluginLog.Verbose("Open map on arrival is enabled and coords exist");
                                            var flagtargetableStartTime = DateTime.Now;
                                            while (!token.IsCancellationRequested && (DateTime.Now - flagtargetableStartTime).TotalSeconds <= 60) // Inner loop timeout (e.g., 60 seconds)
                                            {

                                                var territoryType = Svc.ClientState.TerritoryType;
                                                var territoryName = Svc.Data.GetExcelSheet<TerritoryType>()
                                                                     .GetRow(territoryType)?.PlaceName.Value?.Name.ToString();

                                                PluginLog.Verbose($"In Loop waiting on targetable and location match. Current Zone: {territoryName} | Destination Zone: {startZone}");

                                                if ((Svc.ClientState.LocalPlayer?.IsTargetable == true) && (territoryName == startZone))
                                                {
                                                    await Task.Delay(500, token);
                                                    PluginLog.Verbose($"Opening map and flagging coordinates");
                                                    FlagOnMap(locationCoords, startZone);
                                                    return;
                                                }
                                                await Task.Delay(1000, token); // wait 2 seconds to start teleport
                                            }
                                        }else
                                        {
                                            return;
                                        }
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
        try
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
        }catch(Exception ex)
        {
            PluginLog.Error("Error placing flag on map.");
            PluginLog.Error(ex.ToString());
        }
    }

    private unsafe void  OpenPartyFinder()
    {

        try
        {
            if (EzThrottler.Throttle("OpenHuntPF", 1000))
            {
                TaskManager = new() { AbortOnTimeout = true, TimeLimitMS = 5000 };

                if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("LookingForGroup", out var _))
                {
                    this.TaskManager.Enqueue(() => Chat.Instance.SendMessage("/partyfinder"));
                    this.TaskManager.DelayNext(500);
                }
                this.TaskManager.Enqueue(() =>
                {
                    if (GenericHelpers.TryGetAddonByName<AtkUnitBase>("LookingForGroup", out var addon))
                    {
                        var btn = addon->UldManager.NodeList[35];
                        var enabled = btn->GetAsAtkComponentNode()->Component->UldManager.NodeList[2]->Alpha_2 == 255;
                        var selected = btn->GetAsAtkComponentNode()->Component->UldManager.NodeList[4]->GetAsAtkImageNode()->PartId == 0;
                        if (enabled)
                        {
                            if (!selected)
                            {
                                PluginLog.Debug($"Selecting hunts");
                                Callback.Fire(addon, true, 21, 11, Callback.ZeroAtkValue);
                            }
                            return true;
                        }
                    }
                    return false;
                });
            }
        }
        catch(Exception ex)
        { 
            PluginLog.Error(ex.ToString());
        }
    }


}
