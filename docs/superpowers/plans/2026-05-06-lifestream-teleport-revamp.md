# Lifestream-only teleport revamp Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace TeleporterPlugin entirely with the `ECommons.IPC` Lifestream subscriber and fix the broken "Aetherite Unknown" coords-fallback so hunts without a server-supplied aetheryte still get a usable destination.

**Architecture:** The `ECommons.IPC` NuGet package (already referenced) ships a typed Lifestream IPC subscriber accessed via `ECommonsIPC.Lifestream` (`Teleport(uint, byte)`, `IsBusy()`, `ChangeWorld(string, WorldChangeAetheryte?)`, `Available`). HuntAlerts will pass aetheryte **row IDs** end-to-end (`HuntTrainMessage` gains `startLocationAetheryteId`); `MapManager` resolves names ↔ IDs and provides a zone-primary fallback. All TeleporterPlugin integration (config, install checks, `teleporterEnabled` field, `/tp` and `/tpm` calls) is deleted.

**Tech Stack:** C# 12 / .NET 10 (Dalamud plugin), ECommons, ECommons.IPC v1.0.0.19, Newtonsoft.Json, Lumina Excel sheets.

**Spec:** [`docs/superpowers/specs/2026-05-06-lifestream-teleport-revamp-design.md`](../specs/2026-05-06-lifestream-teleport-revamp-design.md)

**Testing note:** This is a Dalamud plugin — there is no automated test suite in the repo. "Verification" for each task means a successful `dotnet build HuntAlerts/HuntAlerts.csproj` (the project is set up via `Dalamud.NET.SDK`). End-to-end behavior is validated manually in-game per the spec's "Testing" section. Each task ends with a build and a commit; the final task lists the manual checklist to run before declaring the work done.

---

## File map

| File | Responsibility | Change |
|------|----------------|--------|
| `HuntAlerts/Helpers/MapManager.cs` | Aetheryte name/ID/coords resolution | Return `(uint, string)` from nearest-aetheryte, add `GetZonePrimaryAetheryte`, `LookupAetheryteByName` |
| `HuntAlerts/Helpers/HuntTrainMessage.cs` | Cached message DTO | Add `startLocationAetheryteId`, remove `teleporterEnabled` |
| `HuntAlerts/Helpers/Utilities.cs` | Drives the teleport flow | Replace `/li` and `/tp`/`/tpm` calls with `ECommonsIPC.Lifestream`; drop `teleporterEnabled` parameter and city-name massage |
| `HuntAlerts/Helpers/MessageCacheManager.cs` | Ctrl-click handler | Drop teleporter install/integration check; pass new field through |
| `HuntAlerts/Messaging/WebSocketManagement.cs` | Inbound message → `HuntTrainMessage` | Fix Unknown-aetherite ordering; drop teleporter install/integration check; populate `startLocationAetheryteId` for hunt-train and srank flows |
| `HuntAlerts/Configuration/Configuration.cs` | Persisted plugin config | Remove `TeleporterIntegration` |
| `HuntAlerts/Windows/ConfigWindow.cs` | Settings UI | Remove teleporter integration UI; simplify combined-disable conditions |
| `HuntAlerts/Windows/NotifyWindow.cs` | Hunt notification window | Simplify button-visible condition; drop `teleporterEnabled` reference |

`HuntAlerts/Services/IPCManager.cs` is intentionally untouched — it's the outbound publisher (`OnHuntTrainMessageReceived`) that exposes HuntAlerts to other plugins, unrelated to consuming Lifestream.

---

## Task ordering rationale

The dependency chain is: `MapManager` (aetheryte resolution) → `HuntTrainMessage` (carries the new field) → `WebSocketManagement` (populates it on the inbound path) → `Utilities` (consumes it on the teleport path) → callers/UI cleanup → final teleporter rip-out. Each task should leave the project building.

---

### Task 1: Extend MapManager with ID-returning helpers

**Files:**
- Modify: `HuntAlerts/Helpers/MapManager.cs`

- [ ] **Step 1: Replace the file contents**

Open `HuntAlerts/Helpers/MapManager.cs` and replace the whole file with:

