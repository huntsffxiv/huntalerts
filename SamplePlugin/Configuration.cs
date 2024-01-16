using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace HuntAlerts
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
        public bool Light { get; set; } = false;
        public bool Chaos { get; set; } = false;
        public int soundEffect { get; set; } = 0;
        public int MaxLineLength { get; set; } = 200;

        // Aether World Options
        public bool MidgardsormrWorld { get; set; } = true;
        public bool FaerieWorld { get; set; } = true;
        public bool JenovaWorld { get; set; } = true;
        public bool CactuarWorld { get; set; } = true;
        public bool SargatanasWorld { get; set; } = true;
        public bool AdamantoiseWorld { get; set; } = true;
        public bool SirenWorld { get; set; } = true;
        public bool GilgameshWorld { get; set; } = true;

        // Primal World Options
        public bool BehemothWorld { get; set; } = true;
        public bool ExcaliburWorld { get; set; } = true;
        public bool ExodusWorld { get; set; } = true;
        public bool FamfritWorld { get; set; } = true;
        public bool HyperionWorld { get; set; } = true;
        public bool LamiaWorld { get; set; } = true;
        public bool LeviathanWorld { get; set; } = true;
        public bool UltrosWorld { get; set; } = true;

        // Crystal World Options
        public bool BalmungWorld { get; set; } = true;
        public bool BrynhildrWorld { get; set; } = true;
        public bool CoeurlWorld { get; set; } = true;
        public bool DiabolosWorld { get; set; } = true;
        public bool GoblinWorld { get; set; } = true;
        public bool MalboroWorld { get; set; } = true;
        public bool MateusWorld { get; set; } = true;
        public bool ZaleraWorld { get; set; } = true;

        // Dynamis World Options
        public bool HalicarnassusWorld { get; set; } = true;
        public bool MaduinWorld { get; set; } = true;
        public bool MarilithWorld { get; set; } = true;
        public bool SeraphWorld { get; set; } = true;

        // Chaos World Options
        public bool CerberusWorld { get; set; } = true;
        public bool LouisoixWorld { get; set; } = true;
        public bool MoogleWorld { get; set; } = true;
        public bool OmegaWorld { get; set; } = true;
        public bool PhantomWorld { get; set; } = true;
        public bool RagnarokWorld { get; set; } = true;
        public bool SagittariusWorld { get; set; } = true;
        public bool SprigganWorld { get; set; } = true;

        // Light World Options
        public bool AlphaWorld { get; set; } = true;
        public bool LichWorld { get; set; } = true;
        public bool OdinWorld { get; set; } = true;
        public bool PhoenixWorld { get; set; } = true;
        public bool RaidenWorld { get; set; } = true;
        public bool ShivaWorld { get; set; } = true;
        public bool TwintaniaWorld { get; set; } = true;
        public bool ZodiarkWorld { get; set; } = true;


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
