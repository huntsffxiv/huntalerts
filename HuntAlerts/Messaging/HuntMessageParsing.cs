using ECommons.Logging;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HuntAlerts.Messaging;

public static partial class HuntMessageParsing
{
    public static (float?, float?) ExtractCoordinates(string message)
    {
        try
        {
            var match = CoordsRegex().Match(message);

            if (match.Success)
            {
                float.TryParse(match.Groups[1].Value, NumberFormatInfo.InvariantInfo, out float x);
                float.TryParse(match.Groups[4].Value, NumberFormatInfo.InvariantInfo, out float y);
                return (x, y);
            }

            return (null, null);
        }
        catch (Exception ex)
        {
            PluginLog.Verbose($"Could not parse coordinates: {ex}");
            return (0, 0);
        }
    }

    [GeneratedRegex(@"\b(-?\d+(\.\d+)?)(\s*,\s*|\s+|\s*-\s*)(-?\d+(\.\d+)?)\b")]
    private static partial Regex CoordsRegex();

    public static string StripLocalized(string s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        var idx = s.IndexOf('［');
        if (idx < 0) idx = s.IndexOf('[');
        if (idx >= 0) s = s.Substring(0, idx);
        return s.Trim();
    }
}
