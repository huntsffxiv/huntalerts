using Dalamud.Game.Text;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace HuntAlerts.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    HuntAlerts Plugin;


    public ConfigWindow(HuntAlerts plugin) : base(
       "HuntAlerts",
       ImGuiWindowFlags.NoResize)
    {
        this.Plugin = plugin;
        this.Size = new Vector2(400, 980);
        this.SizeCondition = ImGuiCond.Always;

        this.Configuration = plugin.Configuration;
    }

    string ttString = "";
    public void Dispose() { }

    public override void Draw()
    { 
        // Create a simple header
        ImGui.Text("General Options");

        // Optional: Draw a separator line
        ImGui.Separator();

        var suppressDuplicates = this.Configuration.SuppressDuplicates;
        if(ImGui.Checkbox("Suppress Duplicate Messages", ref suppressDuplicates))
        {
            this.Configuration.SuppressDuplicates = suppressDuplicates;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }





        var openmaponArrival = this.Configuration.OpenMapOnArrival;
        if (ImGui.Checkbox("Flag Map automatically if possible", ref openmaponArrival))
        {
            this.Configuration.OpenMapOnArrival = openmaponArrival;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        var usedalamudChat = this.Configuration.UseDalamudChat;
        if (ImGui.Checkbox("Use Dalamud Default Chat", ref usedalamudChat))
        {
            this.Configuration.UseDalamudChat = usedalamudChat;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }


        if (this.Configuration.UseDalamudChat == true)
        {
            ImGui.BeginDisabled();
        }

        // Get all enum values except "None"
        var enumValues = Enum.GetValues(typeof(XivChatType))
                             .Cast<XivChatType>()
                             .Where(value => value != XivChatType.None) // Assuming XivChatType.None is the value you want to exclude
                             .ToArray();

        // Convert enum values to string array for display, excluding "None"
        var chatTypes = enumValues.Select(e => e.ToString()).ToArray();

        // Find the current index of OutputChat in the enumValues array (adjusted for exclusion of "None")
        var currentOutputChat = this.Configuration.OutputChat;
        int outputChatIndex = Array.IndexOf(enumValues, currentOutputChat);

        if (ImGui.Combo("Chat Channel", ref outputChatIndex, chatTypes, chatTypes.Length))
        {
            // Update the Configuration.OutputChat with the new enum value based on the selected index
            this.Configuration.OutputChat = enumValues[outputChatIndex];
            this.Configuration.Save();
        }
        if (this.Configuration.UseDalamudChat == true)
        {
            ImGui.EndDisabled();
        }


        // Local variable for color options
        Dictionary<string, int> _colorOptions = new Dictionary<string, int>
        {
            { "Default", 0 },
            { "Red", 16 },
            { "Green", 43 },
            { "Blue", 57 },
            { "Yellow", 25 },
            { "Purple", 48 }
        };

        string[] items = _colorOptions.Keys.ToArray();
        var textColor = Array.IndexOf(items, _colorOptions.FirstOrDefault(x => x.Value == this.Configuration.TextColor).Key);
        if (ImGui.Combo("Train Text Color", ref textColor, items, items.Length))
        {
            // Update the configuration when the selection changes
            this.Configuration.TextColor = _colorOptions[items[textColor]];
            this.Configuration.Save(); // Method to save your configuration
        }

        var sranktextColor = Array.IndexOf(items, _colorOptions.FirstOrDefault(x => x.Value == this.Configuration.SRankTextColor).Key);
        if (ImGui.Combo("S Rank Text Color", ref sranktextColor, items, items.Length))
        {
            // Update the configuration when the selection changes
            this.Configuration.SRankTextColor = _colorOptions[items[sranktextColor]];
            this.Configuration.Save(); // Method to save your configuration
        }
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
        var soundEffectIndex = this.Configuration.SoundEffect;
        if (ImGui.Combo("Sound Effect", ref soundEffectIndex, soundEffects, soundEffects.Length))
        {
            // soundEffectIndex is the index of the selected item, which corresponds to the sound number.
            this.Configuration.SoundEffect = soundEffectIndex; // Update the configuration

            if (soundEffectIndex != 0)
            {
                UIModule.PlayChatSoundEffect((uint)soundEffectIndex); // Play the selected sound effect
            }

            this.Configuration.Save(); // Save the configuration
        }


        // Add blank line
        ImGui.NewLine();

        // Create a simple header
        ImGui.Text("Integrations (Changes take effect next hunt message)");

        // Optional: Draw a separator line
        ImGui.Separator();

        bool? teleporterInstalled = Svc.PluginInterface.InstalledPlugins.FirstOrDefault(x => x.InternalName == "TeleporterPlugin")?.IsLoaded;
        bool? lifestreamInstalled = Svc.PluginInterface.InstalledPlugins.FirstOrDefault(x => x.InternalName == "Lifestream")?.IsLoaded;

        if (teleporterInstalled != true)
        {
            ImGui.BeginDisabled();
        }

        var teleporterIntegration = this.Configuration.TeleporterIntegration;
        if (ImGui.Checkbox("Enable Teleporter Integration", ref teleporterIntegration))
        {
            this.Configuration.TeleporterIntegration = teleporterIntegration;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }
        if (teleporterInstalled != true)
        {
            ImGui.EndDisabled();
        }


        if (lifestreamInstalled != true)
        {
            ImGui.BeginDisabled();
        }

        var lifestreamIntegration = this.Configuration.LifestreamIntegration;
        if (ImGui.Checkbox("Enable Lifestream Integration", ref lifestreamIntegration))
        {
            this.Configuration.LifestreamIntegration = lifestreamIntegration;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        if (lifestreamInstalled != true)
        {
            ImGui.EndDisabled();
        }

        if ((teleporterInstalled != true && lifestreamInstalled != true) || (!this.Configuration.TeleporterIntegration && !this.Configuration.LifestreamIntegration))
        {
            ImGui.BeginDisabled();

        }

        var ctrlclickTeleport = this.Configuration.ctrlclickTeleport;
        if (ImGui.Checkbox("Ctrl-Click messages to teleport", ref ctrlclickTeleport))
        {
            this.Configuration.ctrlclickTeleport = ctrlclickTeleport;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }


        if ((teleporterInstalled != true && lifestreamInstalled != true) || (!this.Configuration.TeleporterIntegration && !this.Configuration.LifestreamIntegration))
        {
            ImGui.EndDisabled();
        }

        ImGui.NewLine();
        ImGui.Text("S Rank Options");

        // Optional: Draw a separator line
        ImGui.Separator();

        ImGui.Columns(2, "", false); // 2 columns, no border

        var srankEnabledValue = this.Configuration.SRankEnabled;
        if (ImGui.Checkbox("S Ranks Enabled", ref srankEnabledValue))
        {
            this.Configuration.SRankEnabled = srankEnabledValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        ImGui.NextColumn();

        if (!srankEnabledValue)
        {
            ImGui.BeginDisabled();
        }
        var srankcurrentWorld = this.Configuration.SRankCurrentWorld;
        if (ImGui.Checkbox("Current World Only##SRank", ref srankcurrentWorld))
        {
            this.Configuration.SRankCurrentWorld = srankcurrentWorld;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }
       

        ImGui.Columns(1);

        // Add blank line
        ImGui.NewLine();

        // Create a simple header
        ImGui.Text("S Rank Notifications");

        // Optional: Draw a separator line
        ImGui.Separator();

        // can't ref a property, so use a local copy
        var endwalkerSRankValue = this.Configuration.EndwalkerSRank;
        if (ImGui.Checkbox("Endwalker##SRank", ref endwalkerSRankValue))
        {
            this.Configuration.EndwalkerSRank = endwalkerSRankValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        var shadowbringersSRankValue = this.Configuration.ShadowbringersSRank;
        if (ImGui.Checkbox("Shadowbringers##SRank", ref shadowbringersSRankValue))
        {
            this.Configuration.ShadowbringersSRank = shadowbringersSRankValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }
        var centurioSRankValue = this.Configuration.CenturioSRank;
        if (ImGui.Checkbox("Centurio##SRank", ref centurioSRankValue))
        {
            this.Configuration.CenturioSRank = centurioSRankValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        if (!srankEnabledValue)
        {
            ImGui.EndDisabled();
        }

        // Add blank line
        ImGui.NewLine();

        // Create a simple header
        ImGui.Text("Hunt Train Notifications");

        // Optional: Draw a separator line
        ImGui.Separator();

        // can't ref a property, so use a local copy
        var endwalkerValue = this.Configuration.EndwalkerHunts;
        if (ImGui.Checkbox("Endwalker##HuntTrains", ref endwalkerValue))
        {
            this.Configuration.EndwalkerHunts = endwalkerValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        var shadowbringersValue = this.Configuration.ShadowbringersHunts;
        if (ImGui.Checkbox("Shadowbringers##HuntTrains", ref shadowbringersValue))
        {
            this.Configuration.ShadowbringersHunts = shadowbringersValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }
        var centurioValue = this.Configuration.CenturioHunts;
        if (ImGui.Checkbox("Centurio##HuntTrains", ref centurioValue))
        {
            this.Configuration.CenturioHunts = centurioValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }
        

        // Create a simple header
        ImGui.NewLine();
        ImGui.Text("Hunt Train Options");

        // Optional: Draw a separator line
        ImGui.Separator();

        ImGui.Columns(2, "", false); // 2 columns, no border

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

        ImGui.NextColumn();

        var currentworldonlyValue = this.Configuration.CurrentWorldOnly;
        if (ImGui.Checkbox("Current World Only##HuntTrains", ref currentworldonlyValue))
        {
            this.Configuration.CurrentWorldOnly = currentworldonlyValue;
            if(currentworldonlyValue)
            {
                this.Configuration.HomeWorldOnly = false;
            }
            // can save immediately on change, if you don't want to provide a "Save and Close" button

            this.Configuration.Save();
        }

        ImGui.Columns(1);

        // Create a simple header
        ImGui.NewLine();
        ImGui.Text("Hunt Train Datacenter");

        // Optional: Draw a separator line
        ImGui.Separator();

        if(currentworldonlyValue || homeworldonlyValue)
        {
            ImGui.BeginDisabled();
        }

        // Start the columns
        ImGui.Columns(3, "", false); // 2 columns, no border

        var aetherValue = this.Configuration.Aether;
        if (ImGui.Checkbox("Aether", ref aetherValue))
        {
            this.Configuration.Aether = aetherValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        ImGui.NextColumn();

        var crystalValue = this.Configuration.Crystal;
        if (ImGui.Checkbox("Crystal", ref crystalValue))
        {
            this.Configuration.Crystal = crystalValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        ImGui.NextColumn();

        var primalValue = this.Configuration.Primal;
        if (ImGui.Checkbox("Primal", ref primalValue))
        {
            this.Configuration.Primal = primalValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        ImGui.NextColumn();

        var dynamisValue = this.Configuration.Dynamis;
        if (ImGui.Checkbox("Dynamis", ref dynamisValue))
        {
            this.Configuration.Dynamis = dynamisValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        ImGui.NextColumn();

        var lightValue = this.Configuration.Light;
        if (ImGui.Checkbox("Light", ref lightValue))
        {
            this.Configuration.Light = lightValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        ImGui.NextColumn();


        var chaosValue = this.Configuration.Chaos;
        if (ImGui.Checkbox("Chaos", ref chaosValue))
        {
            this.Configuration.Chaos = chaosValue;
            // can save immediately on change, if you don't want to provide a "Save and Close" button
            this.Configuration.Save();
        }

        ImGui.Columns(1);


        if (currentworldonlyValue || homeworldonlyValue)
        {
            ImGui.EndDisabled();
        }

        ImGui.NewLine();
        ImGui.Text("Hunt Train World Selection");
        ImGui.Separator();



        if (!aetherValue && !crystalValue && !primalValue && !dynamisValue && !lightValue && !chaosValue)
        {
            ImGui.Text("No Datacenters selected, please choose a datacenter");
        }

        if (currentworldonlyValue || homeworldonlyValue)
        {
            ImGui.BeginDisabled();
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

        if (currentworldonlyValue || homeworldonlyValue)
        {
            ImGui.EndDisabled();
        }
        
        /*if (ImGui.CollapsingHeader("Debug"))
        {
            ImGui.InputFloat("x", ref x);
            ImGui.InputFloat("y", ref y);
            ImGui.InputText("tt", ref ttString, 20);
            if (ImGui.Button("Test link"))
            {
                uint tt;
                PluginLog.Verbose($"Zone: {ttString}");
                
                if (Svc.Data.GetExcelSheet<TerritoryType>().TryGetFirst(x => x.TerritoryIntendedUse == (uint)TerritoryIntendedUseEnum.Open_World && (x.PlaceName.Value?.Name.ExtractText() ?? "").EqualsIgnoreCase(ttString), out var value))
                {
                    //string ttString = System.Text.Encoding.UTF8.GetString(ttBuffer).TrimEnd('\0');
                    PluginLog.Verbose($"Zone string: {ttString}");
                    tt = value.RowId; //is territory id
                    MapManager.OpenMapWithMarker(tt, x, y);
                }
            }
        }*/

        //if (ImGui.Button("Test")) Plugin.Test();
    }

    float x, y;
    int tt;

}
