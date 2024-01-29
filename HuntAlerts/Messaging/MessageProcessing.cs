
using ECommons.Logging;
using System;
using System.Text.RegularExpressions;


namespace HuntAlerts
{
    public sealed partial class HuntAlerts
    {
        private static string ReplaceTimestampsWithLocalTime(string input)
        {
            // Regex pattern to find timestamps
            string pattern = @"<t:(\d+):(t|T|d|D|f|F|R)>";

            // Replace each match in the input string
            return Regex.Replace(input, pattern, match =>
            {
                // Extract the Unix timestamp from the match
                long unixTimestamp = long.Parse(match.Groups[1].Value);

                string time = ConvertTime(unixTimestamp);

                // Convert Unix timestamp to DateTime
                //DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).ToLocalTime().DateTime;

                // Format the DateTime as needed, e.g., "MM/dd/yyyy HH:mm:ss"
                //return dateTime.ToString("g"); // or any other format
                return time;
            });
        }
        private static string RemoveDiscordEmojis(string input)
        {
            // Regex pattern to find Discord custom emojis
            string customEmojiPattern = @"<a?:(\w+):\d+>";
            input = Regex.Replace(input, customEmojiPattern, "");

            // Regex pattern to find standard emojis (word within colons)
            string standardEmojiPattern = @":\w+:";
            return Regex.Replace(input, standardEmojiPattern, "");
        }
        public static string ConvertTime(long epochTime)
        {

            // Convert Unix timestamp to DateTime
            DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(epochTime).ToLocalTime().DateTime;

            // Format the DateTime as needed, e.g., "MM/dd/yyyy HH:mm:ss"
            PluginLog.Verbose($"Posted Time:  {epochTime}");
            string convertedTime = dateTime.ToString("hh:mm tt"); // or any other format
            PluginLog.Verbose($"converted time: {convertedTime}");
            return convertedTime;
        }

        [GeneratedRegex("[^a-zA-Z0-9]")]
        private static partial Regex RemoveSymbolsRegex();
    }
}
