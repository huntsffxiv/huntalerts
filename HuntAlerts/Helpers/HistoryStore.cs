using System;
using System.IO;
using ECommons.DalamudServices;
using ECommons.Logging;
using Newtonsoft.Json;

namespace HuntAlerts.Helpers;

internal sealed class PersistedCache
{
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;
    public int Capacity { get; set; }
    public int CommandCount { get; set; }
    public HuntTrainMessage?[] Slots { get; set; } = Array.Empty<HuntTrainMessage?>();
}

internal static class HistoryStore
{
    private const string FileName = "HuntAlerts.History.json";

    private static string FilePath =>
        Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, FileName);

    public static PersistedCache? TryLoad()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var json = File.ReadAllText(FilePath);
            var doc  = JsonConvert.DeserializeObject<PersistedCache>(json);
            if (doc == null || doc.Slots == null) return null;
            if (doc.Version != PersistedCache.CurrentVersion)
            {
                PluginLog.Verbose($"HistoryStore: schema version mismatch (got {doc.Version}, want {PersistedCache.CurrentVersion}); discarding old cache.");
                try { File.Delete(FilePath); } catch { }
                return null;
            }
            return doc;
        }
        catch (Exception ex)
        {
            PluginLog.Warning($"HistoryStore: load failed ({ex.Message}).");
            return null;
        }
    }

    public static void Save(int capacity, int commandCount, HuntTrainMessage?[] slots)
    {
        try
        {
            Svc.PluginInterface.ConfigDirectory.Create();
            var doc  = new PersistedCache { Capacity = capacity, CommandCount = commandCount, Slots = slots };
            var json = JsonConvert.SerializeObject(doc, Formatting.None);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            PluginLog.Warning($"HistoryStore: save failed ({ex.Message}).");
        }
    }
}
