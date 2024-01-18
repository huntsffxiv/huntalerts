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

namespace HuntAlerts.Windows;
public class NotifyWindow : Window
{
    public Dictionary<string, (string Message, string huntKind, string huntWorld, string currentworldName, string currentregionName, string huntregionName, string Posted_Time,string startLocation, string startZone, bool teleporterEnabled,bool lifestreamEnabled)> Cache = new Dictionary<string, (string, string, string, string, string, string, string, string, string, bool, bool)>();
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

            string headerText = $"Hunt: {huntType}{Environment.NewLine}World: {world}{Environment.NewLine}Posted: {postedTime}{Environment.NewLine}{Environment.NewLine}";
            
            if (currentregionName == huntregionname)
            {

                if (currentworldName != world)
                {
                    if (lifestreamEnabled)
                    {
                        /*float textWidth = ImGui.CalcTextSize(headerText).X;
                        float windowWidth = ImGui.GetWindowWidth();
                        //float buttonWidth = ImGui.CalcTextSize($"Teleport to hunt").X + ImGui.GetStyle().FramePadding.X * 2;
                        float buttonWidth = ImGuiHelpers.GetButtonSize("Teleport to hunt").X;

                        // Calculate space needed to align the button to the right, then subtract 250 pixels to move it to the left
                        float space = windowWidth - textWidth - buttonWidth - ImGui.GetStyle().WindowPadding.X * 2 + 100;

                        // Ensure that the space value does not go negative
                        space = space > 0 ? space : 0;

                        // Place button on the same line as text and align it to the right
                        ImGui.SameLine(space);*/
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
                        if (startLocation != null && (startLocation != "invalid" || startZone != "invalid"))
                        {
                            /*float textWidth = ImGui.CalcTextSize(headerText).X;
                            float windowWidth = ImGui.GetWindowWidth();
                            //float buttonWidth = ImGui.CalcTextSize($"Teleport to Hunt").X + ImGui.GetStyle().FramePadding.X * 2;
                            float buttonWidth = ImGuiHelpers.GetButtonSize("Teleport to Hunt").X;

                            // Calculate space needed to align the button to the right, then subtract 250 pixels to move it to the left
                            float space = windowWidth - textWidth - buttonWidth - ImGui.GetStyle().WindowPadding.X * 2 + 120;

                            // Ensure that the space value does not go negative
                            space = space > 0 ? space : 0;

                            // Place button on the same line as text and align it to the right
                            ImGui.SameLine(space);*/
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
                                    }
                                }
                            });
                        }
                    }
                }
            }

            // If you don't set a wrap position, text wraps at the window edge
            ImGui.PushTextWrapPos();
            ImGui.TextUnformatted(headerText);
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


}
