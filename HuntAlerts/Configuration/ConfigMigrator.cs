using System;
using System.IO;
using ECommons.DalamudServices;
using ECommons.IPC;
using ECommons.Logging;
using HuntAlerts.Helpers;

namespace HuntAlerts
{
    internal static class ConfigMigrator
    {
        private const string HistoryFileName = "HuntAlerts.History.json";

        public static Configuration LoadOrMigrate()
        {
            try
            {
                var existing = Svc.PluginInterface.GetPluginConfig() as Configuration;
                if (existing != null)
                {
                    if (existing.Version >= 3) return existing;

                    if (existing.Version == 2)
                    {
                        MigrateV2ToV3(existing);
                        existing.Save();
                        return existing;
                    }
                }
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"HuntAlerts: incompatible saved config detected ({ex.Message}); wiping and starting fresh.");
            }

            WipeFiles();

            var fresh = new Configuration();
            ApplyDefaults(fresh);
            fresh.Save();
            return fresh;
        }

        private static void MigrateV2ToV3(Configuration c)
        {
            c.EnabledSRankDatacenters ??= [];
            c.EnabledSRankWorlds      ??= [];

            foreach (var dc in WorldData.DatacentersInOrder)
            {
                c.EnabledSRankDatacenters.Add(dc.Name);
                foreach (var w in dc.Worlds) c.EnabledSRankWorlds.Add(w);
            }

            c.SRankScope = ScopeMode.CurrentDatacenterOnly;
            c.Version    = 3;
            PluginLog.Information("HuntAlerts: migrated config v2 → v3 (S Rank world/DC selection added; train and general settings preserved).");
        }

        private static void ApplyDefaults(Configuration c)
        {
            foreach (var g in HuntGroups.All)
            {
                c.EnabledTrainGroups.Add(g);
                c.EnabledSRankGroups.Add(g);
            }

            foreach (var dc in WorldData.DatacentersInOrder)
            {
                c.EnabledDatacenters.Add(dc.Name);
                c.EnabledSRankDatacenters.Add(dc.Name);
                foreach (var w in dc.Worlds)
                {
                    c.EnabledWorlds.Add(w);
                    c.EnabledSRankWorlds.Add(w);
                }
            }

            if (ECommonsIPC.Lifestream.Available)
                c.LifestreamIntegration = true;
        }

        private static void WipeFiles()
        {
            DeleteIfExists(Svc.PluginInterface.ConfigFile.FullName);
            DeleteIfExists(Path.Combine(Svc.PluginInterface.ConfigDirectory.FullName, HistoryFileName));
        }

        private static void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    PluginLog.Information($"HuntAlerts: deleted incompatible file: {path}");
                }
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"HuntAlerts: failed to delete '{path}': {ex.Message}");
            }
        }
    }
}
