using System.Linq;

namespace HuntAlerts
{
    public sealed partial class HuntAlerts
    {
        public Configuration Configuration { get; init; }
        private bool IsDataCenterEnabled(string dataCenter)
        {
            return dataCenter switch
            {
                "Aether" => this.Configuration.Aether,
                "Primal" => this.Configuration.Primal,
                "Crystal" => this.Configuration.Crystal,
                "Dynamis" => this.Configuration.Dynamis,
                "Light" => this.Configuration.Light,
                "Chaos" => this.Configuration.Chaos,
                _ => false,
            };
        }
        private bool IsWorldEnabled(string world)
        {
            return world switch
            {
                // Aether
                "Midgardsormr" => this.Configuration.MidgardsormrWorld,
                "Faerie" => this.Configuration.FaerieWorld,
                "Jenova" => this.Configuration.JenovaWorld,
                "Cactuar" => this.Configuration.CactuarWorld,
                "Sargatanas" => this.Configuration.SargatanasWorld,
                "Adamantoise" => this.Configuration.AdamantoiseWorld,
                "Siren" => this.Configuration.SirenWorld,
                "Gilgamesh" => this.Configuration.GilgameshWorld,

                // Primal
                "Behemoth" => this.Configuration.BehemothWorld,
                "Excalibur" => this.Configuration.ExcaliburWorld,
                "Exodus" => this.Configuration.ExodusWorld,
                "Famfrit" => this.Configuration.FamfritWorld,
                "Hyperion" => this.Configuration.HyperionWorld,
                "Lamia" => this.Configuration.LamiaWorld,
                "Leviathan" => this.Configuration.LeviathanWorld,
                "Ultros" => this.Configuration.UltrosWorld,

                // Crystal
                "Balmung" => this.Configuration.BalmungWorld,
                "Brynhildr" => this.Configuration.BrynhildrWorld,
                "Coeurl" => this.Configuration.CoeurlWorld,
                "Diabolos" => this.Configuration.DiabolosWorld,
                "Goblin" => this.Configuration.GoblinWorld,
                "Malboro" => this.Configuration.MalboroWorld,
                "Mateus" => this.Configuration.MateusWorld,
                "Zalera" => this.Configuration.ZaleraWorld,

                // Dynamis
                "Halicarnassus" => this.Configuration.HalicarnassusWorld,
                "Maduin" => this.Configuration.MaduinWorld,
                "Marilith" => this.Configuration.MarilithWorld,
                "Seraph" => this.Configuration.SeraphWorld,

                // Chaos
                "Cerberus" => this.Configuration.CerberusWorld,
                "Louisoix" => this.Configuration.LouisoixWorld,
                "Moogle" => this.Configuration.MoogleWorld,
                "Omega" => this.Configuration.OmegaWorld,
                "Phantom" => this.Configuration.PhantomWorld,
                "Ragnarok" => this.Configuration.RagnarokWorld,
                "Sagittarius" => this.Configuration.SagittariusWorld,
                "Spriggan" => this.Configuration.SprigganWorld,

                // Light
                "Alpha" => this.Configuration.AlphaWorld,
                "Lich" => this.Configuration.LichWorld,
                "Odin" => this.Configuration.OdinWorld,
                "Phoenix" => this.Configuration.PhoenixWorld,
                "Raiden" => this.Configuration.RaidenWorld,
                "Shiva" => this.Configuration.ShivaWorld,
                "Twintania" => this.Configuration.TwintaniaWorld,
                "Zodiark" => this.Configuration.ZodiarkWorld,
                _ => false,
            };
        }
        private bool IsHuntEnabled(string huntKinds)
        {
            // Split the huntKinds string by comma and trim any whitespace
            var huntTypes = huntKinds.Split(',').Select(h => h.Trim());

            // Check if any of the hunt types are enabled in the configuration
            foreach (var huntType in huntTypes)
            {
                switch (huntType)
                {
                    case "Endwalker":
                        if (this.Configuration.EndwalkerHunts) return true;
                        break;
                    case "Shadowbringers":
                        if (this.Configuration.ShadowbringersHunts) return true;
                        break;
                    case "Centurio":
                        if (this.Configuration.CenturioHunts) return true;
                        break;
                        // Add cases for other hunt types as necessary
                }
            }

            // Return false if none of the hunt types are enabled
            return false;
        }
    }
}
