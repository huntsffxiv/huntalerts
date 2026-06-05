using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using ECommons.Logging;
using HuntAlerts.Helpers;
using HuntAlerts.Services;
using System;
using System.Numerics;

namespace HuntAlerts.Windows;

public class NotifyWindow : Window
{
    private HuntTrainMessage? currentMessage;
    public HuntTrainMessage? CurrentMessage
    {
        get => currentMessage;
        set
        {
            currentMessage = value;
            if (value != null)
                ArrowWaypoint.Set(value.startTerritoryTypeId, value.mapLocationX, value.mapLocationY, "notification", value.huntWorld);
        }
    }
    private readonly TitleBarButton snoozeButton;

    public NotifyWindow() : base("HuntAlerts Notification", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        Size          = new Vector2(440, 360);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 240),
            MaximumSize = new Vector2(900, 1400),
        };
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon        = FontAwesomeIcon.Cog,
            IconOffset  = new Vector2(2, 1),
            Click       = _ => Service.OpenConfig(),
            ShowTooltip = () => ImGui.SetTooltip("Open settings"),
        });
        snoozeButton = new TitleBarButton
        {
            Icon        = FontAwesomeIcon.Bell,
            IconOffset  = new Vector2(2, 1),
            Click       = _ => Service.Snooze.ToggleSnooze(),
            ShowTooltip = () => ImGui.SetTooltip(SnoozeTooltipText()),
        };
        TitleBarButtons.Add(snoozeButton);
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon        = FontAwesomeIcon.History,
            IconOffset  = new Vector2(2, 1),
            Click       = _ => Service.HuntListWindow.IsOpen = true,
            ShowTooltip = () => ImGui.SetTooltip("Recent Hunts"),
        });
    }

    public override void PreDraw()
    {
        snoozeButton.Icon = Service.Snooze.IsSnoozed ? FontAwesomeIcon.BellSlash : FontAwesomeIcon.Bell;
    }

    private static string SnoozeTooltipText()
    {
        if (Service.Snooze.IsSnoozed)
            return $"Snoozed -{Math.Ceiling(Service.Snooze.SnoozeRemaining.TotalMinutes)}m remaining. Click to wake.";
        var d = HuntAlerts.C.SnoozeDefaultMinutes;
        return $"Snooze alerts for {d}m";
    }

    public override void Draw()
    {
        var entry = CurrentMessage;
        if (entry == null)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
            ImGui.TextWrapped("Could not find requested entry.");
            ImGui.PopStyleColor();
            return;
        }

        var isTrain = entry.huntType == "new_hunt";

        DrawHero(entry, isTrain);
        ImGui.Spacing();
        DrawTagStrip(entry);
        ImGui.Spacing();

        var footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y * 2;
        var midHeight    = Math.Max(40f, ImGui.GetContentRegionAvail().Y - footerHeight);

        if (ImGui.BeginChild("##notifyMid", new Vector2(0, midHeight), false))
        {
            if (isTrain)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.Text);
                ImGui.PushTextWrapPos();
                ImGui.TextUnformatted(entry.Message);
                ImGui.PopTextWrapPos();
                ImGui.PopStyleColor();
            }
            else
            {
                DrawStructuredFields(entry);
            }
        }
        ImGui.EndChild();

        ImGui.Separator();
        DrawActions(entry);
    }

    private static void DrawHero(HuntTrainMessage entry, bool isTrain)
    {
        var (badgeText, style, title) = isTrain
            ? ("TRAIN",  BadgeStyle.Train, $"{entry.huntKind} train")
            : ("S RANK", BadgeStyle.SRank, $"{entry.huntKind} S Rank");
        Components.Badge(badgeText, style);
        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.DefaultFont);
        ImGui.TextUnformatted(title);
        ImGui.PopFont();
    }

    private static void DrawTagStrip(HuntTrainMessage entry)
    {
        Components.Badge(entry.huntKind ?? "", BadgeStyle.Kind);
        ImGui.SameLine();
        Components.Badge(entry.huntWorld ?? "", BadgeStyle.World);
        if (entry.instance > 1)
        {
            ImGui.SameLine();
            Components.Badge($"i{entry.instance}", BadgeStyle.Kind);
        }
    }

    private static void DrawStructuredFields(HuntTrainMessage entry)
    {
        if (!string.IsNullOrEmpty(entry.creatureName))
            Components.FieldRow("Creature",  entry.creatureName);
        if (!string.IsNullOrEmpty(entry.startZone))
            Components.FieldRow("Zone",      entry.startZone);
        if (!string.IsNullOrEmpty(entry.locationCoords))
            Components.FieldRow("Coords",    entry.locationCoords);
        if (!string.IsNullOrEmpty(entry.startLocation))
            Components.FieldRow("Aetheryte", entry.startLocation);
        if (!string.IsNullOrEmpty(entry.Posted_Time))
            Components.FieldRow("Posted",    entry.Posted_Time);

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
        ImGui.PushTextWrapPos();
        ImGui.TextUnformatted(entry.Message);
        ImGui.PopTextWrapPos();
        ImGui.PopStyleColor();
    }

    private static void DrawActions(HuntTrainMessage entry)
    {
        var canTeleport =
            entry.lifestreamEnabled &&
            !string.IsNullOrEmpty(entry.huntWorld) &&
            entry.currentRegionRowId != 0 &&
            entry.currentRegionRowId == entry.huntRegionRowId;

        var first = true;

        if (canTeleport)
        {
            var label = entry.startLocationAetheryteId != 0 ? "Teleport" : "Change World";
            if (Components.ActionButton(FontAwesomeIcon.Rocket, label, ButtonRole.Accent))
            {
                PluginLog.Verbose("Attempting Lifestream teleport / world change from NotifyWindow");
                Utilities.ExecuteTeleport(
                    entry.huntWorld, entry.startLocation, entry.startLocationAetheryteId,
                    entry.startZone, entry.locationCoords, entry.instance,
                    entry.openmaponArrival, entry.lifestreamEnabled);
            }
            first = false;
        }

        if (!string.IsNullOrEmpty(entry.locationCoords))
        {
            if (!first) ImGui.SameLine();
            if (Components.ActionButton(FontAwesomeIcon.MapMarkerAlt, "Flag on Map", ButtonRole.Warn))
                Utilities.FlagOnMap(entry.locationCoords, entry.startZone);
            first = false;
        }

        if (!first) ImGui.SameLine();
        if (Components.ActionButton(FontAwesomeIcon.Users, "Party Finder", ButtonRole.Info))
            Utilities.OpenPartyFinder();

        if (entry.startTerritoryTypeId != 0 && !(entry.mapLocationX == 0f && entry.mapLocationY == 0f))
        {
            ImGui.SameLine();
            if (Components.ActionButton(FontAwesomeIcon.LocationArrow, "Nav", ButtonRole.Accent))
                ArrowWaypoint.Set(entry.startTerritoryTypeId, entry.mapLocationX, entry.mapLocationY, "manual", entry.huntWorld, force: true);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Point the navigation arrow at these coordinates");
        }

        ImGui.SameLine();
        var defaultChannel = string.IsNullOrEmpty(HuntAlerts.C.DefaultRelayChannel)
            ? "/p"
            : HuntAlerts.C.DefaultRelayChannel;
        var defaultDisplay = RelayChannels.DisplayFor(defaultChannel);
        if (Components.ActionButton(FontAwesomeIcon.Bullhorn, "Relay", ButtonRole.Success))
            RelayChannels.RelayMessage(entry, defaultChannel);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Relay to {defaultDisplay}");

        ImGui.SameLine(0, 2);
        ImGui.PushStyleColor(ImGuiCol.Button,        Theme.SuccessBtn);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.SuccessBtnHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  Theme.SuccessBtnActive);
        if (ImGuiComponents.IconButton("##relayChevron", FontAwesomeIcon.ChevronUp))
            ImGui.OpenPopup("##relayPicker");
        ImGui.PopStyleColor(3);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Pick a one-off relay channel");

        if (ImGui.BeginPopup("##relayPicker"))
        {
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
            ImGui.TextUnformatted($"Default: {defaultDisplay}");
            ImGui.PopStyleColor();
            ImGui.Separator();
            foreach (var ch in RelayChannels.All)
            {
                if (ImGui.MenuItem($"{ch.Display}  {ch.Command}"))
                {
                    RelayChannels.RelayMessage(entry, ch.Command);
                }
            }
            ImGui.EndPopup();
        }
    }
}
