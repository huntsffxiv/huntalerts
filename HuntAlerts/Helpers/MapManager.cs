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

    public static (uint RowId, string Name, string ZoneName)? LookupAetheryteByNameAnywhere(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        foreach (var data in Svc.Data.GetExcelSheet<Aetheryte>())
        {
            if (!data.IsAetheryte) continue;
            if (data.PlaceName.ValueNullable == null) continue;
            var n = data.PlaceName.ValueNullable?.Name.ToString() ?? "";
            if (n.EqualsIgnoreCase(name))
            {
                var zoneName = data.Territory.ValueNullable?.PlaceName.ValueNullable?.Name.ToString() ?? "";
                return (data.RowId, n, zoneName);
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
