using ECommons.Logging;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace HuntAlerts.Messaging;

public static class HuntMessageFormatting
{
    public static string ReplaceTimestampsWithLocalTime(string input)
    {
        const string pattern = @"<t:(\d+):(t|T|d|D|f|F|R)>";

        return Regex.Replace(input, pattern, match =>
        {
            long unixTimestamp = long.Parse(match.Groups[1].Value);
            return ConvertTime(unixTimestamp);
        });
    }

    public static string RemoveDiscordEmojis(string input)
    {
        const string customEmojiPattern = @"<a?:(\w+):\d+>";
        input = Regex.Replace(input, customEmojiPattern, "");

        const string standardEmojiPattern = @":\w+:";
        return Regex.Replace(input, standardEmojiPattern, "");
    }

    public static string ConvertTime(long epochTime)
    {
        DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(epochTime).ToLocalTime().DateTime;

        PluginLog.Verbose($"Posted Time:  {epochTime}");
        string convertedTime = dateTime.ToString("hh:mm tt", CultureInfo.GetCultureInfo("en-US"));
        PluginLog.Verbose($"converted time: {convertedTime}");
        return convertedTime;
    }
}
