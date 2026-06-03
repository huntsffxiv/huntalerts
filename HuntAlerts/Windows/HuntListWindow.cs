using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ECommons.Logging;
using HuntAlerts.Helpers;
using HuntAlerts.Services;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace HuntAlerts.Windows;

public class HuntListWindow : Window
{
    private readonly TitleBarButton _snoozeButton;

    public HuntListWindow() : base("HuntAlerts History", ImGuiWindowFlags.None)
    {
        Size            = new Vector2(520, 440);
        SizeCondition   = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(380, 260),
            MaximumSize = new Vector2(1200, 1600),
        };
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon        = FontAwesomeIcon.Cog,
            IconOffset  = new Vector2(2, 1),
            Click       = _ => Service.OpenConfig(),
            ShowTooltip = () => ImGui.SetTooltip("Open settings"),
        });
        _snoozeButton = new TitleBarButton
        {
            Icon        = FontAwesomeIcon.Bell,
            IconOffset  = new Vector2(2, 1),
            Click       = _ => Service.Snooze.ToggleSnooze(),
            ShowTooltip = () => ImGui.SetTooltip(SnoozeTooltipText()),
        };
        TitleBarButtons.Add(_snoozeButton);
    }

    public override void PreDraw()
    {
        _snoozeButton.Icon = Service.Snooze.IsSnoozed ? FontAwesomeIcon.BellSlash : FontAwesomeIcon.Bell;
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
        try
        {
            var messages = Service.MessageCacheManager.GetOrderedMessages();
            messages.Reverse();

            DrawHeader(messages.Count);

            if (messages.Count == 0)
            {
                ImGui.Spacing();
                ImGui.Spacing();
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
                ImGui.PushFont(UiBuilder.IconFont);
                var icon = FontAwesomeIcon.Inbox.ToIconString();
                var iconWidth = ImGui.CalcTextSize(icon).X;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - iconWidth) * 0.5f);
                ImGui.TextUnformatted(icon);
                ImGui.PopFont();
                ImGui.Spacing();
                var msg = "No hunts have come in yet.";
                var msgWidth = ImGui.CalcTextSize(msg).X;
                ImGui.SetCursorPosX((ImGui.GetWindowWidth() - msgWidth) * 0.5f);
                ImGui.TextUnformatted(msg);
                ImGui.PopStyleColor();
                return;
            }

            if (ImGui.BeginChild("##huntListScroll", new Vector2(0, 0), false))
                DrawCards(messages);
            ImGui.EndChild();
        }
        catch (Exception ex)
        {
            PluginLog.Error("Error while displaying the huntalert list");
            PluginLog.Error(ex.Message);
        }
    }

    private static void DrawHeader(int count)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Accent);
        ImGui.TextUnformatted(FontAwesomeIcon.History.ToIconString());
        ImGui.PopStyleColor();
        ImGui.PopFont();

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Accent);
        ImGui.TextUnformatted("Recent Hunts");
        ImGui.PopStyleColor();

        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
        ImGui.TextUnformatted($"  ·  {count} cached");
        ImGui.PopStyleColor();

        if (Service.Snooze.IsSnoozed)
        {
            ImGui.SameLine();
            var remainingMin = (int)Math.Ceiling(Service.Snooze.SnoozeRemaining.TotalMinutes);
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentBtn);
            ImGui.TextUnformatted($"  {FontAwesomeIcon.BellSlash.ToIconString()}");
            ImGui.PopStyleColor();
            ImGui.PopFont();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.AccentBtn);
            ImGui.TextUnformatted($" Snoozed -{remainingMin}m left");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            if (Components.ActionButton(FontAwesomeIcon.Sun, "Wake", ButtonRole.Accent))
                Service.Snooze.ClearSnooze();
        }

        ImGui.Separator();
        ImGui.Spacing();
    }

    private static void DrawCards(List<HuntTrainMessage> messages)
    {
        var style = ImGui.GetStyle();
        var drawList = ImGui.GetWindowDrawList();

        const float cardHeight  = 60f;
        const float cardSpacing = 6f;
        const float padX        = 12f;
        const float padY        = 9f;

        for (var i = 0; i < messages.Count; i++)
        {
            var msg = messages[i];
            if (msg == null) continue;

            var isTrain    = msg.huntType == "new_hunt";
            var badgeText  = isTrain ? "TRAIN" : "S RANK";
            var badgeStyle = isTrain ? BadgeStyle.Train : BadgeStyle.SRank;
            var accent     = isTrain ? Theme.TrainBorder : Theme.SRankBorder;
            var kindLabel  = NormalizeKindLabel(msg.huntKind);
            var world      = msg.huntWorld ?? "Unknown";
            var time       = FormatClockTime(msg);
            var age        = FormatRelativeAge(msg);

            ImGui.PushID(i);

            var origin    = ImGui.GetCursorScreenPos();
            var width     = ImGui.GetContentRegionAvail().X;
            var topLeft   = origin;
            var btmRight  = new Vector2(origin.X + width, origin.Y + cardHeight);

            if (ImGui.InvisibleButton("##card", new Vector2(width, cardHeight)))
            {
                Service.NotifyWindow.IsOpen = true;
                Service.NotifyWindow.CurrentMessage = msg;
            }
            var hovered = ImGui.IsItemHovered();
            if (hovered) ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            var bg     = hovered ? Theme.CardBgHover : Theme.CardBg;
            var border = hovered ? accent          : Theme.CardBorder;
            drawList.AddRectFilled(topLeft, btmRight, bg, 6f);
            drawList.AddRect(topLeft, btmRight, border, 6f, ImDrawFlags.None, hovered ? 2f : 1f);
            drawList.AddRectFilled(topLeft, new Vector2(topLeft.X + 4f, btmRight.Y), accent, 6f);

            ImGui.SetCursorScreenPos(new Vector2(topLeft.X + padX, topLeft.Y + padY));
            Components.Badge(badgeText, badgeStyle);
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Text);
            ImGui.TextUnformatted(kindLabel);
            ImGui.PopStyleColor();

            var timeSize = ImGui.CalcTextSize(time);
            var ageSize  = ImGui.CalcTextSize(age);
            var iconStr  = FontAwesomeIcon.ChevronRight.ToIconString();
            ImGui.PushFont(UiBuilder.IconFont);
            var chevSize = ImGui.CalcTextSize(iconStr);
            ImGui.PopFont();

            var rightPadX  = padX;
            var chevLeftX  = btmRight.X - rightPadX - chevSize.X;
            var textRightX = chevLeftX - 12f;

            ImGui.SetCursorScreenPos(new Vector2(chevLeftX, origin.Y + (cardHeight - chevSize.Y) * 0.5f));
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.PushStyleColor(ImGuiCol.Text, hovered ? Theme.Text : Theme.Subtle);
            ImGui.TextUnformatted(iconStr);
            ImGui.PopStyleColor();
            ImGui.PopFont();

            ImGui.SetCursorScreenPos(new Vector2(textRightX - timeSize.X, topLeft.Y + padY + 1f));
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Text);
            ImGui.TextUnformatted(time);
            ImGui.PopStyleColor();

            if (!string.IsNullOrEmpty(age))
            {
                ImGui.SetCursorScreenPos(new Vector2(textRightX - ageSize.X, topLeft.Y + padY + 1f + ImGui.GetTextLineHeight() + 2f));
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
                ImGui.TextUnformatted(age);
                ImGui.PopStyleColor();
            }

            ImGui.SetCursorScreenPos(new Vector2(topLeft.X + padX, topLeft.Y + cardHeight - padY - ImGui.GetTextLineHeight() - 2f));
            Components.Badge(world, BadgeStyle.World);
            if (msg.instance > 1)
            {
                ImGui.SameLine();
                Components.Badge($"i{msg.instance}", BadgeStyle.Kind);
            }
            if (!string.IsNullOrEmpty(msg.startZone))
            {
                ImGui.SameLine();
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
                ImGui.TextUnformatted($"  ·  {msg.startZone}");
                ImGui.PopStyleColor();
            }

            ImGui.SetCursorScreenPos(new Vector2(origin.X, btmRight.Y + cardSpacing));
            ImGui.PopID();
        }
    }

    private static string FormatClockTime(HuntTrainMessage msg)
    {
        if (msg.PostedEpoch <= 0) return msg.Posted_Time ?? "";
        var posted = DateTimeOffset.FromUnixTimeSeconds(msg.PostedEpoch).ToLocalTime().DateTime;
        return posted.ToString("hh:mm tt", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
    }

    private static string FormatRelativeAge(HuntTrainMessage msg)
    {
        if (msg.PostedEpoch <= 0) return "";
        var posted  = DateTimeOffset.FromUnixTimeSeconds(msg.PostedEpoch).ToLocalTime().DateTime;
        var today   = DateTime.Now.Date;
        var msgDay  = posted.Date;
        if (msgDay == today) return "Today";
        var daysAgo = (int)(today - msgDay).TotalDays;
        if (daysAgo == 1) return "1 Day Ago";
        return $"{daysAgo} Days Ago";
    }

    private static string NormalizeKindLabel(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "Unknown";
        return raw switch
        {
            "EW"  => "Endwalker",
            "DT"  => "Dawntrail",
            "SHB" => "Shadowbringers",
            "SB" or "HW" or "ARR" => "Centurio",
            _     => raw,
        };
    }
}
