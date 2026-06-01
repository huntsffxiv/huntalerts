using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Logging;
using System;
using System.Text;

namespace HuntAlerts.Helpers;

public readonly record struct RelayChannel(string Display, string Command);

public static class RelayChannels
{
    public static readonly RelayChannel[] All =
    {
        new("Say",           "/s"),
        new("Yell",          "/y"),
        new("Shout",         "/sh"),
        new("Party",         "/p"),
        new("Alliance",      "/a"),
        new("Free Company",  "/fc"),
        new("Linkshell 1",   "/l1"),
        new("Linkshell 2",   "/l2"),
        new("Linkshell 3",   "/l3"),
        new("Linkshell 4",   "/l4"),
        new("Linkshell 5",   "/l5"),
        new("Linkshell 6",   "/l6"),
        new("Linkshell 7",   "/l7"),
        new("Linkshell 8",   "/l8"),
        new("CWLS 1",        "/cwl1"),
        new("CWLS 2",        "/cwl2"),
        new("CWLS 3",        "/cwl3"),
        new("CWLS 4",        "/cwl4"),
        new("CWLS 5",        "/cwl5"),
        new("CWLS 6",        "/cwl6"),
        new("CWLS 7",        "/cwl7"),
        new("CWLS 8",        "/cwl8"),
        new("Echo (test)",   "/echo"),
    };

    public static string DisplayFor(string command)
    {
        foreach (var ch in All)
            if (ch.Command.Equals(command, StringComparison.OrdinalIgnoreCase))
                return ch.Display;
        return "Party";
    }

    public static int IndexOfCommand(string command)
    {
        for (var i = 0; i < All.Length; i++)
            if (All[i].Command.Equals(command, StringComparison.OrdinalIgnoreCase))
                return i;
        return 3;
    }

    public static void RelayMessage(HuntTrainMessage entry, string channelCommand)
    {
        if (entry == null) return;
        var text = BuildRelayText(entry);
        if (string.IsNullOrWhiteSpace(text)) return;

        var line = $"{channelCommand} {text}";
        if (line.Length > 500) line = line.Substring(0, 500);

        Svc.Framework.RunOnFrameworkThread(() =>
        {
            try
            {
                Chat.Instance.SendMessage(line);
                PluginLog.Verbose($"Relayed hunt to {channelCommand}: {text}");
            }
            catch (Exception ex)
            {
                PluginLog.Warning($"Relay failed ({channelCommand}): {ex.Message}");
            }
        });
    }

    private static string BuildRelayText(HuntTrainMessage entry)
    {
        var isTrain  = entry.huntType == "new_hunt";
        var kind     = CleanOrFallback(entry.huntKind, "Hunt");
        var world    = Clean(entry.huntWorld);
        var creature = Clean(entry.creatureName);
        var zone     = Clean(entry.startZone);
        var coords  = Clean(entry.locationCoords);
        var ae      = Clean(entry.startLocation);
        var inst    = entry.instance > 1 ? $" i{entry.instance}" : "";

        var label = isTrain ? "train" : "S Rank";

        var sb = new StringBuilder();
        sb.Append(kind).Append(' ').Append(label);
        if (!isTrain && !string.IsNullOrEmpty(creature)) sb.Append(' ').Append(creature);
        if (!string.IsNullOrEmpty(world)) sb.Append(" on ").Append(world);
        sb.Append('!');

        var hasZone   = !string.IsNullOrEmpty(zone);
        var hasCoords = !string.IsNullOrEmpty(coords);
        if (hasZone || hasCoords)
        {
            sb.Append(' ');
            if (hasZone) sb.Append(zone);
            if (hasZone && hasCoords) sb.Append(' ');
            if (hasCoords) sb.Append('(').Append(coords).Append(')');
            sb.Append(inst);
        }
        else if (inst.Length > 0)
        {
            sb.Append(inst);
        }

        if (!string.IsNullOrEmpty(ae)) sb.Append(" - ").Append(ae);
        return sb.ToString();
    }

    private static string Clean(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var t = s.Trim();
        if (t.Equals("invalid", StringComparison.OrdinalIgnoreCase)) return "";
        if (t.Equals("unknown", StringComparison.OrdinalIgnoreCase)) return "";
        return t;
    }

    private static string CleanOrFallback(string s, string fallback)
    {
        var c = Clean(s);
        return string.IsNullOrEmpty(c) ? fallback : c;
    }
}
