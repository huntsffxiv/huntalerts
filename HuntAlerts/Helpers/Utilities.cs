using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.IPC;
using ECommons.Logging;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using HuntAlerts.Services;
using Lumina.Excel.Sheets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HuntAlerts.Helpers
{
    public static class Utilities
    {
        public static unsafe void OpenPartyFinder()
        {
            try
            {
                if (EzThrottler.Throttle("OpenHuntPF", 1000))
                {
                    Service.TaskManager.DefaultConfiguration.AbortOnTimeout = true;
                    Service.TaskManager.DefaultConfiguration.TimeLimitMS = 5000;

                    if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>("LookingForGroup", out var _))
                    {
                        Service.TaskManager.Enqueue(() => Chat.SendMessage("/partyfinder"));
                        Service.TaskManager.EnqueueDelay(500);
                    }
                    Service.TaskManager.Enqueue(() =>
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
            catch (Exception ex)
            {
                PluginLog.Error(ex.ToString());
            }
        }

        public static void FlagOnMap(string locationCoords, string startZone)
        {
            try
            {
                // Code to execute when the button is pressed
                PluginLog.Verbose($"Attempting to flag coords {startZone} {locationCoords} on Map");
                uint tt;
                //var (x, y) = (locationCoords.Split(',').Select(s => float.Parse(s.Trim())).ToArray() is float[] coords) ? (coords[0], coords[1]) : (0f, 0f);
                var (x, y) = Messaging.HuntMessageParsing.ExtractCoordinates(locationCoords);
                if (x is null || y is null) return;

                if (Svc.Data.GetExcelSheet<TerritoryType>(Dalamud.Game.ClientLanguage.English).TryGetFirst(x => x.TerritoryIntendedUse.RowId == (uint)TerritoryIntendedUseEnum.Open_World && (x.PlaceName.ValueNullable?.Name.ExtractText() ?? "").EqualsIgnoreCase(startZone), out var value))
                {
                    tt = value.RowId; //is territory id
                    MapManager.OpenMapWithMarker(tt, (float)x, (float)y);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error("Error placing flag on map.");
                PluginLog.Error(ex.ToString());
            }
        }


        private static CancellationTokenSource? CancellationTokenSource;
        private static int IsTaskRunning = 0; // 0 = idle, 1 = running. Use Interlocked.* only.
        public static async void ExecuteTeleport(string world, string startLocation, uint startLocationAetheryteId, string startZone, string locationCoords, int instance, bool openmaponArrival, bool lifestreamEnabled)
        {
            PluginLog.Information($"[Teleport] ENTER world={world} aetheryte={startLocation}(id={startLocationAetheryteId}) zone={startZone} lifestreamEnabled={lifestreamEnabled} coords={locationCoords}");

            // Atomically claim the running slot. If it was already 1, this is a "click while running" — cancel and return.
            if (Interlocked.Exchange(ref IsTaskRunning, 1) == 1)
            {
                PluginLog.Information("[Teleport] click-while-running detected; cancelling in-flight teleport.");
                CancellationTokenSource?.Cancel();
                Interlocked.Exchange(ref IsTaskRunning, 0);
                return;
            }

            try
            {
                if (!lifestreamEnabled || !ECommonsIPC.Lifestream.Available)
                {
                    PluginLog.Warning($"[Teleport] aborting: lifestreamEnabled={lifestreamEnabled} lifestreamAvailable={ECommonsIPC.Lifestream.Available}");
                    Svc.Chat.Print("Lifestream is required for teleport but is not enabled or installed.");
                    return;
                }

                CancellationTokenSource?.Dispose();
                CancellationTokenSource = new CancellationTokenSource();
                var token = CancellationTokenSource.Token;

                string currentworldName = Svc.Framework.RunOnFrameworkThread(() => Svc.Objects.LocalPlayer?.CurrentWorld.ValueNullable?.Name.ToString()).Result ?? "";
                if (currentworldName.IsNullOrEmpty())
                {
                    PluginLog.Warning($"Player is not available");
                    return;
                }
                string currentregionName = WorldData.TryGetWorld(currentworldName, out var cwInfo) ? WorldData.RegionLabel(cwInfo.Region) : "";
                string huntregionName    = WorldData.TryGetWorld(world,            out var hwInfo) ? WorldData.RegionLabel(hwInfo.Region) : "";

                if (huntregionName != currentregionName)
                {
                    Svc.Chat.Print("You can't teleport there, you are not in the same region as this hunt");
                    return;
                }

                bool hasToServerTransfer = currentworldName != world;
                PluginLog.Information($"[Teleport] currentworld={currentworldName} target={world} hasToServerTransfer={hasToServerTransfer} region={currentregionName}");
                if (hasToServerTransfer)
                {
                    PluginLog.Information($"[Teleport] calling Lifestream.ChangeWorld('{world}')");
                    if (!ECommonsIPC.Lifestream.ChangeWorld(world))
                    {
                        PluginLog.Warning($"[Teleport] ChangeWorld rejected by Lifestream.");
                        Svc.Chat.Print($"Lifestream rejected the world change to {world} (busy or unreachable).");
                        return;
                    }
                }

                if (startLocationAetheryteId == 0)
                {
                    PluginLog.Warning("[Teleport] no aetheryte id; world change done, returning without issuing teleport.");
                    return;
                }

                PluginLog.Information("[Teleport] entering wait loop for target world arrival + Lifestream not-busy.");

                var startTime = DateTime.Now;
                while (!token.IsCancellationRequested && (DateTime.Now - startTime).TotalSeconds <= 720)
                {
                    bool isLoggedIn = Svc.Framework.RunOnFrameworkThread(() => Svc.ClientState.IsLoggedIn).Result;
                    bool localPlayerExists = Svc.Framework.RunOnFrameworkThread(() => Svc.Objects.LocalPlayer != null).Result;
                    if (isLoggedIn && localPlayerExists)
                    {
                        currentworldName = Svc.Framework.RunOnFrameworkThread(() => Svc.Objects.LocalPlayer?.CurrentWorld.Value.Name.ToString() ?? "").Result;
                        PluginLog.Verbose($"Player is logged in. Currentworld: {currentworldName}");

                        var lifestreamBusy = ECommonsIPC.Lifestream.IsBusy();
                        PluginLog.Information($"[Teleport] poll: currentworld={currentworldName} (need {world}) lifestreamBusy={lifestreamBusy}");
                        if (currentworldName == world && !lifestreamBusy)
                        {
                            PluginLog.Information("[Teleport] on target world + Lifestream idle. Waiting for IsTargetable.");
                            var targetableStartTime = DateTime.Now;
                            while (!token.IsCancellationRequested && (DateTime.Now - targetableStartTime).TotalSeconds <= 60)
                            {
                                bool isTargetable = Svc.Framework.RunOnFrameworkThread(() => Svc.Objects.LocalPlayer?.IsTargetable ?? false).Result;
                                if (isTargetable)
                                {
                                    PluginLog.Information($"[Teleport] player targetable. Will teleport to aetheryte '{startLocation}' (id {startLocationAetheryteId}).");
                                    if (hasToServerTransfer)
                                    {
                                        PluginLog.Information("[Teleport] post-transfer settle: sleeping 1s.");
                                        await Task.Delay(1000, token);
                                    }

                                    // Lifestream may have become busy between the world-change settle and now (e.g.,
                                    // post-arrival housekeeping). Wait briefly for it to clear before issuing Teleport.
                                    var teleportIssueStart = DateTime.Now;
                                    while (!token.IsCancellationRequested && ECommonsIPC.Lifestream.IsBusy() && (DateTime.Now - teleportIssueStart).TotalSeconds <= 10)
                                    {
                                        PluginLog.Information("[Teleport] waiting for Lifestream to clear (IsBusy)...");
                                        await Task.Delay(500, token);
                                    }
                                    var stillBusy = ECommonsIPC.Lifestream.IsBusy();
                                    PluginLog.Information($"[Teleport] calling Lifestream.Teleport({startLocationAetheryteId}, 0). IsBusy at call time = {stillBusy}");
                                    var teleportResult = await Svc.Framework.RunOnFrameworkThread(
                                        () => ECommonsIPC.Lifestream.Teleport(startLocationAetheryteId, 0));
                                    PluginLog.Information($"[Teleport] Lifestream.Teleport returned {teleportResult}.");
                                    if (!teleportResult)
                                    {
                                        Svc.Chat.Print($"Lifestream rejected teleport to {startLocation}.");
                                        return;
                                    }

                                    if (openmaponArrival && locationCoords != "")
                                    {
                                        PluginLog.Verbose("Open map on arrival is enabled and coords exist");
                                        var flagStartTime = DateTime.Now;
                                        while (!token.IsCancellationRequested && (DateTime.Now - flagStartTime).TotalSeconds <= 60)
                                        {
                                            var territoryType = Svc.Framework.RunOnFrameworkThread(() => Svc.ClientState.TerritoryType).Result;
                                            var territoryName = Svc.Framework.RunOnFrameworkThread(() => Svc.Data.GetExcelSheet<TerritoryType>(Dalamud.Game.ClientLanguage.English)
                                                                 .GetRowOrDefault(territoryType)?.PlaceName.ValueNullable?.Name.ToString()).Result;

                                            PluginLog.Verbose($"Waiting on targetable + zone match. Current: {territoryName} | Destination: {startZone}");
                                            isTargetable = Svc.Framework.RunOnFrameworkThread(() => Svc.Objects.LocalPlayer?.IsTargetable ?? false).Result;
                                            if (isTargetable && territoryName == startZone)
                                            {
                                                await Task.Delay(1000, token);
                                                PluginLog.Verbose("Opening map and flagging coordinates");
                                                _ = Svc.Framework.RunOnFrameworkThread(() =>
                                                {
                                                    FlagOnMap(locationCoords, startZone);
                                                });
                                                return;
                                            }
                                            await Task.Delay(1000, token);
                                        }
                                    }
                                    return;
                                }
                                await Task.Delay(1000, token);
                            }
                        }
                    }
                    else
                    {
                        PluginLog.Verbose($"Player is still transferring");
                    }

                    await Task.Delay(500, token);
                }
            }
            catch (TaskCanceledException)
            {
                // Cancellation is expected when the user clicks the button again to abort.
            }
            catch (Exception e)
            {
                e.Log();
            }
            finally
            {
                Interlocked.Exchange(ref IsTaskRunning, 0);
            }
        }
    }
}