```csharp
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Logging;
using Lumina.Excel.Sheets;
using System;
using System.Linq;

namespace HuntAlerts.Helpers;
public static class MapManager
{
    public static void OpenMapWithMarker(uint territoryType, float x, float y)
    {
        var map = Svc.Data.GetExcelSheet<TerritoryType>().GetRow(territoryType).Map.RowId;
        var linkPayload = new MapLinkPayload(territoryType, map, x, y);
        Svc.GameGui.OpenMapWithMapLink(linkPayload);
    }

    public static (uint RowId, string Name) GetNearestAetheryte(uint territoryType, float x, float y)
    {
        uint bestId = 0;
        var bestName = "";
        double distance = 0;
        foreach (var data in Svc.Data.GetExcelSheet<Aetheryte>())
        {
            if (!data.IsAetheryte) continue;
            if (data.Territory.ValueNullable == null) continue;
            if (data.PlaceName.ValueNullable == null) continue;
            if (data.Territory.Value.RowId != territoryType) continue;
            if (!Svc.Data.GetExcelSheet<Map>().TryGetFirst(m => m.TerritoryType.RowId == territoryType, out var place)) continue;

            var scale = place.SizeFactor;
            var mapMarker = Svc.Data.GetSubrowExcelSheet<MapMarker>().AllRows()
                .FirstOrNull(m => m.DataType == 3 && m.DataKey.RowId == data.RowId);
            if (mapMarker == null)
            {
                DuoLog.Error($"Cannot find aetheryte position for {territoryType}#{data.PlaceName.Value.Name}");
                continue;
            }
            var ax = ConvertMapMarkerToMapCoordinate(mapMarker.Value.X, scale);
            var ay = ConvertMapMarkerToMapCoordinate(mapMarker.Value.Y, scale);
            PluginLog.Debug($"Aetheryte: {data.PlaceName.Value.Name} ({ax} ,{ay})");
            var d = Math.Pow(ax - x, 2) + Math.Pow(ay - y, 2);
            if (bestId == 0 || d < distance)
            {
                distance = d;
                bestId = data.RowId;
                bestName = data.PlaceName.ValueNullable?.Name.ToString() ?? "";
            }
        }
        return (bestId, bestName);
    }

    public static (uint RowId, string Name) GetNearestAetheryte(MapLinkPayload maplinkMessage)
    {
        return GetNearestAetheryte(maplinkMessage.TerritoryType.RowId, maplinkMessage.XCoord, maplinkMessage.YCoord);
    }

    public static (uint RowId, string Name) GetZonePrimaryAetheryte(uint territoryType)
    {
        // Closest aetheryte to map center (21, 21 in standard map space) is the zone's "primary".
        return GetNearestAetheryte(territoryType, 21f, 21f);
    }

    public static (uint RowId, string Name)? LookupAetheryteByName(uint territoryType, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (var data in Svc.Data.GetExcelSheet<Aetheryte>())
        {
            if (!data.IsAetheryte) continue;
            if (data.Territory.ValueNullable == null) continue;
            if (data.PlaceName.ValueNullable == null) continue;
            if (data.Territory.Value.RowId != territoryType) continue;
            var n = data.PlaceName.ValueNullable?.Name.ToString() ?? "";
            if (n.EqualsIgnoreCase(name))
            {
                return (data.RowId, n);
            }
        }
        return null;
    }

    public static float ConvertMapMarkerToMapCoordinate(int pos, float scale)
    {
        var num = scale / 100f;
        var rawPosition = (int)((float)(pos - 1024.0) / num * 1000f);
        return ConvertRawPositionToMapCoordinate(rawPosition, scale);
    }

    public static float ConvertRawPositionToMapCoordinate(int pos, float scale)
    {
        var num = scale / 100f;
        return (float)((pos / 1000f * num + 1024.0) / 2048.0 * 41.0 / num + 1.0);
    }

    public static double ToMapCoordinate(double val, float scale)
    {
        var c = scale / 100.0;
        val *= c;
        return 41.0 / c * ((val + 1024.0) / 2048.0) + 1;
    }
}
```

Why: returns `(uint RowId, string Name)` so callers get the aetheryte ID needed by `Lifestream.Teleport(uint, byte)`. The `MapLinkPayload` overload now delegates to the coords-based one (DRY). `GetZonePrimaryAetheryte` reuses the same nearest-finder pointed at map center. `LookupAetheryteByName` does a case-insensitive lookup constrained to the territory.

- [ ] **Step 2: Verify the project builds**

Run: `dotnet build HuntAlerts/HuntAlerts.csproj -nologo`
Expected: `Build succeeded` with 0 errors. Existing callers of `GetNearestAetheryte` (in `WebSocketManagement.cs:307`) will fail to compile because the return type changed — that's the next task. Until then, **expect at least one compile error pointing at `WebSocketManagement.cs:307`** (`Cannot implicitly convert type '(uint RowId, string Name)' to 'string'`). Do not commit yet — Task 2 fixes that line.

- [ ] **Step 3: Defer commit until Task 2**

The repo policy is "every task ends with a working build". Since Task 1 alone leaves a callsite broken, commit Task 1 + Task 2 together at the end of Task 2.

---

### Task 2: Add startLocationAetheryteId to HuntTrainMessage and update producer

**Files:**
- Modify: `HuntAlerts/Helpers/HuntTrainMessage.cs`
- Modify: `HuntAlerts/Messaging/WebSocketManagement.cs` (hunt-train branch around lines 280-330; srank branch around lines 409-421)

