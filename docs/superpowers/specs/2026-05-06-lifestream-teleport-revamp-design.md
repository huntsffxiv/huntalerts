# Lifestream-only teleport revamp

**Date:** 2026-05-06
**Status:** Approved (pending plan)

## Goal

Replace TeleporterPlugin entirely with Lifestream as the sole teleport backend, and fix the broken "Aetherite Unknown" fallback so hunts without a server-supplied aetheryte still get a usable destination.

## Background

`HuntAlerts/Helpers/Utilities.cs:91` (`ExecuteTeleport`) drives the teleport flow today. It:

1. Uses `/li {world}` to swap worlds when same-region but different-world.
2. Uses TeleporterPlugin's `/tp {startLocation}` (line 180) to teleport to an aetheryte.
3. Uses TeleporterPlugin's `/tpm {startZone}` (line 222) as a fallback when the aetheryte name is missing.

The "Aetherite Unknown" case in `HuntAlerts/Messaging/WebSocketManagement.cs:283-310` is broken:

```csharp
string startLocation = huntMessage.AetheriteName;
if (startLocation == "invalid") { startLocation = "Unknown"; }   // line 284
// ... later, line 305:
if (startLocation == "invalid") {                                // dead — already "Unknown"
    startLocation = MapManager.GetNearestAetheryte(tt, x, y);
}
```

The rename to `"Unknown"` runs before the coords-based fallback, so the fallback never fires.

The `ECommons.IPC` NuGet package (`v1.0.0.19`, already referenced) ships a typed Lifestream subscriber at `ECommons.IPC.Subscribers.LifestreamIPC.LifestreamIPC` exposing `Teleport(uint, byte)`, `IsBusy()`, `ChangeWorld(string, WorldChangeAetheryte?)`, `Available`, etc., accessed via `ECommonsIPC.Lifestream`. Source: `C:\ECommons.IPC-main\ECommons.IPC\Subscribers\Lifestream\LifestreamIPC.cs`.

## Decisions

- **Q1 — when Lifestream is unavailable:** rip out TeleporterPlugin entirely. No silent fallback. Remove all `TeleporterIntegration` config, install checks, and the `teleporterEnabled` field on `HuntTrainMessage`.
- **Q2 — "Aetherite Unknown" with no coords:** show the teleport button anyway and teleport to the zone's primary/main aetheryte (closest to map center, or first aetheryte found in the zone).
- **Q3 — invocation style:** typed IPC subscriber via `ECommonsIPC.Lifestream` (no command-string parsing).
- **Q4 — instance handling:** out of scope. Keep `instance = 1` and don't change instances on arrival.

## Changes

### 1. Replace teleport call sites (`Helpers/Utilities.cs`)

In `ExecuteTeleport`:

- `Svc.Commands.ProcessCommand($"/li {world}")` → `ECommonsIPC.Lifestream.ChangeWorld(world)` (auto-detects same-DC vs cross-DC and is a no-op when already on the target world).
- `Svc.Commands.ProcessCommand($"/tp {startLocation}")` → `ECommonsIPC.Lifestream.Teleport(aetheryteRowId, 0)`.
- `Svc.Commands.ProcessCommand($"/tpm {startZone}")` → delete. After the WebSocketManagement fix below, we always have an aetheryte ID.
- The Limsa / Gridania / Ul'dah substring-massage block (lines 176-178) goes away — we now pass IDs.
- Inside the polling loop, replace bespoke "is the world swap done" probing with checks of `ECommonsIPC.Lifestream.IsBusy()` alongside the existing `IsLoggedIn` / `LocalPlayer` / `IsTargetable` / territory checks. The 720-second outer timeout and 60-second inner timeout stay; structure stays roughly the same.

The method signature loses `bool teleporterEnabled` and gains `uint startLocationAetheryteId`:

```csharp
public static async void ExecuteTeleport(
    string world, string startLocation, uint startLocationAetheryteId,
    string startZone, string locationCoords, int instance,
    bool openmaponArrival, bool lifestreamEnabled);
```

`startLocation` (string) is kept for log/display only.

### 2. Pass aetheryte IDs, not names (`Helpers/MapManager.cs`)

- Change `GetNearestAetheryte(uint territoryType, float x, float y)` to return `(uint RowId, string Name)`. Internally it already iterates the `Aetheryte` Excel sheet — exposing `data.RowId` is one extra field on the existing tuple/return.
- Change `GetNearestAetheryte(MapLinkPayload)` similarly.
- Add `GetZonePrimaryAetheryte(uint territoryType)` returning `(uint RowId, string Name)`. Implementation: iterate aetherytes whose `Territory.RowId == territoryType && IsAetheryte`, take the one whose map-marker coordinate is closest to the map center (`(21, 21)` in map space, the midpoint of the standard 1-41 range). Returns `(0, "")` if no aetheryte exists in the territory. Used as the no-coords fallback.
- Add `LookupAetheryteByName(uint territoryType, string name)` returning `(uint RowId, string Name)?` for the case where the server provides a name and we still need an ID.

### 3. Fix the "Aetherite Unknown" flow (`Messaging/WebSocketManagement.cs:283-321`)

Replace the existing block with:

