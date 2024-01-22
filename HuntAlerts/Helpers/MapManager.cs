using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Logging;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Linq;

namespace HuntAlerts.Helpers;
public static class MapManager
{
    public static void OpenMapWithMarker(uint territoryType, float x, float y)
    {
        var map = Svc.Data.GetExcelSheet<TerritoryType>().GetRow(territoryType).Map.Row;
        var linkPayload = new MapLinkPayload(territoryType, map, x, y);
        Svc.GameGui.OpenMapWithMapLink(linkPayload);
    }

    public static string GetNearestAetheryte(uint territoryType, float x, float y)
    {
        var aetheryteName = "";
        double distance = 0;
        foreach (var data in Svc.Data.GetExcelSheet<Aetheryte>())
        {
            if (!data.IsAetheryte) continue;
            if (data.Territory.Value == null) continue;
            if (data.PlaceName.Value == null) continue;
            if (Svc.Data.GetExcelSheet<Map>().TryGetFirst(m => m.TerritoryType.Row == territoryType, out var place))
            {
                var scale = place.SizeFactor;
                if (data.Territory.Value.RowId == territoryType)
                {
                    var mapMarker = Svc.Data.GetExcelSheet<MapMarker>().FirstOrDefault(m => m.DataType == 3 && m.DataKey == data.RowId);
                    if (mapMarker == null)
                    {
                        DuoLog.Error($"Cannot find aetherytes position for {territoryType}#{data.PlaceName.Value.Name}");
                        continue;
                    }
                    var AethersX = ConvertMapMarkerToMapCoordinate(mapMarker.X, scale);
                    var AethersY = ConvertMapMarkerToMapCoordinate(mapMarker.Y, scale);
                    PluginLog.Debug($"Aetheryte: {data.PlaceName.Value.Name} ({AethersX} ,{AethersY})");
                    var temp_distance = Math.Pow(AethersX - x, 2) + Math.Pow(AethersY - y, 2);
                    if (aetheryteName == "" || temp_distance < distance)
                    {
                        distance = temp_distance;
                        aetheryteName = data.PlaceName.Value.Name;
                    }
                }
            }
        }
        return aetheryteName;
    }

    public static string GetNearestAetheryte(MapLinkPayload maplinkMessage)
    {
        var aetheryteName = "";
        double distance = 0;
        foreach (var data in Svc.Data.GetExcelSheet<Aetheryte>())
        {
            if (!data.IsAetheryte) continue;
            if (data.Territory.Value == null) continue;
            if (data.PlaceName.Value == null) continue;
            if (Svc.Data.GetExcelSheet<Map>().TryGetFirst(m => m.TerritoryType.Row == maplinkMessage.TerritoryType.RowId, out var place))
            {
                var scale = place.SizeFactor;
                if (data.Territory.Value.RowId == maplinkMessage.TerritoryType.RowId)
                {
                    var mapMarker = Svc.Data.GetExcelSheet<MapMarker>().FirstOrDefault(m => m.DataType == 3 && m.DataKey == data.RowId);
                    if (mapMarker == null)
                    {
                        DuoLog.Error($"Cannot find aetherytes position for {maplinkMessage.PlaceName}#{data.PlaceName.Value.Name}");
                        continue;
                    }
                    var AethersX = ConvertMapMarkerToMapCoordinate(mapMarker.X, scale);
                    var AethersY = ConvertMapMarkerToMapCoordinate(mapMarker.Y, scale);
                    PluginLog.Debug($"Aetheryte: {data.PlaceName.Value.Name} ({AethersX} ,{AethersY})");
                    var temp_distance = Math.Pow(AethersX - maplinkMessage.XCoord, 2) + Math.Pow(AethersY - maplinkMessage.YCoord, 2);
                    if (aetheryteName == "" || temp_distance < distance)
                    {
                        distance = temp_distance;
                        aetheryteName = data.PlaceName.Value.Name;
                    }
                }
            }
        }
        return aetheryteName;
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