- [ ] **Step 1: Update `HuntTrainMessage.cs`**

Replace the file contents with:

```csharp
namespace HuntAlerts.Helpers;
public class HuntTrainMessage
{
    public string Message;
    public string huntType;
    public string huntKind;
    public string huntWorld;
    public string currentworldName;
    public string currentregionName;
    public string huntregionName;
    public string Posted_Time;
    public string startLocation;
    public uint startLocationAetheryteId;
    public string startZone;
    public int instance;
    public string locationCoords;
    public bool openmaponArrival;
    public bool lifestreamEnabled;

    public HuntTrainMessage(string message, string huntType, string huntKind, string huntWorld,
        string currentworldName, string currentregionName, string huntregionName, string posted_Time,
        string startLocation, uint startLocationAetheryteId, string startZone, int instance,
        string locationCoords, bool openmaponArrival, bool lifestreamEnabled)
    {
        this.Message = message;
        this.huntType = huntType;
        this.huntKind = huntKind;
        this.huntWorld = huntWorld;
        this.currentworldName = currentworldName;
        this.currentregionName = currentregionName;
        this.huntregionName = huntregionName;
        this.Posted_Time = posted_Time;
        this.startLocation = startLocation;
        this.startLocationAetheryteId = startLocationAetheryteId;
        this.startZone = startZone;
        this.instance = instance;
        this.locationCoords = locationCoords;
        this.openmaponArrival = openmaponArrival;
        this.lifestreamEnabled = lifestreamEnabled;
    }
}
```

Why: adds `startLocationAetheryteId` immediately after `startLocation` (the natural pairing) and removes `teleporterEnabled` per spec section 4. The constructor parameter order mirrors field order for readability.

- [ ] **Step 2: Update the hunt-train branch in `WebSocketManagement.cs`**

Locate the block from "string startLocation = huntMessage.AetheriteName;" through the construction of `htmessage` (currently lines ~283-329). Replace it with:

```csharp
                                bool lifestreamEnabled = this.Configuration.LifestreamIntegration && (lifestreamInstalled == true);
                                bool openmaponArrival = this.Configuration.OpenMapOnArrival;

                                string startLocation = huntMessage.AetheriteName;
                                uint startLocationAetheryteId = 0;
                                double? coordX = null, coordY = null;

                                try
                                {
                                    var (cx, cy) = ExtractCoordinates(messageContent);
                                    coordX = cx;
                                    coordY = cy;
                                    if (cx is not null && cy is not null)
                                    {
                                        locationCoords = $"{(float)cx}, {(float)cy}";
                                    }
                                    PluginLog.Verbose($"Extracted Coordinates {cx}, {cy} from message");
                                }
                                catch (Exception ex)
                                {
                                    PluginLog.Error("Error parsing coordinates from message.");
                                    PluginLog.Error(ex.ToString());
                                }

                                uint tt = 0;
                                bool haveTerritory = Svc.Data.GetExcelSheet<TerritoryType>().TryGetFirst(
                                    x => x.TerritoryIntendedUse.RowId == (uint)TerritoryIntendedUseEnum.Open_World
                                         && (x.PlaceName.ValueNullable?.Name.ExtractText() ?? "").EqualsIgnoreCase(huntMessage.LocationName),
                                    out var ttRow);
                                if (haveTerritory) tt = ttRow.RowId;

                                if (startLocation == "invalid")
                                {
                                    if (haveTerritory && coordX is not null && coordY is not null)
                                    {
                                        var (id, name) = MapManager.GetNearestAetheryte(tt, (float)coordX, (float)coordY);
                                        startLocationAetheryteId = id;
                                        startLocation = name;
                                        PluginLog.Verbose($"Resolved nearest aetheryte from coords on territory {tt}: {name} (id {id})");
                                    }
                                    else if (haveTerritory)
                                    {
                                        var (id, name) = MapManager.GetZonePrimaryAetheryte(tt);
                                        startLocationAetheryteId = id;
                                        startLocation = name;
                                        PluginLog.Verbose($"No coords; using zone-primary aetheryte on territory {tt}: {name} (id {id})");
                                    }
                                }
                                else if (haveTerritory)
                                {
                                    var match = MapManager.LookupAetheryteByName(tt, startLocation);
                                    if (match is not null)
                                    {
                                        startLocationAetheryteId = match.Value.RowId;
                                    }
                                }

                                if (string.IsNullOrEmpty(startLocation))
                                {
                                    startLocation = "Unknown";
                                }

                                string startZone = huntMessage.LocationName;
                                string aetheriteName = huntMessage.AetheriteName;
                                string formatted_message = $"Kind: Hunt Train{Environment.NewLine}Hunt: {huntMessage.Kind}{Environment.NewLine}Start Zone: {startZone}{Environment.NewLine}Aetherite: {startLocation}{Environment.NewLine}World: {huntMessage.World}{Environment.NewLine}Posted: {ConvertTime(huntMessage.Posted_Epoch)}{Environment.NewLine}{Environment.NewLine}" + messageContent;

                                int instance = 1;

                                int textColor = this.Configuration.TextColor;
                                SeString message;

                                var htmessage = new HuntTrainMessage(formatted_message, huntMessage.Type, huntMessage.Kind, huntMessage.World, currentworldName, currentregionName, huntregionName, ConvertTime(huntMessage.Posted_Epoch), startLocation, startLocationAetheryteId, startZone, instance, locationCoords, openmaponArrival, lifestreamEnabled);
                                var link = P.MessageCacheManager.AddMessage(htmessage);
                                Service.IPCManager.OnHuntTrainMessageReceived(htmessage);
```

