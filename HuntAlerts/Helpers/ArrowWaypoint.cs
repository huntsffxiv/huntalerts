using System;

namespace HuntAlerts.Helpers;

public static class ArrowWaypoint
{
    public static uint   TerritoryTypeId { get; private set; }
    public static float  MapX            { get; private set; }
    public static float  MapY            { get; private set; }
    public static string Source          { get; private set; } = "";
    public static string WorldName       { get; private set; } = "";
    public static bool   Forced          { get; private set; }

    private static long _expiresAt;
    private const int ExpirySeconds = 30 * 60;

    public static bool IsActive =>
        TerritoryTypeId != 0 && Environment.TickCount64 <= _expiresAt;

    public static void Set(uint territoryTypeId, float mapX, float mapY, string source, string worldName = "", bool force = false)
    {
        if (territoryTypeId == 0) return;
        if (mapX == 0f && mapY == 0f) return;
        TerritoryTypeId = territoryTypeId;
        MapX            = mapX;
        MapY            = mapY;
        Source          = source;
        WorldName       = worldName ?? "";
        Forced          = force;
        _expiresAt      = Environment.TickCount64 + ExpirySeconds * 1000;
    }

    public static void Clear()
    {
        TerritoryTypeId = 0;
        Forced          = false;
    }
}
