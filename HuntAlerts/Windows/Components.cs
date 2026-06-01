using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;

namespace HuntAlerts.Windows;

internal static class Components
{
    public static void SectionHeader(string title)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Accent);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();
        ImGui.Separator();
        ImGui.Spacing();
    }

    public static void Badge(string text, BadgeStyle style)
    {
        var (bg, border, fg) = style switch
        {
            BadgeStyle.SRank => (Theme.SRankBg, Theme.SRankBorder, Theme.SRankText),
            BadgeStyle.Train => (Theme.TrainBg, Theme.TrainBorder, Theme.TrainText),
            BadgeStyle.Kind  => (Theme.KindBg,  Theme.KindBorder,  Theme.KindText),
            BadgeStyle.World => (Theme.WorldBg, Theme.WorldBorder, Theme.WorldText),
            _                => (Theme.KindBg,  Theme.KindBorder,  Theme.KindText),
        };
        var pad   = Theme.BadgePadding;
        var draw  = ImGui.GetWindowDrawList();
        var size  = ImGui.CalcTextSize(text);
        var start = ImGui.GetCursorScreenPos();
        var end   = new Vector2(start.X + size.X + pad.X * 2, start.Y + size.Y + pad.Y * 2);
        draw.AddRectFilled(start, end, bg, 2f);
        draw.AddRect(start, end, border, 2f);
        draw.AddText(new Vector2(start.X + pad.X, start.Y + pad.Y), fg, text);
        ImGui.Dummy(new Vector2(size.X + pad.X * 2, size.Y + pad.Y * 2));
    }

    public static void FieldRow(string label, string value)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Accent);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();
        ImGui.SameLine(90f);
        ImGui.TextUnformatted(value);
    }

    public static bool ActionButton(FontAwesomeIcon icon, string label, ButtonRole role)
    {
        var (b, h, a) = role switch
        {
            ButtonRole.Accent => (Theme.AccentBtn, Theme.AccentBtnHover, Theme.AccentBtnActive),
            ButtonRole.Warn   => (Theme.WarnBtn,   Theme.WarnBtnHover,   Theme.WarnBtnActive),
            ButtonRole.Info   => (Theme.InfoBtn,   Theme.InfoBtnHover,   Theme.InfoBtnActive),
            ButtonRole.Danger => (Theme.DangerBtn, Theme.DangerBtnHover, Theme.DangerBtnActive),
            _                 => (Theme.AccentBtn, Theme.AccentBtnHover, Theme.AccentBtnActive),
        };
        ImGui.PushStyleColor(ImGuiCol.Button,        b);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, h);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  a);
        var clicked = ImGuiComponents.IconButtonWithText(icon, label);
        ImGui.PopStyleColor(3);
        return clicked;
    }

    public static void Icon(FontAwesomeIcon icon, uint? colorAbgr = null)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        if (colorAbgr.HasValue) ImGui.PushStyleColor(ImGuiCol.Text, colorAbgr.Value);
        ImGui.TextUnformatted(icon.ToIconString());
        if (colorAbgr.HasValue) ImGui.PopStyleColor();
        ImGui.PopFont();
    }

    public static bool DcToggleButton(bool isOn, string id)
    {
        var (b, h, a) = isOn
            ? (Theme.ButtonOn,  Theme.ButtonOnHover,  Theme.ButtonOnActive)
            : (Theme.ButtonOff, Theme.ButtonOffHover, Theme.ButtonOffActive);
        ImGui.PushStyleColor(ImGuiCol.Button,        b);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, h);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive,  a);
        var clicked = ImGui.SmallButton($"{(isOn ? "DC On" : "DC Off")}##{id}");
        ImGui.PopStyleColor(3);
        return clicked;
    }
}
