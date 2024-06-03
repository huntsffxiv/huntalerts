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


        public static (float?, float?) ExtractCoordinates(string message)
        {
            try {
                // Regex adjusted to include hyphen as a delimiter
                var regex = CoordsRegex();
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
                PluginLog.Verbose($"Could not parse coordinates: {ex}");
                return (0, 0);
            }
        }

        [GeneratedRegex(@"\b(-?\d+(\.\d+)?)(\s*,\s*|\s+|\s*-\s*)(-?\d+(\.\d+)?)\b")]
        private static partial Regex CoordsRegex();
    }



}
