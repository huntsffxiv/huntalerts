using System.Linq;
using HuntAlerts.Helpers;

namespace HuntAlerts
{
    public sealed partial class HuntAlerts
    {
        public Configuration Configuration { get; init; }

        private bool IsDataCenterEnabled(string dataCenter) =>
            !string.IsNullOrEmpty(dataCenter) && Configuration.EnabledDatacenters.Contains(dataCenter);

        private bool IsWorldEnabled(string world) =>
            !string.IsNullOrEmpty(world) && Configuration.EnabledWorlds.Contains(world);

        private bool IsTrainGroupEnabled(string huntKinds)
        {
            if (string.IsNullOrEmpty(huntKinds)) return false;
            foreach (var raw in huntKinds.Split(','))
            {
                var name = NormalizeGroup(raw.Trim());
                if (name != null && Configuration.EnabledTrainGroups.Contains(name)) return true;
            }
            return false;
        }

        private bool IsSRankGroupEnabled(string huntKinds)
        {
            if (string.IsNullOrEmpty(huntKinds)) return false;
            foreach (var raw in huntKinds.Split(','))
            {
                var name = NormalizeGroup(raw.Trim());
                if (name != null && Configuration.EnabledSRankGroups.Contains(name)) return true;
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
}
