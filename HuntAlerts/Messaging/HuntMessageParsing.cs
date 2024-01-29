using ECommons.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;


namespace HuntAlerts
{
    public sealed partial class HuntAlerts
    {
        public class HuntMessage
        {
            public string Type { get; set; }
            public string Content { get; set; }
            public string World { get; set; }
            public string Kind { get; set; }
            public long Posted_Epoch { get; set; }
            public string CreatureName { get; set; }
            public string LocationName { get; set; }
            public string LocationCoords { get; set; }
            public string AetheriteName { get; set; }
            public long DeathTime { get; set; }
            public Dictionary<string, object> AdditionalData { get; set; }
        }
        public static string ParseForStartZone(string message)
        {
            // Define a dictionary mapping keywords to corresponding values
            var keywordMap = new Dictionary<string, string>
            {
                // EW
                { "mare", "Mare Lamentorum" },
                { "thule", "Ultima Thule" },
                { "thav", "Thavnair" },
                { "elpis", "Elpis" },
                { "garlemald", "Garlemald" },
                { "laby", "Labyrinthos" },

                // SHB
                
                { "lakeland", "Lakeland" },
                { "kholusia", "Kholusia" },
                { "araeng", "Amh Araeng" },
                { "mheg", "Il Mheg" },
                { "greatwood", "The Rak'tika Greatwood" },
                { "tempest", "The Tempest" },
                

                // SB
                { "fringes", "The Fringes" },
                { "ruby sea", "The Ruby Sea" },
                { "azim", "The Azim Steppe" },
                { "lochs", "The Lochs" },
                { "peaks", "The Peaks" },


                // HW
                { "sea of clouds", "The Sea of Clouds" },
                { "azys", "Azys Lla" },
                { "forelands", "The Dravanian Forelands" },
                { "mists", "The Churning Mists" },
            };

            // Convert message to lower case for case-insensitive comparison
            string lowerMessage = message.ToLower();

            // Find which keywords are in the input string
            var foundKeywords = keywordMap.Keys.Where(keyword => lowerMessage.Contains(keyword)).ToList();

            // Prepare the result string
            string result;

            if (foundKeywords.Count > 0)
            {
                // Get the corresponding value from the dictionary
                result = keywordMap[foundKeywords.First()];
            }
            else
            {
                result = "invalid";
            }

            return result;
        }
        public static (float?, float?) ExtractCoordinates(string message)
        {
            try {
                // Regex adjusted to include hyphen as a delimiter
                var regex = new Regex(@"\b(-?\d+(\.\d+)?)(\s*,\s*|\s+|\s*-\s*)(-?\d+(\.\d+)?)\b");
                var match = regex.Match(message);

                if (match.Success)
                {
                    // Only processing the first match found
                    float.TryParse(match.Groups[1].Value, out float x);
                    float.TryParse(match.Groups[4].Value, out float y);
                    return (x, y);
                }

                return (null, null);
            }
            catch(Exception ex)
            {
                return (0, 0);
            }
        }


        public static string ParseForStartLocation(string message)
        {
            // Define a dictionary mapping keywords to corresponding values
            var keywordMap = new Dictionary<string, string>
            {
                { "fort", "Fort Jobb" },
                { "foot", "Fort Jobb" },
                { "jobb", "Fort Jobb" },
                { "ostall", "The Ostall Imperative" },
                { "great work", "The Great Work" },
                { "palaka", "Palaka's Stand" },
                { "yedli", "Yedlihmad" },
                { "castrum", "Castrum Oriens" },
                { "camp broken", "Camp Broken Glass" },
                { "sinus", "Sinus Lacrimarum" },
                { "tertium", "Tertium" },
                { "anag", "Anagnorisis" },
                { "wonder", "The Twelve Wonders" },
                { "poie", "Poieten Oikos" },
                { "apor", "Aporia" },
                { "arche", "The Archeion" },
                { "haml", "Sharlayan Hamlet" },
                { "ondo", "The Ondo Cups" },
                { "lydha", "Lydha Lran" },
                { "slither", "Slitherbough" },
                { "fanow", "Fanow" },
                { "twine", "Twine" },
                { "mord", "Mord Souq" },
                { "inn at journey", "The Inn at Journey's Head" },
                { "tomra", "Tomra" },
                { "wright", "Wright" },
                { "stilltide", "Stilltide" },
                { "wole", "Wolekdorf" },
                { "peering", "The Peering Stones" },
                { "gannh", "Ala Gannha" },
                { "ghiri", "Ala Ghiri" },
                { "porta", "Porta Praetoria" },
                { "quarter", "The Ala Mhigan Quarter" },
                { "onokoro", "Onokoro" },
                { "tama", "Tamamizu" },
                { "house", "The House of the Fierce" },
                { "dhoro", "Dhoro Iloh" },
                { "reunion", "Reunion" },
                { "throne", "The Dawn Throne" },
                { "omicron", "Base Omicron" },

            };

            // Convert message to lower case for case-insensitive comparison
            string lowerMessage = message.ToLower();

            // Find which keywords are in the input string
            var foundKeywords = keywordMap.Keys.Where(keyword => lowerMessage.Contains(keyword)).ToList();

            // Prepare the result string
            string result;

            if (foundKeywords.Count > 0)
            {
                // Get the corresponding value from the dictionary
                result = keywordMap[foundKeywords.First()];
                PluginLog.Verbose($"StartLocation Matched Keyword: {foundKeywords.First()}");
            }
            else
            {
                result = "invalid";
            }

            return result;
        }

    }

    
}
