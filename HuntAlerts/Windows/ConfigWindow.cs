using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ECommons.IPC;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Bindings.ImGui;
using HuntAlerts.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace HuntAlerts.Windows;

public class ConfigWindow : Window, IDisposable
{
    private enum Page { General, Integrations, HuntTrains, TrainWorlds, SRanks, SRankWorlds, Connection, Debug }

    private static readonly (Page Id, string Label, FontAwesomeIcon Icon)[] TabDefs =
    {
        (Page.HuntTrains,   "Hunt Trains",   FontAwesomeIcon.Train),
        (Page.TrainWorlds,  "Train Worlds",  FontAwesomeIcon.Globe),
        (Page.SRanks,       "S Ranks",       FontAwesomeIcon.Skull),
        (Page.SRankWorlds,  "S Rank Worlds", FontAwesomeIcon.MapMarkedAlt),
        (Page.General,      "General",       FontAwesomeIcon.Cog),
        (Page.Integrations, "Integrations",  FontAwesomeIcon.Plug),
        (Page.Connection,   "Connection",    FontAwesomeIcon.Server),
        (Page.Debug,        "Debug",         FontAwesomeIcon.Bug),
    };

    private readonly Configuration Configuration;
    private readonly HuntAlerts Plugin;

    private Page _current = Page.HuntTrains;
    private string _worldSearch = "";
    private string _srankWorldSearch = "";
    private bool _debugUnlocked = false;

    private int    _dbgType       = 0;
    private bool[] _dbgKinds      = { false, false, false, true };
    private string _dbgWorld      = "";
    private string _dbgZone       = "";
    private string _dbgAetheryte  = "";
    private string _dbgCoords     = "20.0, 20.0";
    private string _dbgCreature   = "";
    private int    _dbgInstance   = 1;
    private string _dbgContent    = "";
    private string _dbgLastResult = "";

