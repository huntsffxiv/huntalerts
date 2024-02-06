using Dalamud.Configuration;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace HuntAlerts
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        // General Settings
        public bool SuppressDuplicates { get; set; } = true;
        public int TextColor { get; set; } = 0;
        public int SRankTextColor { get; set; } = 0;
        public bool UseDalamudChat { get; set; } = true;
        public XivChatType OutputChat { get; set; } = (XivChatType)56;
        public bool OpenMapOnArrival { get; set; } = true;
        public bool EndwalkerSRank { get; set; } = false;
        public bool ShadowbringersSRank { get; set; } = false;
        public bool CenturioSRank { get; set; } = false;
        public int SoundEffect { get; set; } = 0;
        public bool TeleporterIntegration { get; set; } = false;
        public bool LifestreamIntegration { get; set; } = false;
        public bool ctrlclickTeleport {  get; set; } = false;
        public bool SRankEnabled { get; set; } = false;
        public bool SRankCurrentWorld { get; set; } = true;
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


        // Add a dictionary to map world names to their Datacenter
        [NonSerialized]
        public Dictionary<string, string> WorldDatacenterMap;

        // Add a dictionary to map Datacenters to their Region
        [NonSerialized]
        public Dictionary<string, string> DatacenterRegionMap;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;



            WorldDatacenterMap = new Dictionary<string, string>
            {
                // Aether
                { "Adamantoise", "Aether" },
                { "Cactuar", "Aether" },
                { "Faerie", "Aether" },
                { "Gilgamesh", "Aether" },
                { "Jenova", "Aether" },
                { "Midgardsormr", "Aether" },
                { "Sargatanas", "Aether" },
                { "Siren", "Aether" },

                // Crystal
                { "Balmung", "Crystal" },
                { "Brynhildr", "Crystal" },
                { "Coeurl", "Crystal" },
                { "Diabolos", "Crystal" },
                { "Goblin", "Crystal" },
                { "Malboro", "Crystal" },
                { "Mateus", "Crystal" },
                { "Zalera", "Crystal" },

                // Primal
                { "Behemoth", "Primal" },
                { "Excalibur", "Primal" },
                { "Exodus", "Primal" },
                { "Famfrit", "Primal" },
                { "Hyperion", "Primal" },
                { "Lamia", "Primal" },
                { "Leviathan", "Primal" },
                { "Ultros", "Primal" },

                // Dynamis
                { "Halicarnassus", "Dynamis" },
                { "Maduin", "Dynamis" },
                { "Marilith", "Dynamis" },
                { "Seraph", "Dynamis" },

                // Light
                { "Cerberus", "Chaos" },
                { "Louisoix", "Chaos" },
                { "Moogle", "Chaos" },
                { "Omega", "Chaos" },
                { "Phantom", "Chaos" },
                { "Ragnarok", "Chaos" },
                { "Sagittarius", "Chaos" },
                { "Spriggan", "Chaos" },

                // Chaos
                { "Alpha", "Light" },
                { "Lich", "Light" },
                { "Odin", "Light" },
                { "Phoenix", "Light" },
                { "Raiden", "Light" },
                { "Shiva", "Light" },
                { "Twintania", "Light" },
                { "Zodiark", "Light" },

                // Add mappings for all worlds in their respective data centers
            };

            DatacenterRegionMap = new Dictionary<string, string>
            {
                // NA
                { "Aether", "NA" },
                { "Crystal", "NA" },
                { "Primal", "NA" },
                { "Dynamis", "NA" },
                
                // EU
                { "Light", "EU" },
                { "Chaos", "EU" },

                // Add mappings for all Datacenters in their respective Regions
            };
        }

        public void Save()
        {
            this.PluginInterface!.SavePluginConfig(this);
        }
    }

    public sealed partial class HuntAlerts
    {
        public string serverURI = "wss://huntalerts.pro:24842";
        //public string serverURI = "ws://localhost:6789";
    }
}
