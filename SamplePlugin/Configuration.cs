using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace HuntAlert
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool EndwalkerHunts { get; set; } = false;
        public bool ShadowbringersHunts { get; set; } = false;
        public bool CenturioHunts { get; set; } = false;
        public bool Aether { get; set; } = false;
        public bool HomeWorldOnly { get; set; } = false;
        public bool CurrentWorldOnly { get; set; } = false;
        public bool Primal { get; set; } = false;
        public bool Crystal { get; set; } = false;
        public bool Dynamis { get; set; } = false;
        public int soundEffect { get; set; } = 0;

        // the below exist just to make saving less cumbersome
        [NonSerialized]
        private DalamudPluginInterface? PluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.PluginInterface!.SavePluginConfig(this);
        }
    }
}