Also remove the line `bool teleporterEnabled = this.Configuration.TeleporterIntegration && (teleporterInstalled == true);` from this block — it's gone in the new code above.

The line at ~356 that logs `$"Teleporter: {teleporterEnabled} | Lifestream: ..."` becomes:

```csharp
                                PluginLog.Verbose($"Lifestream: {lifestreamEnabled} | startLocation: {startLocation} | startLocationAetheryteId: {startLocationAetheryteId} | startZone: {startZone}");
```

Why: this is the spec section 3 fix. The "Unknown" rename now runs only after the fallback paths, the resolution always tries to produce an ID, and the rename only fires when the *name* is empty (preserving display info). Coords are extracted once, then reused for both the `locationCoords` string and the aetheryte resolution — the previous code parsed inside a nested try/catch that obscured this.

- [ ] **Step 3: Update the srank branch in `WebSocketManagement.cs`**

Locate the srank block around lines 409-421 (the `bool teleporterEnabled = ...` line down through `var htmessage = new HuntTrainMessage(...)` for srank). Replace with:

```csharp
                                            bool lifestreamEnabled = this.Configuration.LifestreamIntegration && (lifestreamInstalled == true);
                                            bool openmaponArrival = this.Configuration.OpenMapOnArrival;
                                            string startLocation = aetheriteName;
                                            uint startLocationAetheryteId = 0;
                                            string startZone = locationName;

                                            uint ttSrank = 0;
                                            bool haveTerritorySrank = Svc.Data.GetExcelSheet<TerritoryType>().TryGetFirst(
                                                x => x.TerritoryIntendedUse.RowId == (uint)TerritoryIntendedUseEnum.Open_World
                                                     && (x.PlaceName.ValueNullable?.Name.ExtractText() ?? "").EqualsIgnoreCase(startZone),
                                                out var ttRowSrank);
                                            if (haveTerritorySrank) ttSrank = ttRowSrank.RowId;

                                            if (haveTerritorySrank)
                                            {
                                                if (string.IsNullOrEmpty(startLocation) || startLocation == "invalid")
                                                {
                                                    var (id, name) = MapManager.GetZonePrimaryAetheryte(ttSrank);
                                                    startLocationAetheryteId = id;
                                                    startLocation = name;
                                                }
                                                else
                                                {
                                                    var match = MapManager.LookupAetheryteByName(ttSrank, startLocation);
                                                    if (match is not null) startLocationAetheryteId = match.Value.RowId;
                                                }
                                            }

                                            if (string.IsNullOrEmpty(startLocation))
                                            {
                                                startLocation = "Unknown";
                                            }
```

Then update the srank `new HuntTrainMessage(...)` call at line ~421 to pass `startLocationAetheryteId` after `startLocation`:

```csharp
                                                var htmessage = new HuntTrainMessage(messageContent, huntMessage.Type, huntMessage.Kind, huntMessage.World, currentworldName, currentregionName, huntregionName, ConvertTime(huntMessage.Posted_Epoch), startLocation, startLocationAetheryteId, startZone, instance, locationCoords, openmaponArrival, lifestreamEnabled);
```

Also remove the now-unused line `bool teleporterEnabled = this.Configuration.TeleporterIntegration && (teleporterInstalled == true);` from the srank block.

Why: the srank flow constructs its own `HuntTrainMessage` and must populate the new field too. SRank messages don't go through `ExtractCoordinates` (the server provides `LocationCoords` directly), so the resolution is simpler — try name lookup, fall back to zone primary.

- [ ] **Step 4: Verify the project builds**

Run: `dotnet build HuntAlerts/HuntAlerts.csproj -nologo`
Expected: build still has errors — `Utilities.ExecuteTeleport` and `MessageCacheManager.ProcessLinkPayload` and `NotifyWindow.Draw` still reference `teleporterEnabled` and call `ExecuteTeleport` with the old signature. Those are Tasks 3-5. **The hunt-train and srank construction sites should compile cleanly now**; the remaining errors should all be in `Utilities.cs`, `MessageCacheManager.cs`, and `NotifyWindow.cs`.