    public ConfigWindow(HuntAlerts plugin) : base("HuntAlerts", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Plugin = plugin;
        Configuration = plugin.Configuration;
        Size          = new Vector2(620, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 360),
            MaximumSize = new Vector2(1200, 1400),
        };
    }

    public void Dispose() { }

    public void OpenDebug()
    {
        _debugUnlocked = true;
        _current       = Page.Debug;
        IsOpen         = true;
    }

    public override void Draw()
    {
        if (ImGui.BeginTable("##cfgRoot", 2, ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableSetupColumn("##nav",  ImGuiTableColumnFlags.WidthFixed, 140);
            ImGui.TableSetupColumn("##body", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            DrawSidebar();

            ImGui.TableNextColumn();
            ImGui.BeginChild("##pageBody", new Vector2(0, 0), false);
            DrawCurrentPage();
            ImGui.EndChild();

            ImGui.EndTable();
        }
    }

    private void DrawSidebar()
    {
        foreach (var (id, label, icon) in TabDefs)
        {
            if (id == Page.Debug && !_debugUnlocked) continue;

            var selected = _current == id;
            if (ImGui.Selectable($"##nav-{id}", selected, ImGuiSelectableFlags.AllowItemOverlap, new Vector2(0, 24)))
                _current = id;

            ImGui.SameLine(8);
            Components.Icon(icon, selected ? Theme.Accent : Theme.Text);
            ImGui.SameLine();
            ImGui.TextUnformatted(label);
        }

        var btnHeight = ImGui.GetFrameHeight() + 4f;
        var avail = ImGui.GetContentRegionAvail();
        if (avail.Y > btnHeight + 8f)
            ImGui.Dummy(new Vector2(0, avail.Y - btnHeight - 4f));

        ImGui.Separator();
        if (Components.ActionButton(FontAwesomeIcon.Comments, "Discord", ButtonRole.Accent))
            Dalamud.Utility.Util.OpenLink("https://discord.gg/punishxiv");
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Open the PunishXIV Discord - ask in #Asuna-plugins for HuntAlerts support");
    }

    private void DrawCurrentPage()
    {
        if (_current == Page.Debug && !_debugUnlocked) _current = Page.HuntTrains;

        switch (_current)
        {
            case Page.General:      DrawGeneralSection();      break;
            case Page.Integrations: DrawIntegrationsSection(); break;
            case Page.HuntTrains:   DrawTrainsSection();       break;
            case Page.TrainWorlds:  DrawWorldsSection();       break;
            case Page.SRanks:       DrawSRankSection();        break;
            case Page.SRankWorlds:  DrawSRankWorldsSection();  break;
            case Page.Connection:   DrawConnectionSection();   break;
            case Page.Debug:        DrawDebugSection();        break;
        }
    }

    private void DrawGeneralSection()
    {
        Components.SectionHeader("General Options");

        var sup = Configuration.SuppressDuplicates;
        if (ImGui.Checkbox("Suppress Duplicate Messages", ref sup))
        { Configuration.SuppressDuplicates = sup; Configuration.Save(); }

        var flag = Configuration.OpenMapOnArrival;
        if (ImGui.Checkbox("Flag Map automatically on arrival", ref flag))
        { Configuration.OpenMapOnArrival = flag; Configuration.Save(); }

        var useDalamud = Configuration.UseDalamudChat;
        if (ImGui.Checkbox("Use Dalamud Default Chat", ref useDalamud))
        { Configuration.UseDalamudChat = useDalamud; Configuration.Save(); }

        ImGui.BeginDisabled(Configuration.UseDalamudChat);
        var chatTypes = Enum.GetValues<XivChatType>().Where(v => v != XivChatType.None).ToArray();
        var chatNames = chatTypes.Select(c => c.ToString()).ToArray();
        var chatIdx   = Array.IndexOf(chatTypes, Configuration.OutputChat);
        if (ImGui.Combo("Chat Channel", ref chatIdx, chatNames, chatNames.Length))
        { Configuration.OutputChat = chatTypes[chatIdx]; Configuration.Save(); }
        ImGui.EndDisabled();

        var relayNames = RelayChannels.All.Select(c => $"{c.Display}  {c.Command}").ToArray();
        var relayIdx   = RelayChannels.IndexOfCommand(Configuration.DefaultRelayChannel);
        if (ImGui.Combo("Default Relay Channel", ref relayIdx, relayNames, relayNames.Length))
        { Configuration.DefaultRelayChannel = RelayChannels.All[relayIdx].Command; Configuration.Save(); }

        var colorOpts = new (string Name, int Value)[]
        {
            ("Default", 0), ("Red", 16), ("Green", 43), ("Blue", 57), ("Yellow", 25), ("Purple", 48),
        };
        var colorNames = colorOpts.Select(o => o.Name).ToArray();

        var textIdx = Array.FindIndex(colorOpts, o => o.Value == Configuration.TextColor);
        if (textIdx < 0) textIdx = 0;
        if (ImGui.Combo("Train Text Color", ref textIdx, colorNames, colorNames.Length))
        { Configuration.TextColor = colorOpts[textIdx].Value; Configuration.Save(); }

        var sIdx = Array.FindIndex(colorOpts, o => o.Value == Configuration.SRankTextColor);
        if (sIdx < 0) sIdx = 0;
        if (ImGui.Combo("S Rank Text Color", ref sIdx, colorNames, colorNames.Length))
        { Configuration.SRankTextColor = colorOpts[sIdx].Value; Configuration.Save(); }

        var soundNames = Enumerable.Range(0, 17)
            .Select(i => i == 0 ? "None" : $"Sound {i}").ToArray();
        var sound = Configuration.SoundEffect;
        if (ImGui.Combo("Sound Effect", ref sound, soundNames, soundNames.Length))
        {
            Configuration.SoundEffect = sound;
            if (sound != 0) UIGlobals.PlayChatSoundEffect((uint)sound);
            Configuration.Save();
        }

        var snoozeOpts  = new (string Name, int Value)[] { ("5 min", 5), ("15 min", 15), ("30 min", 30), ("60 min", 60), ("2 hours", 120) };
        var snoozeNames = snoozeOpts.Select(o => o.Name).ToArray();
        var snoozeIdx   = Array.FindIndex(snoozeOpts, o => o.Value == Configuration.SnoozeDefaultMinutes);
        if (snoozeIdx < 0) snoozeIdx = 2;
        if (ImGui.Combo("Snooze Duration", ref snoozeIdx, snoozeNames, snoozeNames.Length))
        { Configuration.SnoozeDefaultMinutes = snoozeOpts[snoozeIdx].Value; Configuration.Save(); }
        ImGui.SameLine();
        if (HuntAlerts.P.IsSnoozed)
        {
            var rem = (int)Math.Ceiling(HuntAlerts.P.SnoozeRemaining.TotalMinutes);
            if (Components.ActionButton(FontAwesomeIcon.Sun, $"Wake ({rem}m left)", ButtonRole.Accent))
                HuntAlerts.P.ClearSnooze();
        }
        else
        {
            if (Components.ActionButton(FontAwesomeIcon.Moon, $"Snooze {Configuration.SnoozeDefaultMinutes}m", ButtonRole.Warn))
                HuntAlerts.P.SnoozeDefault();
        }
    }

    private void DrawIntegrationsSection()
    {
        Components.SectionHeader("Integrations (Changes take effect next hunt message)");

        var lifestreamInstalled = ECommonsIPC.Lifestream.Available;
        ImGui.BeginDisabled(!lifestreamInstalled);
        var ls = Configuration.LifestreamIntegration;
        if (ImGui.Checkbox("Enable Lifestream Integration", ref ls))
        { Configuration.LifestreamIntegration = ls; Configuration.Save(); }
        ImGui.EndDisabled();

        ImGui.BeginDisabled(!lifestreamInstalled || !Configuration.LifestreamIntegration);
        var ctrl = Configuration.ctrlclickTeleport;
        if (ImGui.Checkbox("Ctrl-Click messages to teleport", ref ctrl))
        { Configuration.ctrlclickTeleport = ctrl; Configuration.Save(); }
        ImGui.EndDisabled();
    }

    private void DrawSRankSection()
    {
        Components.SectionHeader("S Rank Options");

        var srankOn = Configuration.SRankEnabled;
        if (ImGui.Checkbox("S Ranks Enabled", ref srankOn))
        { Configuration.SRankEnabled = srankOn; Configuration.Save(); }

        ImGui.Spacing();

        ImGui.BeginDisabled(!srankOn);

        ImGui.TextUnformatted("S Rank Notifications");
        ImGui.Separator();
        DrawHuntGroupCheckboxes(Configuration.EnabledSRankGroups, "SRank");

        ImGui.Spacing();
        Components.SectionHeader("S Rank Scope");

        DrawSRankScopeRadio(ScopeMode.AllConfigured,         "All configured datacenters/worlds (use the S Rank Worlds tab)");
        DrawSRankScopeRadio(ScopeMode.CurrentDatacenterOnly, "Current datacenter only");
        DrawSRankScopeRadio(ScopeMode.CurrentWorldOnly,      "Current world only");
        DrawSRankScopeRadio(ScopeMode.HomeWorldOnly,         "Home world only");

        ImGui.EndDisabled();
    }

    private void DrawSRankScopeRadio(ScopeMode target, string label)
    {
        var sel = Configuration.SRankScope == target;
        if (ImGui.RadioButton($"{label}##SRankScope", sel) && !sel)
        {
            Configuration.SRankScope = target;
            Configuration.Save();
        }
    }

    private void DrawTrainsSection()
    {
        Components.SectionHeader("Hunt Train Notifications");
        DrawHuntGroupCheckboxes(Configuration.EnabledTrainGroups, "Train");

        ImGui.Spacing();
        Components.SectionHeader("Hunt Train Scope");

        DrawScopeRadio(ScopeMode.AllConfigured,         "All configured datacenters/worlds");
        DrawScopeRadio(ScopeMode.CurrentDatacenterOnly, "Current datacenter only");
        DrawScopeRadio(ScopeMode.CurrentWorldOnly,      "Current world only");
        DrawScopeRadio(ScopeMode.HomeWorldOnly,         "Home world only");
    }

    private void DrawHuntGroupCheckboxes(HashSet<string> set, string idSuffix)
    {
        foreach (var group in HuntGroups.All)
        {
            var label = group switch
            {
                HuntGroups.Centurio => "Centurio (ARR / HW / SB)",
                _                    => group,
            };
            var enabled = set.Contains(group);
            if (ImGui.Checkbox($"{label}##{idSuffix}-{group}", ref enabled))
            {
                if (enabled) set.Add(group); else set.Remove(group);
                Configuration.Save();
            }
        }
    }

    private void DrawScopeRadio(ScopeMode target, string label)
    {
        var sel = Configuration.Scope == target;
        if (ImGui.RadioButton(label, sel) && !sel)
        {
            Configuration.Scope = target;
            Configuration.Save();
        }
    }

    private void DrawWorldsSection()
    {
        DrawWorldsList(
            title: "Hunt Train Worlds",
            scopeBlocks: Configuration.Scope != ScopeMode.AllConfigured,
            scopeBlockedMessage: "World selection is ignored because the Hunt Train scope is set to one of the override modes. Switch to \"All configured datacenters/worlds\" to use this list.",
            enabledDcs: Configuration.EnabledDatacenters,
            enabledWorlds: Configuration.EnabledWorlds,
            searchRef: () => _worldSearch,
            setSearch: v => _worldSearch = v,
            idScope: "train");
    }

    private void DrawSRankWorldsSection()
    {
        DrawWorldsList(
            title: "S Rank Worlds",
            scopeBlocks: Configuration.SRankScope != ScopeMode.AllConfigured,
            scopeBlockedMessage: "World selection is ignored because the S Rank scope is not \"All configured datacenters/worlds\". Switch on the S Ranks tab to use this list.",
            enabledDcs: Configuration.EnabledSRankDatacenters,
            enabledWorlds: Configuration.EnabledSRankWorlds,
            searchRef: () => _srankWorldSearch,
            setSearch: v => _srankWorldSearch = v,
            idScope: "srank");
    }

    private void DrawWorldsList(
        string title,
        bool scopeBlocks,
        string scopeBlockedMessage,
        HashSet<string> enabledDcs,
        HashSet<string> enabledWorlds,
        Func<string> searchRef,
        Action<string> setSearch,
        string idScope)
    {
        Components.SectionHeader(title);

        var dcs = WorldData.DatacentersInOrder;
        if (dcs.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
            ImGui.TextWrapped("No world data available. (Lumina sheets may not be loaded yet - try reopening the window after the game finishes loading.)");
            ImGui.PopStyleColor();
            return;
        }

        if (scopeBlocks)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
            ImGui.TextWrapped(scopeBlockedMessage);
            ImGui.PopStyleColor();
            return;
        }

        var current = searchRef();
        ImGui.PushItemWidth(220);
        if (ImGui.InputTextWithHint($"##worldSearch-{idScope}", "Filter worlds...", ref current, 64))
            setSearch(current);
        ImGui.PopItemWidth();
        ImGui.Spacing();

        var search = current.Trim();

        foreach (var dc in dcs)
        {
            var matching = string.IsNullOrEmpty(search)
                ? dc.Worlds
                : dc.Worlds.Where(w => w.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            if (matching.Count == 0) continue;

            DrawDcBlock(dc, matching, enabledDcs, enabledWorlds, idScope);
            ImGui.Spacing();
        }
    }

    private void DrawDcBlock(WorldData.DatacenterInfo dc, IReadOnlyList<string> shownWorlds, HashSet<string> enabledDcs, HashSet<string> enabledWorlds, string idScope)
    {
        var dcOn   = enabledDcs.Contains(dc.Name);
        var on     = dcOn ? dc.Worlds.Count(w => enabledWorlds.Contains(w)) : 0;
        var region = WorldData.RegionLabel(dc.Region);

        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Accent);
        ImGui.TextUnformatted(dc.Name);
        ImGui.PopStyleColor();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
        ImGui.TextUnformatted($"({region})  {on}/{dc.Worlds.Count} enabled");
        ImGui.PopStyleColor();

        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 150);
        if (Components.DcToggleButton(dcOn, $"{idScope}-{dc.Name}"))
        {
            if (dcOn) enabledDcs.Remove(dc.Name);
            else      enabledDcs.Add(dc.Name);
            Configuration.Save();
        }
        ImGui.SameLine();
        if (ImGui.SmallButton($"All##{idScope}-{dc.Name}"))
        {
            foreach (var w in dc.Worlds) enabledWorlds.Add(w);
            Configuration.Save();
        }
        ImGui.SameLine();
        if (ImGui.SmallButton($"None##{idScope}-{dc.Name}"))
        {
            foreach (var w in dc.Worlds) enabledWorlds.Remove(w);
            Configuration.Save();
        }
        ImGui.Separator();

        ImGui.BeginDisabled(!dcOn);
        if (ImGui.BeginTable($"worlds-{idScope}-{dc.Name}", 3, ImGuiTableFlags.NoBordersInBody))
        {
            foreach (var w in shownWorlds)
            {
                ImGui.TableNextColumn();
                var ticked = enabledWorlds.Contains(w);
                if (ImGui.Checkbox($"{w}##{idScope}-{dc.Name}-{w}", ref ticked))
                {
                    if (ticked) enabledWorlds.Add(w);
                    else        enabledWorlds.Remove(w);
                    Configuration.Save();
                }
            }
            ImGui.EndTable();
        }
        ImGui.EndDisabled();
    }

    private void DrawConnectionSection()
    {
        Components.SectionHeader("Connection");

        var state = Plugin.SocketState;
        var (pillBg, pillBorder, pillFg, pillText) = state switch
        {
            HuntAlerts.ConnectionState.Connected    => (0xFF1E5C3A, 0xFF40A080, 0xFF8AFFD5, "CONNECTED"),
            HuntAlerts.ConnectionState.Connecting   => (0xFF3A3A5C, 0xFF7070C0, 0xFFB8B8FF, "CONNECTING"),
            HuntAlerts.ConnectionState.Reconnecting => (0xFF5C4A1E, 0xFFC0A040, 0xFFFFD68A, "RECONNECTING"),
            HuntAlerts.ConnectionState.Disconnected => (0xFF5C1E1E, 0xFFC04040, 0xFFFF8A8A, "DISCONNECTED"),
            HuntAlerts.ConnectionState.Error        => (0xFF5C1E1E, 0xFFC04040, 0xFFFF8A8A, "ERROR"),
            _                                       => (0xFF2A2A2A, 0xFF4A4A4A, 0xFFB8B8B8, "UNKNOWN"),
        };
        DrawPill(pillText, pillBg, pillBorder, pillFg);

        ImGui.SameLine();
        if (state == HuntAlerts.ConnectionState.Reconnecting)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
            ImGui.TextUnformatted($"  attempt {Plugin.ReconnectAttemptCount}");
            ImGui.PopStyleColor();
        }
        if (Plugin.LastStateChangeUtc.HasValue)
        {
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
            var ago = DateTime.UtcNow - Plugin.LastStateChangeUtc.Value;
            ImGui.TextUnformatted($"  ·  {FormatAgo(ago)} ago");
            ImGui.PopStyleColor();
        }

        ImGui.Spacing();
        Components.FieldRow("Server", Plugin.ServerUri);

        if (!string.IsNullOrEmpty(Plugin.LastConnectionError))
        {
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text, 0xFFFF8A8A);
            ImGui.TextWrapped($"Last error: {Plugin.LastConnectionError}");
            ImGui.PopStyleColor();
        }

        ImGui.Spacing();
        var busy = state == HuntAlerts.ConnectionState.Connecting || state == HuntAlerts.ConnectionState.Reconnecting;
        ImGui.BeginDisabled(busy);
        if (Components.ActionButton(FontAwesomeIcon.Sync, busy ? "Reconnecting..." : "Reconnect", ButtonRole.Accent))
            _ = Plugin.ReconnectAsync();
        ImGui.EndDisabled();

        ImGui.Spacing();
        Components.SectionHeader("Recent Activity");

        var log = Plugin.ConnectionLog;
        if (log.Count == 0)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
            ImGui.TextWrapped("No connection events captured yet.");
            ImGui.PopStyleColor();
            return;
        }

        if (ImGui.BeginChild("##connLog", new Vector2(0, 0), true))
        {
            for (var i = log.Count - 1; i >= 0; i--)
            {
                var entry = log[i];
                var localTime = entry.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");
                var levelColor = entry.Level switch
                {
                    "OK"    => 0xFF90D080u,
                    "WARN"  => 0xFFFFC68Au,
                    "ERROR" => 0xFFFF8A8Au,
                    _       => Theme.Subtle,
                };
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
                ImGui.TextUnformatted(localTime);
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, levelColor);
                ImGui.TextUnformatted($"[{entry.Level}]");
                ImGui.PopStyleColor();
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.Text);
                ImGui.TextWrapped(entry.Message);
                ImGui.PopStyleColor();
            }
        }
        ImGui.EndChild();
    }

    private static void DrawPill(string text, uint bg, uint border, uint fg)
    {
        var draw  = ImGui.GetWindowDrawList();
        var pad   = new Vector2(10, 3);
        var size  = ImGui.CalcTextSize(text);
        var start = ImGui.GetCursorScreenPos();
        var end   = new Vector2(start.X + size.X + pad.X * 2, start.Y + size.Y + pad.Y * 2);
        draw.AddRectFilled(start, end, bg, 10f);
        draw.AddRect(start, end, border, 10f, ImDrawFlags.None, 1.5f);
        draw.AddText(new Vector2(start.X + pad.X, start.Y + pad.Y), fg, text);
        ImGui.Dummy(new Vector2(size.X + pad.X * 2, size.Y + pad.Y * 2));
    }

    private static string FormatAgo(TimeSpan t)
    {
        if (t.TotalSeconds < 60) return $"{(int)t.TotalSeconds}s";
        if (t.TotalMinutes < 60) return $"{(int)t.TotalMinutes}m";
        if (t.TotalHours   < 24) return $"{(int)t.TotalHours}h";
        return $"{(int)t.TotalDays}d";
    }

    private void DrawDebugSection()
    {
        Components.SectionHeader("Simulate Hunt Message");

        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
        ImGui.TextWrapped("Inject a fake server message through the same pipeline as a real one. Use this to test scope, kind filters, aetheryte resolution and the notification UI without waiting on the live feed.");
        ImGui.PopStyleColor();
        ImGui.Spacing();

        var typeLabels = new[] { "Hunt Train", "S Rank" };
        ImGui.PushItemWidth(220);
        ImGui.Combo("Type", ref _dbgType, typeLabels, typeLabels.Length);
        ImGui.PopItemWidth();

        ImGui.TextUnformatted("Kinds");
        for (var i = 0; i < HuntGroups.All.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            var on = _dbgKinds[i];
            if (ImGui.Checkbox($"{HuntGroups.All[i]}##dbgKind{i}", ref on))
                _dbgKinds[i] = on;
        }

        ImGui.PushItemWidth(220);
        ImGui.InputTextWithHint("World##dbg",     "e.g. Sargatanas",          ref _dbgWorld,     32);
        ImGui.SameLine();
        if (ImGui.SmallButton("Current##dbgWorld"))
        {
            var cw = Svc.Objects.LocalPlayer?.CurrentWorld.ValueNullable?.Name.ToString();
            if (!string.IsNullOrEmpty(cw)) _dbgWorld = cw;
        }

        ImGui.InputTextWithHint("Start Zone##dbg", "e.g. Yak T'el",            ref _dbgZone,      64);
        ImGui.InputTextWithHint("Aetheryte##dbg",  "Iq Br'aax or 'invalid'",   ref _dbgAetheryte, 64);
        ImGui.InputTextWithHint("Coords##dbg",     "23.1, 23.5",               ref _dbgCoords,    32);
        ImGui.PopItemWidth();

        if (_dbgType == 1)
        {
            ImGui.PushItemWidth(220);
            ImGui.InputTextWithHint("Creature##dbg", "Optional creature name", ref _dbgCreature, 64);
            ImGui.InputInt("Instance##dbg", ref _dbgInstance);
            if (_dbgInstance < 1) _dbgInstance = 1;
            ImGui.PopItemWidth();
        }
        else
        {
            ImGui.PushItemWidth(420);
            ImGui.InputTextMultiline("Content##dbg", ref _dbgContent, 512, new Vector2(420, 60));
            ImGui.PopItemWidth();
        }

        ImGui.Spacing();

        if (Components.ActionButton(FontAwesomeIcon.PaperPlane, "Send Test Hunt", ButtonRole.Accent))
            SendDebugHunt();
        ImGui.SameLine();
        if (Components.ActionButton(FontAwesomeIcon.Eraser, "Reset Fields", ButtonRole.Warn))
            ResetDebugFields();

        if (!string.IsNullOrEmpty(_dbgLastResult))
        {
            ImGui.Spacing();
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
            ImGui.TextWrapped(_dbgLastResult);
            ImGui.PopStyleColor();
        }
    }

    private void SendDebugHunt()
    {
        var selected = HuntGroups.All.Where((_, i) => _dbgKinds[i]).ToArray();
        if (selected.Length == 0)
        {
            _dbgLastResult = "No kinds selected - tick at least one expansion checkbox.";
            return;
        }
        var kind = string.Join(", ", selected);
        var isTrain = _dbgType == 0;

        var content = !string.IsNullOrEmpty(_dbgContent)
            ? _dbgContent
            : (isTrain
                ? $"{kind} train starting at {_dbgZone} ({_dbgCoords}) - simulated"
                : $"{kind} S Rank spotted at {_dbgZone} ({_dbgCoords}) - simulated");

        var hm = new HuntAlerts.HuntMessage
        {
            Type           = isTrain ? "new_hunt" : "srank",
            Content        = content,
            World          = _dbgWorld,
            Kind           = kind,
            Posted_Epoch   = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            CreatureName   = _dbgCreature,
            LocationName   = _dbgZone,
            LocationCoords = _dbgCoords,
            AetheriteName  = string.IsNullOrEmpty(_dbgAetheryte) ? "invalid" : _dbgAetheryte,
            Instance       = isTrain ? 0 : _dbgInstance,
            DeathTime      = 0,
            AdditionalData = new Dictionary<string, object>(),
        };

        Plugin.SimulateHuntMessage(hm);
        _dbgLastResult = $"Dispatched {(isTrain ? "train" : "S Rank")} for {kind} on '{_dbgWorld}'. Check chat / popup; verbose log captures any scope-filtering reason.";
    }

    private void ResetDebugFields()
    {
        _dbgType       = 0;
        _dbgKinds      = new[] { false, false, false, true };
        _dbgWorld      = "";
        _dbgZone       = "";
        _dbgAetheryte  = "";
        _dbgCoords     = "20.0, 20.0";
        _dbgCreature   = "";
        _dbgInstance   = 1;
        _dbgContent    = "";
        _dbgLastResult = "";
    }
}
