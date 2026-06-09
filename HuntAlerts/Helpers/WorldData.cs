using System;
using System.Collections.Generic;
using ECommons.DalamudServices;
using ECommons.Logging;
using Lumina.Excel.Sheets;

namespace HuntAlerts.Helpers;

public static class WorldData
{
    public sealed record DatacenterInfo(string Name, RegionId Region, IReadOnlyList<string> Worlds);
    public sealed record WorldInfo(string Name, string Datacenter, RegionId Region);

    public enum RegionId
    {
        Unknown = 0,
        JP = 1,
        NA = 2,
        EU = 3,
        OCE = 4,
    }

    private static IReadOnlyDictionary<string, WorldInfo>? _worldByName;
    private static IReadOnlyList<DatacenterInfo>? _datacenters;
    private static readonly object _lock = new();

    public static IReadOnlyDictionary<string, WorldInfo> WorldByName
    {
        get { EnsureLoaded(); return _worldByName!; }
    }

    public static IReadOnlyList<DatacenterInfo> DatacentersInOrder
    {
        get { EnsureLoaded(); return _datacenters!; }
    }

    public static bool TryGetWorld(string name, out WorldInfo info)
    {
        if (!string.IsNullOrEmpty(name) && WorldByName.TryGetValue(name, out var w))
        {
            info = w;
            return true;
        }
        info = null!;
        return false;
    }

    public static string RegionLabel(RegionId r) => r switch
    {
        RegionId.JP  => "JP",
        RegionId.NA  => "NA",
        RegionId.EU  => "EU",
        RegionId.OCE => "OCE",
        _ => "??",
    };

    private static void EnsureLoaded()
    {
        if (_worldByName != null) return;
        lock (_lock)
        {
            if (_worldByName != null) return;
            try
            {
                var worldSheet = Svc.Data.GetExcelSheet<World>();
                var dcSheet    = Svc.Data.GetExcelSheet<WorldDCGroupType>();
                if (worldSheet == null || dcSheet == null)
                {
                    PluginLog.Warning("WorldData: Excel sheets unavailable; falling back to empty world set.");
                    _worldByName = new Dictionary<string, WorldInfo>();
                    _datacenters = Array.Empty<DatacenterInfo>();
                    return;
                }

                var worldsByDcRow = new Dictionary<uint, List<string>>();
                var dcMeta = new Dictionary<uint, (string Name, RegionId Region)>();

                foreach (var w in worldSheet)
                {
                    if (!w.IsPublic) continue;
                    var name = w.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (w.DataCenter.ValueNullable is not { } dcRef) continue;

                    var dcRowId = dcRef.RowId;
                    var dcName = dcRef.Name.ExtractText();
                    if (string.IsNullOrWhiteSpace(dcName)) continue;

                    if (dcName.Contains("Cloud", StringComparison.OrdinalIgnoreCase)) continue;

                    var region = (RegionId)dcRef.Region.RowId;

                    if (region == RegionId.Unknown) continue;


                    if (!dcMeta.ContainsKey(dcRowId))
                        dcMeta[dcRowId] = (dcName, region);

                    if (!worldsByDcRow.TryGetValue(dcRowId, out var bucket))
                    {
                        bucket = [];
                        worldsByDcRow[dcRowId] = bucket;
                    }
                    bucket.Add(name);
                }

                var datacenters = new List<DatacenterInfo>();
                var worldMap = new Dictionary<string, WorldInfo>(StringComparer.Ordinal);
                foreach (var (dcRowId, worlds) in worldsByDcRow)
                {
                    var (dcName, region) = dcMeta[dcRowId];
                    worlds.Sort(StringComparer.Ordinal);
                    datacenters.Add(new DatacenterInfo(dcName, region, worlds));
                    foreach (var wname in worlds)
                        worldMap[wname] = new WorldInfo(wname, dcName, region);
                }
                datacenters.Sort((a, b) =>
                {
                    var byRegion = a.Region.CompareTo(b.Region);
                    return byRegion != 0 ? byRegion : string.Compare(a.Name, b.Name, StringComparison.Ordinal);
                });

                _worldByName = worldMap;
                _datacenters = datacenters;
                PluginLog.Information($"WorldData: loaded {worldMap.Count} worlds across {datacenters.Count} datacenters.");
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"WorldData: load failed ({ex.Message}); using empty fallback.");
                _worldByName = new Dictionary<string, WorldInfo>();
                _datacenters = Array.Empty<DatacenterInfo>();
            }
        }
    }
}
