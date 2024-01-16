using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using FFXIVClientStructs.FFXIV.Client.UI;
using System.Collections.Generic;
using FFXIVClientStructs.Havok;

namespace HuntAlerts.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    Plugin Plugin;


    public ConfigWindow(Plugin plugin) : base(
       "HuntAlerts Config",
       ImGuiWindowFlags.NoResize)
    {
        this.Plugin = plugin;
        this.Size = new Vector2(400, 800);
        this.SizeCondition = ImGuiCond.Always;

        this.Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Create a simple header
        ImGui.Text("General Options");

        // Optional: Draw a separator line
        ImGui.Separator();

        var maxlineLength = this.Configuration.MaxLineLength;
        if(ImGui.InputInt("Max Line Length",ref maxlineLength))
        {
            if (maxlineLength > 999)
            {
                maxlineLength = 999;
            }else if (maxlineLength <= 50)
            {
                maxlineLength = 50;
            }

            this.Configuration.MaxLineLength = maxlineLength;
            this.Configuration.Save();
        }

        // Add blank line
        ImGui.NewLine();

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
        ImGui.NewLine();
        ImGui.Text("World Options");

        // Optional: Draw a separator line
        ImGui.Separator();

        var homeworldonlyValue = this.Configuration.HomeWorldOnly;
        if (ImGui.Checkbox("Homeworld Only", ref homeworldonlyValue))
        {
            this.Configuration.HomeWorldOnly = homeworldonlyValue;
            if(homeworldonlyValue)
            {
                this.Configuration.CurrentWorldOnly = false;
            }

            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        var currentworldonlyValue = this.Configuration.CurrentWorldOnly;
        if (ImGui.Checkbox("Current World Only", ref currentworldonlyValue))
        {
            this.Configuration.CurrentWorldOnly = currentworldonlyValue;
            if(currentworldonlyValue)
            {
                this.Configuration.HomeWorldOnly = false;
            }
            // can save immediately on change, if you don't want to provide a "Save and Close" button

            this.Configuration.Save();
        }

        // Create a simple header
        ImGui.NewLine();
        ImGui.Text("Datacenter");

        // Optional: Draw a separator line
        ImGui.Separator();

        if(currentworldonlyValue || homeworldonlyValue)
        {
            ImGui.BeginDisabled();
        }

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

        var lightValue = this.Configuration.Light;
        if (ImGui.Checkbox("Light", ref lightValue))
        {
            this.Configuration.Light = lightValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        var chaosValue = this.Configuration.Chaos;
        if (ImGui.Checkbox("Chaos", ref chaosValue))
        {
            this.Configuration.Chaos = chaosValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        if (currentworldonlyValue || homeworldonlyValue)
        {
            ImGui.EndDisabled();
        }

        ImGui.NewLine();
        ImGui.Text("World Selection");
        ImGui.Separator();

        if(!aetherValue && !crystalValue && !primalValue && !dynamisValue && !lightValue && !chaosValue)
        {
            ImGui.Text("No Datacenters selected, please choose a datacenter");
        }

        // Aether world selection
        if (aetherValue)
        {

            if (ImGui.TreeNode("Aether World Selection"))
            {
                ImGui.Indent();

                var adamantoiseWorldValue = this.Configuration.AdamantoiseWorld;
                if (ImGui.Checkbox("Adamantoise", ref adamantoiseWorldValue))
                {
                    this.Configuration.AdamantoiseWorld = adamantoiseWorldValue;
                    this.Configuration.Save();
                }

                var cactuarWorldValue = this.Configuration.CactuarWorld;
                if (ImGui.Checkbox("Cactuar", ref cactuarWorldValue))
                {
                    this.Configuration.CactuarWorld = cactuarWorldValue;
                    this.Configuration.Save();
                }

                var faerieWorldValue = this.Configuration.FaerieWorld;
                if (ImGui.Checkbox("Faerie", ref faerieWorldValue))
                {
                    this.Configuration.FaerieWorld = faerieWorldValue;
                    this.Configuration.Save();
                }

                var gilgameshWorldValue = this.Configuration.GilgameshWorld;
                if (ImGui.Checkbox("Gilgamesh", ref gilgameshWorldValue))
                {
                    this.Configuration.GilgameshWorld = gilgameshWorldValue;
                    this.Configuration.Save();
                }

                var jenovaWorldValue = this.Configuration.JenovaWorld;
                if (ImGui.Checkbox("Jenova", ref jenovaWorldValue))
                {
                    this.Configuration.JenovaWorld = jenovaWorldValue;
                    this.Configuration.Save();
                }

                var midgardsormrWorldValue = this.Configuration.MidgardsormrWorld;
                if (ImGui.Checkbox("Midgardsormr", ref midgardsormrWorldValue))
                {
                    this.Configuration.MidgardsormrWorld = midgardsormrWorldValue;
                    this.Configuration.Save();
                }

                var sargatanasWorldValue = this.Configuration.SargatanasWorld;
                if (ImGui.Checkbox("Sargatanas", ref sargatanasWorldValue))
                {
                    this.Configuration.SargatanasWorld = sargatanasWorldValue;
                    this.Configuration.Save();
                }

                var sirenWorldValue = this.Configuration.SirenWorld;
                if (ImGui.Checkbox("Siren", ref sirenWorldValue))
                {
                    this.Configuration.SirenWorld = sirenWorldValue;
                    this.Configuration.Save();
                }

                ImGui.Unindent();
                ImGui.TreePop();

            }
        }

        // Crystal world selection
        if (crystalValue)
        {

            if (ImGui.TreeNode("Crystal World Selection"))
            {
                ImGui.Indent();

                var balmungWorldValue = this.Configuration.BalmungWorld;
                if (ImGui.Checkbox("Balmung", ref balmungWorldValue))
                {
                    this.Configuration.BalmungWorld = balmungWorldValue;
                    this.Configuration.Save();
                }

                var brynhildrWorldValue = this.Configuration.BrynhildrWorld;
                if (ImGui.Checkbox("Brynhildr", ref brynhildrWorldValue))
                {
                    this.Configuration.BrynhildrWorld = brynhildrWorldValue;
                    this.Configuration.Save();
                }

                var coeurlWorldValue = this.Configuration.CoeurlWorld;
                if (ImGui.Checkbox("Coeurl", ref coeurlWorldValue))
                {
                    this.Configuration.CoeurlWorld = coeurlWorldValue;
                    this.Configuration.Save();
                }

                var diabolosWorldValue = this.Configuration.DiabolosWorld;
                if (ImGui.Checkbox("Diabolos", ref diabolosWorldValue))
                {
                    this.Configuration.DiabolosWorld = diabolosWorldValue;
                    this.Configuration.Save();
                }

                var goblinWorldValue = this.Configuration.GoblinWorld;
                if (ImGui.Checkbox("Goblin", ref goblinWorldValue))
                {
                    this.Configuration.GoblinWorld = goblinWorldValue;
                    this.Configuration.Save();
                }

                var malboroWorldValue = this.Configuration.MalboroWorld;
                if (ImGui.Checkbox("Malboro", ref malboroWorldValue))
                {
                    this.Configuration.MalboroWorld = malboroWorldValue;
                    this.Configuration.Save();
                }

                var mateusWorldValue = this.Configuration.MateusWorld;
                if (ImGui.Checkbox("Mateus", ref mateusWorldValue))
                {
                    this.Configuration.MateusWorld = mateusWorldValue;
                    this.Configuration.Save();
                }

                var zaleraWorldValue = this.Configuration.ZaleraWorld;
                if (ImGui.Checkbox("Zalera", ref zaleraWorldValue))
                {
                    this.Configuration.ZaleraWorld = zaleraWorldValue;
                    this.Configuration.Save();
                }

                ImGui.Unindent();
                ImGui.TreePop();

            }
        }

        // Primal world selection
        if (primalValue)
        {

            if (ImGui.TreeNode("Primal World Selection"))
            {
                ImGui.Indent();

                var behemothWorldValue = this.Configuration.BehemothWorld;
                if (ImGui.Checkbox("Behemoth", ref behemothWorldValue))
                {
                    this.Configuration.BehemothWorld = behemothWorldValue;
                    this.Configuration.Save();
                }

                var excaliburWorldValue = this.Configuration.ExcaliburWorld;
                if (ImGui.Checkbox("Excalibur", ref excaliburWorldValue))
                {
                    this.Configuration.ExcaliburWorld = excaliburWorldValue;
                    this.Configuration.Save();
                }

                var exodusWorldValue = this.Configuration.ExodusWorld;
                if (ImGui.Checkbox("Exodus", ref exodusWorldValue))
                {
                    this.Configuration.ExodusWorld = exodusWorldValue;
                    this.Configuration.Save();
                }

                var famfritWorldValue = this.Configuration.FamfritWorld;
                if (ImGui.Checkbox("Famfrit", ref famfritWorldValue))
                {
                    this.Configuration.FamfritWorld = famfritWorldValue;
                    this.Configuration.Save();
                }

                var hyperionWorldValue = this.Configuration.HyperionWorld;
                if (ImGui.Checkbox("Hyperion", ref hyperionWorldValue))
                {
                    this.Configuration.HyperionWorld = hyperionWorldValue;
                    this.Configuration.Save();
                }

                var lamiaWorldValue = this.Configuration.LamiaWorld;
                if (ImGui.Checkbox("Lamia", ref lamiaWorldValue))
                {
                    this.Configuration.LamiaWorld = lamiaWorldValue;
                    this.Configuration.Save();
                }

                var leviathanWorldValue = this.Configuration.LeviathanWorld;
                if (ImGui.Checkbox("Leviathan", ref leviathanWorldValue))
                {
                    this.Configuration.LeviathanWorld = leviathanWorldValue;
                    this.Configuration.Save();
                }

                var ultrosWorldValue = this.Configuration.UltrosWorld;
                if (ImGui.Checkbox("Ultros", ref ultrosWorldValue))
                {
                    this.Configuration.UltrosWorld = ultrosWorldValue;
                    this.Configuration.Save();
                }

                ImGui.Unindent();
                ImGui.TreePop();

            }
        }

        // Dynamis world selection
        if (dynamisValue)
        {

            if (ImGui.TreeNode("Dynamis World Selection"))
            {
                ImGui.Indent();

                var halicarnassusWorldValue = this.Configuration.HalicarnassusWorld;
                if (ImGui.Checkbox("Halicarnassus", ref halicarnassusWorldValue))
                {
                    this.Configuration.HalicarnassusWorld = halicarnassusWorldValue;
                    this.Configuration.Save();
                }

                var maduinWorldValue = this.Configuration.MaduinWorld;
                if (ImGui.Checkbox("Maduin", ref maduinWorldValue))
                {
                    this.Configuration.MaduinWorld = maduinWorldValue;
                    this.Configuration.Save();
                }

                var seraphWorldValue = this.Configuration.SeraphWorld;
                if (ImGui.Checkbox("Seraph", ref seraphWorldValue))
                {
                    this.Configuration.SeraphWorld = seraphWorldValue;
                    this.Configuration.Save();
                }

                ImGui.Unindent();
                ImGui.TreePop();

            }
        }

        // Light world selection
        if (lightValue)
        {

            if (ImGui.TreeNode("Light World Selection"))
            {
                ImGui.Indent();

                var alphaWorldValue = this.Configuration.AlphaWorld;
                if (ImGui.Checkbox("Alpha", ref alphaWorldValue))
                {
                    this.Configuration.AlphaWorld = alphaWorldValue;
                    this.Configuration.Save();
                }

                var lichWorldValue = this.Configuration.LichWorld;
                if (ImGui.Checkbox("Lich", ref lichWorldValue))
                {
                    this.Configuration.LichWorld = lichWorldValue;
                    this.Configuration.Save();
                }

                var odinWorldValue = this.Configuration.OdinWorld;
                if (ImGui.Checkbox("Odin", ref odinWorldValue))
                {
                    this.Configuration.OdinWorld = odinWorldValue;
                    this.Configuration.Save();
                }

                var phoenixWorldValue = this.Configuration.PhoenixWorld;
                if (ImGui.Checkbox("Phoenix", ref phoenixWorldValue))
                {
                    this.Configuration.PhoenixWorld = phoenixWorldValue;
                    this.Configuration.Save();
                }

                var raidenWorldValue = this.Configuration.RaidenWorld;
                if (ImGui.Checkbox("Raiden", ref raidenWorldValue))
                {
                    this.Configuration.RaidenWorld = raidenWorldValue;
                    this.Configuration.Save();
                }

                var shivaWorldValue = this.Configuration.ShivaWorld;
                if (ImGui.Checkbox("Shiva", ref shivaWorldValue))
                {
                    this.Configuration.ShivaWorld = shivaWorldValue;
                    this.Configuration.Save();
                }

                var twintaniaWorldValue = this.Configuration.TwintaniaWorld;
                if (ImGui.Checkbox("Twintania", ref twintaniaWorldValue))
                {
                    this.Configuration.TwintaniaWorld = twintaniaWorldValue;
                    this.Configuration.Save();
                }

                var zodiarkWorldValue = this.Configuration.ZodiarkWorld;
                if (ImGui.Checkbox("Zodiark", ref zodiarkWorldValue))
                {
                    this.Configuration.ZodiarkWorld = zodiarkWorldValue;
                    this.Configuration.Save();
                }

                ImGui.Unindent();
                ImGui.TreePop();

            }
        }

        // Chaos world selection
        if (chaosValue)
        {

            if (ImGui.TreeNode("Chaos World Selection"))
            {
                ImGui.Indent();

                var cerberusWorldValue = this.Configuration.CerberusWorld;
                if (ImGui.Checkbox("Cerberus", ref cerberusWorldValue))
                {
                    this.Configuration.CerberusWorld = cerberusWorldValue;
                    this.Configuration.Save();
                }

                var louisoixWorldValue = this.Configuration.LouisoixWorld;
                if (ImGui.Checkbox("Louisoix", ref louisoixWorldValue))
                {
                    this.Configuration.LouisoixWorld = louisoixWorldValue;
                    this.Configuration.Save();
                }

                var moogleWorldValue = this.Configuration.MoogleWorld;
                if (ImGui.Checkbox("Moogle", ref moogleWorldValue))
                {
                    this.Configuration.MoogleWorld = moogleWorldValue;
                    this.Configuration.Save();
                }

                var omegaWorldValue = this.Configuration.OmegaWorld;
                if (ImGui.Checkbox("Omega", ref omegaWorldValue))
                {
                    this.Configuration.OmegaWorld = omegaWorldValue;
                    this.Configuration.Save();
                }

                var phantomWorldValue = this.Configuration.PhantomWorld;
                if (ImGui.Checkbox("Phantom", ref phantomWorldValue))
                {
                    this.Configuration.PhantomWorld = phantomWorldValue;
                    this.Configuration.Save();
                }

                var ragnarokWorldValue = this.Configuration.RagnarokWorld;
                if (ImGui.Checkbox("Ragnarok", ref ragnarokWorldValue))
                {
                    this.Configuration.RagnarokWorld = ragnarokWorldValue;
                    this.Configuration.Save();
                }

                var sagittariusWorldValue = this.Configuration.SagittariusWorld;
                if (ImGui.Checkbox("Sagittarius", ref sagittariusWorldValue))
                {
                    this.Configuration.SagittariusWorld = sagittariusWorldValue;
                    this.Configuration.Save();
                }

                var sprigganWorldValue = this.Configuration.SprigganWorld;
                if (ImGui.Checkbox("Spriggan", ref sprigganWorldValue))
                {
                    this.Configuration.SprigganWorld = sprigganWorldValue;
                    this.Configuration.Save();
                }

                ImGui.Unindent();
                ImGui.TreePop();

            }
        }
        // Create a simple header
        ImGui.NewLine();
        ImGui.Text("Notification Sound Effect");

        // Optional: Draw a separator line
        ImGui.Separator();

        string[] soundEffects = new string[]
        {
            "None",
            "Sound 1",
            "Sound 2",
            "Sound 3",
            "Sound 4",
            "Sound 5",
            "Sound 6",
            "Sound 7",
            "Sound 8",
            "Sound 9",
            "Sound 10",
            "Sound 11",
            "Sound 12",
            "Sound 13",
            "Sound 14",
            "Sound 15",
            "Sound 16"
        };

        Dictionary<string, int> soundEffectsDict = new Dictionary<string, int>();
        for (int i = 0; i < soundEffects.Length; i++)
        {
            soundEffectsDict[soundEffects[i]] = i;
        }

        // Assuming soundEffect in Configuration is an index of the selected sound effect.
        var soundEffectIndex = this.Configuration.soundEffect;
        if (ImGui.Combo("", ref soundEffectIndex, soundEffects, soundEffects.Length))
        {
            // soundEffectIndex is the index of the selected item, which corresponds to the sound number.
            this.Configuration.soundEffect = soundEffectIndex; // Update the configuration

            if (soundEffectIndex != 0)
            {
                UIModule.PlayChatSoundEffect((uint)soundEffectIndex); // Play the selected sound effect
            }

            this.Configuration.Save(); // Save the configuration
        }

        //if (ImGui.Button("Test")) Plugin.Test();
    }
}
