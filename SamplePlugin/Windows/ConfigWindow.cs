using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace HuntAlert.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    Plugin Plugin;

    public ConfigWindow(Plugin plugin) : base(
        "HuntAlerts Config",
        ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.Plugin = plugin;
        this.Size = new Vector2(232, 280);
        this.SizeCondition = ImGuiCond.Always;

        this.Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Create a simple header
        ImGui.Text("Hunt Notifications");

        // Optional: Draw a separator line
        ImGui.Separator();

        // can't ref a property, so use a local copy
        var endwalkerValue = this.Configuration.EndwalkerHunts;
        if (ImGui.Checkbox("Endwalker", ref endwalkerValue))
        {
            this.Configuration.EndwalkerHunts = endwalkerValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        var shadowbringersValue = this.Configuration.ShadowbringersHunts;
        if (ImGui.Checkbox("Shadowbringers", ref shadowbringersValue))
        {
            this.Configuration.ShadowbringersHunts = shadowbringersValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }
        var centurioValue = this.Configuration.CenturioHunts;
        if (ImGui.Checkbox("Centurio", ref centurioValue))
        {
            this.Configuration.CenturioHunts = centurioValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        // Create a simple header
        ImGui.Text("Datacenter");

        // Optional: Draw a separator line
        ImGui.Separator();

        var aetherValue = this.Configuration.Aether;
        if (ImGui.Checkbox("Aether", ref aetherValue))
        {
            this.Configuration.Aether = aetherValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        var crystalValue = this.Configuration.Crystal;
        if (ImGui.Checkbox("Crystal", ref crystalValue))
        {
            this.Configuration.Crystal = crystalValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        var primalValue = this.Configuration.Primal;
        if (ImGui.Checkbox("Primal", ref primalValue))
        {
            this.Configuration.Primal = primalValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        var dynamisValue = this.Configuration.Dynamis;
        if (ImGui.Checkbox("Dynamis", ref dynamisValue))
        {
            this.Configuration.Dynamis = dynamisValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        if (ImGui.Button("Test")) Plugin.Test();
    }
}
