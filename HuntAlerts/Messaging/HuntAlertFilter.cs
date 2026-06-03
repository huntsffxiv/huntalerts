using HuntAlerts.Helpers;

namespace HuntAlerts.Messaging;

internal static class HuntAlertFilter
{
    public static bool IsDataCenterEnabled(Configuration config, string dataCenter) =>
        !string.IsNullOrEmpty(dataCenter) && config.EnabledDatacenters.Contains(dataCenter);

    public static bool IsWorldEnabled(Configuration config, string world) =>
        !string.IsNullOrEmpty(world) && config.EnabledWorlds.Contains(world);

    public static bool IsSRankDataCenterEnabled(Configuration config, string dataCenter) =>
        !string.IsNullOrEmpty(dataCenter) && config.EnabledSRankDatacenters.Contains(dataCenter);

    public static bool IsSRankWorldEnabled(Configuration config, string world) =>
        !string.IsNullOrEmpty(world) && config.EnabledSRankWorlds.Contains(world);

    public static bool IsTrainGroupEnabled(Configuration config, string huntKinds)
    {
        if (string.IsNullOrEmpty(huntKinds)) return false;
        foreach (var raw in huntKinds.Split(','))
        {
            var name = NormalizeGroup(raw.Trim());
            if (name != null && config.EnabledTrainGroups.Contains(name)) return true;
        }
        return false;
    }

    public static bool IsSRankGroupEnabled(Configuration config, string huntKinds)
    {
        if (string.IsNullOrEmpty(huntKinds)) return false;
        foreach (var raw in huntKinds.Split(','))
        {
            var name = NormalizeGroup(raw.Trim());
            if (name != null && config.EnabledSRankGroups.Contains(name)) return true;
        }
        return false;
    }

    public static string? NormalizeGroup(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return s.Trim().ToUpperInvariant() switch
        {
            "DAWNTRAIL" or "DT"                          => HuntGroups.Dawntrail,
            "ENDWALKER" or "EW"                          => HuntGroups.Endwalker,
            "SHADOWBRINGERS" or "SHB"                    => HuntGroups.Shadowbringers,
            "CENTURIO"                                    => HuntGroups.Centurio,
            "STORMBLOOD" or "SB"                         => HuntGroups.Centurio,
            "HEAVENSWARD" or "HW"                        => HuntGroups.Centurio,
            "ARR" or "A REALM REBORN" or "REALM REBORN"  => HuntGroups.Centurio,
            _ => null,
        };
    }

    public static string? DatacenterOf(string worldName) =>
        WorldData.TryGetWorld(worldName, out var info) ? info.Datacenter : null;
}