```csharp
string startLocation = huntMessage.AetheriteName;
uint startLocationAetheryteId = 0;
double? coordX = null, coordY = null;

try {
    var (cx, cy) = ExtractCoordinates(messageContent);
    coordX = cx; coordY = cy;
    if (cx is not null && cy is not null) {
        locationCoords = $"{(float)cx}, {(float)cy}";
    }
} catch (Exception ex) {
    PluginLog.Error("Error parsing coordinates from message.");
    PluginLog.Error(ex.ToString());
}

uint tt = 0;
bool haveTerritory = Svc.Data.GetExcelSheet<TerritoryType>().TryGetFirst(
    x => x.TerritoryIntendedUse.RowId == (uint)TerritoryIntendedUseEnum.Open_World
         && (x.PlaceName.ValueNullable?.Name.ExtractText() ?? "").EqualsIgnoreCase(startZone),
    out var ttRow);
if (haveTerritory) tt = ttRow.RowId;

if (startLocation == "invalid") {
    if (haveTerritory && coordX is not null && coordY is not null) {
        var (id, name) = MapManager.GetNearestAetheryte(tt, (float)coordX, (float)coordY);
        startLocationAetheryteId = id;
        startLocation = name;
    } else if (haveTerritory) {
        var (id, name) = MapManager.GetZonePrimaryAetheryte(tt);
        startLocationAetheryteId = id;
        startLocation = name;
    }
} else if (haveTerritory) {
    var match = MapManager.LookupAetheryteByName(tt, startLocation);
    if (match is not null) startLocationAetheryteId = match.Value.RowId;
}

if (string.IsNullOrEmpty(startLocation)) {
    startLocation = "Unknown";
}
// startLocationAetheryteId == 0 indicates "no teleport possible" and is checked
// at click time in NotifyWindow / Utilities.ExecuteTeleport. The display name is
// preserved either way so the user still sees where to manually go.
```

`HuntTrainMessage` constructor takes the new `startLocationAetheryteId` and the field is exposed for downstream consumers.

### 4. Rip out TeleporterPlugin

Delete:

- `Configuration.TeleporterIntegration` (and `ctrlclickTeleport` branch that gates on it — keep `ctrlclickTeleport` itself, just simplify the gate to lifestream-only).
- The install check at `Helpers/MessageCacheManager.cs:47-50` (replace with `ECommonsIPC.Lifestream.Available` where the check is still meaningful).
- The install check at `Messaging/WebSocketManagement.cs:109-110`.
- `HuntTrainMessage.teleporterEnabled` field (and constructor param, and all callers).
- `ConfigWindow.cs:179-194` ("Enable Teleporter Integration" checkbox and surrounding install-check messaging). Keep the Lifestream half.
- The combined disable conditions at `ConfigWindow.cs:218, 233` simplify to checking only Lifestream.
- The `(teleporterEnabled && currentworldName == world)` branch in `NotifyWindow.cs:61` — the button condition becomes `lifestreamEnabled && currentregionName == huntregionname && startLocationAetheryteId != 0` (covers same-world via `ChangeWorld` no-op and cross-world via lifestream; hides the button when we have no usable destination ID even though the message is shown).

### 5. Config migration

Existing users have `TeleporterIntegration` and `teleporterEnabled` keys persisted in their JSON config. Newtonsoft tolerates extra unknown properties on deserialize by default; do not add migration code. The dropped property is just ignored on next load and never re-written.

## Components touched

| File | Change |
|------|--------|
| `Helpers/Utilities.cs` | Rewrite `ExecuteTeleport` to use `ECommonsIPC.Lifestream`; drop teleporter-only branches and the city-name massage |
| `Helpers/MapManager.cs` | Return `(uint, string)` from nearest-aetheryte; add `GetZonePrimaryAetheryte` and `LookupAetheryteByName` |
| `Helpers/HuntTrainMessage.cs` | Add `startLocationAetheryteId`; remove `teleporterEnabled` |
| `Helpers/MessageCacheManager.cs` | Drop teleporter install/integration check; pass new field through |
| `Messaging/WebSocketManagement.cs` | Replace the `if (startLocation == "invalid")` block per Section 3; drop teleporter install/integration check |
| `Configuration/Configuration.cs` | Remove `TeleporterIntegration`; keep `ctrlclickTeleport` with simplified gate |
| `Windows/ConfigWindow.cs` | Remove teleporter integration UI; simplify combined-disable conditions |
| `Windows/NotifyWindow.cs` | Simplify button-visible condition to lifestream-only |

`Services/IPCManager.cs` is not touched — it's the outbound publisher (`OnHuntTrainMessageReceived`), unrelated to consuming Lifestream.

## Out of scope

- Instance handling (`/li {instance}` / `ChangeInstance`).
- Cross-region teleport — same-region constraint stays since cross-region travel isn't possible in-game without a manual character move.
- Refactor of `ExecuteTeleport` into named sub-steps (would be a separate cleanup).
- Removing `ctrlclickTeleport` config (still useful with lifestream).

## Testing

Manual:

1. Same-world hunt with valid aetheryte name → teleports directly via `Teleport(id, 0)`.
2. Cross-world same-DC hunt → `ChangeWorld` succeeds, then `Teleport(id, 0)` after world arrival.
3. Cross-DC hunt → same as above but with `isDcTransfer=true` path inside `ChangeWorld`.
4. Hunt with `AetheriteName="invalid"` and valid coords → uses `GetNearestAetheryte`, teleports.
5. Hunt with `AetheriteName="invalid"` and no coords → uses `GetZonePrimaryAetheryte`, button still visible, teleports.
6. Lifestream not installed → button hidden in `NotifyWindow`; no errors.
7. Existing config with `TeleporterIntegration: true` → loads cleanly without teleporter integration appearing anywhere in UI.
