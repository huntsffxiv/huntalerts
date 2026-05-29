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
                if (existing != null && existing.Version >= 2)
                    return existing;
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
                foreach (var w in dc.Worlds) c.EnabledWorlds.Add(w);
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
