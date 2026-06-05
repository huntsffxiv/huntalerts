using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace HuntAlerts.Windows;

public class WhatsNewWindow : Window
{
    public WhatsNewWindow() : base("HuntAlerts - What's New")
    {
        Size            = new Vector2(520, 470);
        SizeCondition   = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 280),
            MaximumSize = new Vector2(1000, 1400),
        };
    }

    public override void Draw()
    {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Accent);
        ImGui.TextUnformatted(FontAwesomeIcon.Gift.ToIconString());
        ImGui.PopStyleColor();
        ImGui.PopFont();
        ImGui.SameLine();
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Accent);
        ImGui.TextUnformatted("What's New in HuntAlerts");
        ImGui.PopStyleColor();
        ImGui.Separator();
        ImGui.Spacing();

        var footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y * 2;
        var midHeight    = System.Math.Max(60f, ImGui.GetContentRegionAvail().Y - footerHeight);

        if (ImGui.BeginChild("##whatsnewBody", new Vector2(0, midHeight), false))
        {
            for (var i = 0; i < Changelog.Entries.Length; i++)
            {
                var e     = Changelog.Entries[i];
                var flags = i == 0 ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
                if (ImGui.CollapsingHeader($"Version {e.Version}##cl{i}", flags))
                {
                    DrawCategory("Features", e.Features);
                    DrawCategory("Bug Fixes", e.BugFixes);
                    ImGui.Spacing();
                }
            }
        }
        ImGui.EndChild();

        ImGui.Separator();
        if (Components.ActionButton(FontAwesomeIcon.Check, "Got it", ButtonRole.Accent))
            IsOpen = false;
    }

    private static void DrawCategory(string title, string[] items)
    {
        if (items == null || items.Length == 0) return;

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, Theme.Accent);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();
        ImGui.Spacing();

        foreach (var c in items)
        {
            var sub  = c.StartsWith("- ");
            var text = sub ? c.Substring(2) : c;

            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Subtle);
            ImGui.TextUnformatted(sub ? "        –" : "   •");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.Text);
            ImGui.PushTextWrapPos();
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
            ImGui.PopStyleColor();
        }
        ImGui.Spacing();
    }
}
