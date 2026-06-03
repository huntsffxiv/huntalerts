using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using System;
using ECommons.DalamudServices;
using System.Collections.Generic;

namespace HuntAlerts
{
    public enum ScopeMode
    {
        AllConfigured,
        CurrentDatacenterOnly,
        CurrentWorldOnly,
        HomeWorldOnly,
    }

    public static class HuntGroups
    {
        public const string Centurio       = "Centurio";
        public const string Shadowbringers = "Shadowbringers";
        public const string Endwalker      = "Endwalker";
        public const string Dawntrail      = "Dawntrail";

        public static readonly string[] All =
        {
            Centurio, Shadowbringers, Endwalker, Dawntrail,
        };
    }

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 3;

        public bool SuppressDuplicates { get; set; } = true;
        public int TextColor { get; set; } = 57;
        public int SRankTextColor { get; set; } = 48;
        public int SRankKillTextColor { get; set; } = 16;
        public bool UseDalamudChat { get; set; } = true;
        public XivChatType OutputChat { get; set; } = (XivChatType)56;
        public bool OpenMapOnArrival { get; set; } = true;
        public int SoundEffect { get; set; } = 0;

        public bool LifestreamIntegration { get; set; } = false;
        public bool ctrlclickTeleport { get; set; } = false;

        public ScopeMode Scope { get; set; } = ScopeMode.AllConfigured;

        public HashSet<string> EnabledTrainGroups { get; set; } = new();
        public HashSet<string> EnabledDatacenters { get; set; } = new();
        public HashSet<string> EnabledWorlds      { get; set; } = new();

        public bool SRankEnabled { get; set; } = true;
        public ScopeMode SRankScope { get; set; } = ScopeMode.CurrentDatacenterOnly;
        public HashSet<string> EnabledSRankGroups       { get; set; } = new();
        public HashSet<string> EnabledSRankDatacenters  { get; set; } = new();
        public HashSet<string> EnabledSRankWorlds       { get; set; } = new();

        public int SnoozeDefaultMinutes { get; set; } = 30;
        public string DefaultRelayChannel { get; set; } = "/p";

        [NonSerialized]
        private IDalamudPluginInterface? PluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            PluginInterface = pluginInterface;
        }

        public void Save()
        {
            Svc.PluginInterface.SavePluginConfig(this);
        }
    }

    public sealed partial class HuntAlerts
    {
        public string serverURI = "wss://huntalerts.pro:24842";
    }
}