- [ ] **Step 5: Hold the commit**

Defer commit until Task 3.

---

### Task 3: Rewrite Utilities.ExecuteTeleport to use ECommonsIPC.Lifestream

**Files:**
- Modify: `HuntAlerts/Helpers/Utilities.cs`

- [ ] **Step 1: Update the `using` directives**

At the top of `HuntAlerts/Helpers/Utilities.cs`, add:

```csharp
using ECommons.IPC;
```

(Alongside the existing `using ECommons;` etc.)

- [ ] **Step 2: Replace `ExecuteTeleport`**

Replace the entire `ExecuteTeleport` method (current lines 89-263) with:

```csharp
        private static CancellationTokenSource _cancellationTokenSource;
        private static bool _isTaskRunning = false;
        public static async void ExecuteTeleport(string world, string startLocation, uint startLocationAetheryteId, string startZone, string locationCoords, int instance, bool openmaponArrival, bool lifestreamEnabled)
        {
            try
            {
                if (_isTaskRunning)
                {
                    _cancellationTokenSource.Cancel();
                    _isTaskRunning = false;
                    return;
                }

                if (!lifestreamEnabled || !ECommonsIPC.Lifestream.Available)
                {
                    Svc.Chat.Print("Lifestream is required for teleport but is not enabled or installed.");
                    return;
                }

                _cancellationTokenSource = new CancellationTokenSource();
                var token = _cancellationTokenSource.Token;
                _isTaskRunning = true;

                string currentworldName = Svc.Framework.RunOnFrameworkThread(() => Svc.Objects.LocalPlayer?.CurrentWorld.ValueNullable?.Name.ToString()).Result ?? "";
                if (currentworldName.IsNullOrEmpty())
                {
                    _isTaskRunning = false;
                    PluginLog.Warning($"Player is not available");
                    return;
                }
                string currentregionName = HuntAlerts.P.Configuration.DatacenterRegionMap[HuntAlerts.P.Configuration.WorldDatacenterMap[currentworldName]];
                string huntregionName = HuntAlerts.P.Configuration.DatacenterRegionMap[HuntAlerts.P.Configuration.WorldDatacenterMap[world]];

                if (huntregionName != currentregionName)
                {
                    Svc.Chat.Print("You can't teleport there, you are not in the same region as this hunt");
                    return;
                }

                bool hasToServerTransfer = currentworldName != world;
                if (hasToServerTransfer)
                {
                    PluginLog.Verbose($"Lifestream ChangeWorld -> {world}");
                    if (!ECommonsIPC.Lifestream.ChangeWorld(world))
                    {
                        Svc.Chat.Print($"Lifestream rejected the world change to {world} (busy or unreachable).");
                        return;
                    }
                }

                if (startLocationAetheryteId == 0)
                {
                    PluginLog.Verbose("No usable aetheryte ID for this hunt; world change done, no teleport will be issued.");
                    return;
                }

                var startTime = DateTime.Now;
                while (!token.IsCancellationRequested && (DateTime.Now - startTime).TotalSeconds <= 720)
                {
                    bool isLoggedIn = Svc.Framework.RunOnFrameworkThread(() => Svc.ClientState.IsLoggedIn).Result;
                    bool localPlayerExists = Svc.Framework.RunOnFrameworkThread(() => Svc.Objects.LocalPlayer != null).Result;
                    if (isLoggedIn && localPlayerExists)
                    {
                        currentworldName = Svc.Framework.RunOnFrameworkThread(() => Svc.Objects.LocalPlayer.CurrentWorld.Value.Name.ToString()).Result;
                        PluginLog.Verbose($"Player is logged in. Currentworld: {currentworldName}");

                        if (currentworldName == world && !ECommonsIPC.Lifestream.IsBusy())
                        {
                            var targetableStartTime = DateTime.Now;
                            while (!token.IsCancellationRequested && (DateTime.Now - targetableStartTime).TotalSeconds <= 60)
                            {
                                bool isTargetable = Svc.Framework.RunOnFrameworkThread(() => Svc.Objects.LocalPlayer.IsTargetable).Result;
                                if (isTargetable)
                                {
                                    PluginLog.Verbose($"On hunt world; teleporting to aetheryte {startLocation} (id {startLocationAetheryteId})");
                                    if (hasToServerTransfer)
                                    {
                                        await Task.Delay(2000, token);
                                    }

                                    if (!ECommonsIPC.Lifestream.Teleport(startLocationAetheryteId, 0))
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
                                            var territoryName = Svc.Framework.RunOnFrameworkThread(() => Svc.Data.GetExcelSheet<TerritoryType>()
                                                                 .GetRowOrDefault(territoryType)?.PlaceName.ValueNullable?.Name.ToString()).Result;

                                            PluginLog.Verbose($"Waiting on targetable + zone match. Current: {territoryName} | Destination: {startZone}");
                                            isTargetable = Svc.Framework.RunOnFrameworkThread(() => Svc.Objects.LocalPlayer.IsTargetable).Result;
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

                    await Task.Delay(5000, token);
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
                _isTaskRunning = false;
            }
        }
```

Why this layout:

1. **Signature** drops `teleporterEnabled` and gains `uint startLocationAetheryteId`.
2. **Pre-flight** — bail early if Lifestream isn't enabled/installed (we no longer have a teleporter fallback). If `startLocationAetheryteId == 0`, do the world change but skip the aetheryte teleport. This is the spec's "preserve display name even when no usable ID" path.
3. **Region check** is the early `return` instead of an `else` branch, flattening one level of nesting.
4. **World swap** uses `Lifestream.ChangeWorld(world)`, which auto-detects same-DC vs cross-DC and is a no-op if already on world. The previous code only called `/li` when `currentworldName != world`, so we mirror that behavior with `hasToServerTransfer`.
5. **Wait-for-arrival outer loop** keeps the 720s timeout but adds `!ECommonsIPC.Lifestream.IsBusy()` to the "we're done transferring" predicate — this catches the case where the world matches but Lifestream is still doing housekeeping (e.g., dismissing dialogs, settling on the new aetheryte).
6. **Aetheryte teleport** is `Lifestream.Teleport(startLocationAetheryteId, 0)`. The Limsa/Gridania/Ul'dah string massaging is gone — IDs don't need it. Sub-aetheryte index is `0` for primary aetherytes (sub-aetheryte teleports go through `AethernetTeleport`, which we don't use here).
7. **Open-map-on-arrival** logic is unchanged — same 60s targetable+zone wait, same `FlagOnMap` call.

- [ ] **Step 3: Verify the project builds**

Run: `dotnet build HuntAlerts/HuntAlerts.csproj -nologo`
Expected: still failing on `MessageCacheManager.cs:67` (call site of `ExecuteTeleport` with old signature) and `NotifyWindow.cs:51,71` (references to `teleporterEnabled` and old `ExecuteTeleport` signature). Those are Tasks 4 and 5.

- [ ] **Step 4: Hold the commit**

Defer commit until Task 5.

---

### Task 4: Update MessageCacheManager (Ctrl-click handler)

**Files:**
- Modify: `HuntAlerts/Helpers/MessageCacheManager.cs`

- [ ] **Step 1: Replace `ProcessLinkPayload`**

Replace the entire `ProcessLinkPayload` method (currently lines 45-76) with:

```csharp
    void ProcessLinkPayload(uint cmd, SeString str)
    {
        bool? lifestreamInstalled = Svc.PluginInterface.InstalledPlugins.FirstOrDefault(x => x.InternalName == "Lifestream")?.IsLoaded;
        bool ctrlHeld = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
        bool ctrlclickTeleport = HuntAlerts.P.Configuration.ctrlclickTeleport
            && lifestreamInstalled == true
            && HuntAlerts.P.Configuration.LifestreamIntegration;

        if (Messages[cmd] != null)
        {
            if (ctrlHeld && ctrlclickTeleport)
            {
                var world = Messages[cmd].huntWorld;
                var startLocation = Messages[cmd].startLocation;
                var startLocationAetheryteId = Messages[cmd].startLocationAetheryteId;
                var startZone = Messages[cmd].startZone;
                var locationCoords = Messages[cmd].locationCoords;
                var openmaponArrival = Messages[cmd].openmaponArrival;
                var lifestreamEnabled = Messages[cmd].lifestreamEnabled;
                var instance = Messages[cmd].instance;

                PluginLog.Verbose("Ctrl key is held down. attempting to teleport");
                Utilities.ExecuteTeleport(world, startLocation, startLocationAetheryteId, startZone, locationCoords, instance, openmaponArrival, lifestreamEnabled);
            }
            else
            {
                HuntAlerts.P.NotifyWindow.IsOpen = true;
                HuntAlerts.P.NotifyWindow.CurrentMessage = Messages[cmd];
            }
        }
    }
```

Why: drops the teleporter install/integration check (per spec section 4), passes the new `startLocationAetheryteId` field, and matches the new `ExecuteTeleport` signature. The Ctrl-click feature itself stays — only its eligibility gate simplifies.

- [ ] **Step 2: Verify the project builds**

Run: `dotnet build HuntAlerts/HuntAlerts.csproj -nologo`
Expected: only `NotifyWindow.cs` errors remain (`teleporterEnabled` reference and old `ExecuteTeleport` call).

---

### Task 5: Update NotifyWindow (visible button + click handler)

**Files:**
- Modify: `HuntAlerts/Windows/NotifyWindow.cs`

- [ ] **Step 1: Replace the `Draw` body**

In `HuntAlerts/Windows/NotifyWindow.cs`, replace the body of `Draw` (currently lines 36-104) with:

```csharp
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
```

Why: the button visibility condition becomes `same region && lifestream enabled && we have a usable aetheryte ID`. This is per spec section 4 — covers both same-world (where `Lifestream.ChangeWorld` is a no-op) and cross-world (where it does the swap), and hides the button when there's no usable destination ID even though the rest of the message still renders. `huntType`, `postedTime`, and the unused `huntdatacenterName`-style locals from the old method are dropped (they were never read after assignment).

- [ ] **Step 2: Verify the project builds**

Run: `dotnet build HuntAlerts/HuntAlerts.csproj -nologo`
Expected: `Build succeeded` with 0 errors. The `TeleporterIntegration` config property is unused now but still compiles — Task 7 removes it.

- [ ] **Step 3: Commit Tasks 1-5**

```bash
git add HuntAlerts/Helpers/MapManager.cs HuntAlerts/Helpers/HuntTrainMessage.cs HuntAlerts/Helpers/Utilities.cs HuntAlerts/Helpers/MessageCacheManager.cs HuntAlerts/Messaging/WebSocketManagement.cs HuntAlerts/Windows/NotifyWindow.cs
git commit -m "$(cat <<'EOF'
Use ECommonsIPC.Lifestream for hunt teleport flow

Wires the typed Lifestream subscriber from the ECommons.IPC NuGet into
Utilities.ExecuteTeleport (replacing /li and /tp/tpm command dispatch),
threads aetheryte row IDs end-to-end via HuntTrainMessage, and fixes the
ordering bug in WebSocketManagement that prevented the coords-based
"Aetherite Unknown" fallback from ever firing. Adds a zone-primary
fallback for the no-coords case.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Remove Teleporter UI from ConfigWindow

**Files:**
- Modify: `HuntAlerts/Windows/ConfigWindow.cs`

- [ ] **Step 1: Replace the integrations block**

Locate the block from the comment `// Create a simple header` / `ImGui.Text("Integrations (Changes take effect next hunt message)");` through the end of the `ctrlclickTeleport` checkbox (currently lines 173-236). Replace with:

```csharp
        // Create a simple header
        ImGui.Text("Integrations (Changes take effect next hunt message)");

        // Optional: Draw a separator line
        ImGui.Separator();

        bool? lifestreamInstalled = Svc.PluginInterface.InstalledPlugins.FirstOrDefault(x => x.InternalName == "Lifestream")?.IsLoaded;

        if (lifestreamInstalled != true)
        {
            ImGui.BeginDisabled();
        }

        var lifestreamIntegration = this.Configuration.LifestreamIntegration;
        if (ImGui.Checkbox("Enable Lifestream Integration", ref lifestreamIntegration))
        {
            this.Configuration.LifestreamIntegration = lifestreamIntegration;
            this.Configuration.Save();
        }

        if (lifestreamInstalled != true)
        {
            ImGui.EndDisabled();
        }

        if (lifestreamInstalled != true || !this.Configuration.LifestreamIntegration)
        {
            ImGui.BeginDisabled();
        }

        var ctrlclickTeleport = this.Configuration.ctrlclickTeleport;
        if (ImGui.Checkbox("Ctrl-Click messages to teleport", ref ctrlclickTeleport))
        {
            this.Configuration.ctrlclickTeleport = ctrlclickTeleport;
            this.Configuration.Save();
        }

        if (lifestreamInstalled != true || !this.Configuration.LifestreamIntegration)
        {
            ImGui.EndDisabled();
        }
```

Why: Removes the `teleporterInstalled` lookup, the "Enable Teleporter Integration" checkbox, and the combined-disable conditions referencing teleporter. The ctrl-click row is now gated on Lifestream alone.

- [ ] **Step 2: Verify the project builds**

Run: `dotnet build HuntAlerts/HuntAlerts.csproj -nologo`
Expected: `Build succeeded` with 0 errors. `Configuration.TeleporterIntegration` is still defined (next task removes it) but no longer referenced anywhere.

- [ ] **Step 3: Commit**

```bash
git add HuntAlerts/Windows/ConfigWindow.cs
git commit -m "$(cat <<'EOF'
Remove TeleporterPlugin integration UI

The teleporter integration checkbox and its combined-disable plumbing
are gone; lifestream is the only teleport backend now.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: Remove TeleporterIntegration config property

**Files:**
- Modify: `HuntAlerts/Configuration/Configuration.cs`

- [ ] **Step 1: Delete the property**

In `HuntAlerts/Configuration/Configuration.cs`, delete line 29:

```csharp
        public bool TeleporterIntegration { get; set; } = false;
```

Leave `LifestreamIntegration` and `ctrlclickTeleport` in place.

- [ ] **Step 2: Verify the project builds**

Run: `dotnet build HuntAlerts/HuntAlerts.csproj -nologo`
Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Verify config compatibility note**

Existing users have `"TeleporterIntegration": true|false` in their saved JSON config. Newtonsoft's default behavior is to silently ignore unknown JSON properties on deserialize — confirm this by skimming the rest of `Configuration.cs` for any `JsonConvert` settings that would change that. None should be present (the default is `MissingMemberHandling.Ignore`). No migration code needed.

- [ ] **Step 4: Commit**

```bash
git add HuntAlerts/Configuration/Configuration.cs
git commit -m "$(cat <<'EOF'
Drop TeleporterIntegration config property

Existing users with the property persisted in JSON will have it silently
ignored on load (Newtonsoft default), and it will not be re-written.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: Manual verification checklist

**Files:** none — this is in-game testing.

The plugin can only be loaded inside FFXIV via Dalamud, so this is a manual smoke pass. Skip any item that requires conditions you can't easily produce; mark the rest as observed.

- [ ] **Step 1: Build and load**

Run: `dotnet build HuntAlerts/HuntAlerts.csproj -nologo`
Expected: `Build succeeded` with 0 errors.

Load the plugin into Dalamud's dev environment and open the HuntAlerts config window. Verify:
- Only "Enable Lifestream Integration" appears under "Integrations" (no teleporter checkbox).
- "Ctrl-Click messages to teleport" is enabled when Lifestream is installed and integration is on.

- [ ] **Step 2: Same-world hunt with valid aetheryte name**

Trigger a hunt for your current world whose `AetheriteName` is a real aetheryte name. Click "Teleport to Hunt".
Expected: Lifestream teleports directly to the named aetheryte. No `/tp` command appears in chat.

- [ ] **Step 3: Cross-world same-DC hunt**

Trigger a hunt for a different world on the same DC. Click "Teleport to Hunt".
Expected: Lifestream changes world, then teleports to the aetheryte once you're targetable on the new world.

- [ ] **Step 4: Cross-DC hunt**

If your current DC has visiting enabled, trigger a hunt on a different DC in the same region. Click teleport.
Expected: Lifestream performs the cross-DC visit (`isDcTransfer=true` path inside `ChangeWorld`), then teleports.

- [ ] **Step 5: Aetherite Unknown with valid coords**

Force a hunt message with `AetheriteName="invalid"` and valid coords (or wait for one in the wild). Open the notification.
Expected: the Aetherite line shows the computed nearest aetheryte (not "Unknown"). Teleport button works.

- [ ] **Step 6: Aetherite Unknown with no coords**

Force a hunt message with `AetheriteName="invalid"` and no coords in the body.
Expected: the Aetherite line shows the zone's primary aetheryte (closest to map center). Teleport button works.

- [ ] **Step 7: Lifestream not installed**

Disable the Lifestream plugin. Trigger any hunt.
Expected: notification appears, but the "Teleport to Hunt" button is hidden.

- [ ] **Step 8: Existing config compatibility**

If you have a config from before this change with `"TeleporterIntegration": true`, restart Dalamud after the upgrade and reopen the config window.
Expected: no errors in the log, no teleporter checkbox appears, Lifestream Integration checkbox reflects whatever was saved.

- [ ] **Step 9: SRank with valid aetheryte name**

Wait for or simulate an SRank pop on a world your config covers. Click teleport.
Expected: same flow as a hunt — Lifestream world change (if needed) then teleport to the aetheryte.

- [ ] **Step 10: Final commit if any tweaks were needed**

If you discovered fixes during manual testing, commit them as a follow-up. Otherwise no commit required.

---

## Self-review notes

After writing the above:

- **Spec coverage:** Sections 1-5 of the spec map to Tasks 3, 1+2, 2 (srank too), 4-7, 7 respectively. The "Out of scope" items remain out of scope. The "Testing" section maps to Task 8. ✓
- **Placeholders:** none. Every step gives the actual code. ✓
- **Type/name consistency:** `startLocationAetheryteId` (`uint`) is used identically across `HuntTrainMessage`, `WebSocketManagement` (both branches), `Utilities.ExecuteTeleport`, `MessageCacheManager.ProcessLinkPayload`, and `NotifyWindow.Draw`. `MapManager.GetNearestAetheryte` consistently returns `(uint RowId, string Name)` and is called the same way in all sites. ✓
- **Build state:** Tasks 1-5 form one logical unit because each one alone leaves the project broken; the plan defers commits to the end of Task 5 and explicitly notes which earlier-task errors are expected at each intermediate build. ✓
