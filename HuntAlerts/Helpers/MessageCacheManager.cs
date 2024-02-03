using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.CircularBuffers;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ECommons.Logging;
using System.Security.Cryptography.X509Certificates;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;

namespace HuntAlerts.Helpers;
#pragma warning disable IDE1006,IDE0040,IDE0044
public class MessageCacheManager : IDisposable //IDisposable because we will need to indicate that this class is to be manually disposed, it's not required but considered good to add
{

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private const int VK_CONTROL = 0x11; // Virtual-Key Code for the Control key

    DalamudLinkPayload[] PayloadList = new DalamudLinkPayload[20]; //initialize 20 cache entries
    HuntTrainMessage[] Messages = new HuntTrainMessage[20]; //initialize 20 cache entries
    int CommandCount = 0; // track command count

    public MessageCacheManager()
    {
        for (var i = 0u; i < 20; i++) 
        {
            PayloadList[i] = Svc.PluginInterface.AddChatLinkHandler(i, ProcessLinkPayload); //we are registering 20 commands here
        }
    }

    public DalamudLinkPayload AddMessage(HuntTrainMessage message)
    {
        var nextCommand = CommandCount % 20; //real counter will reset every 20 messages
        CommandCount++; //increase counter to indicate which slot should be used next time
        Messages[nextCommand] = message; //store the message
        return PayloadList[nextCommand]; //return link payload to the caller
    }

    void ProcessLinkPayload(uint cmd, SeString str)
    {
        bool? teleporterInstalled = Svc.PluginInterface.InstalledPlugins.FirstOrDefault(x => x.InternalName == "TeleporterPlugin")?.IsLoaded;
        bool? lifestreamInstalled = Svc.PluginInterface.InstalledPlugins.FirstOrDefault(x => x.InternalName == "Lifestream")?.IsLoaded;
        bool ctrlHeld = (GetKeyState(VK_CONTROL) & 0x8000) != 0; // Check if Ctrl key is held down
        bool ctrlclickTeleport = HuntAlerts.P.Configuration.ctrlclickTeleport && ((teleporterInstalled == true && HuntAlerts.P.Configuration.TeleporterIntegration ) || (lifestreamInstalled == true && HuntAlerts.P.Configuration.LifestreamIntegration));
       
        if (Messages[cmd] != null) //if message with this number exists
        {
            if (ctrlHeld && ctrlclickTeleport)
            {
                var world = Messages[cmd].huntWorld;
                var startLocation = Messages[cmd].startLocation;
                var startZone = Messages[cmd].startZone;
                var locationCoords = Messages[cmd].locationCoords;
                var openmaponArrival = Messages[cmd].openmaponArrival;
                var teleporterEnabled = Messages[cmd].teleporterEnabled;
                var lifestreamEnabled = Messages[cmd].lifestreamEnabled;


                PluginLog.Verbose("Ctrl key is held down. attempting to teleport");
                Utilities.ExecuteTeleport(world, startLocation, startZone, locationCoords, openmaponArrival, teleporterEnabled, lifestreamEnabled);

            }
            else
            {
                HuntAlerts.P.NotifyWindow.IsOpen = true; //open a window
                HuntAlerts.P.NotifyWindow.CurrentMessage = Messages[cmd]; //set window's message to it
            }
        }
    }

    public List<HuntTrainMessage> GetOrderedMessages()
    {
        List<HuntTrainMessage> orderedMessages = new List<HuntTrainMessage>();
        int oldestIndex = CommandCount % 20; // Index of the oldest message

        // Iterate over the Messages array starting from the oldest message
        for (int i = 0; i < 20; i++)
        {
            int currentIndex = (oldestIndex + i) % 20; // Calculate the current index based on the oldest index
            if (Messages[currentIndex] != null)
            {
                orderedMessages.Add(Messages[currentIndex]);
            }
        }

        return orderedMessages;
    }

    public void Dispose() 
    {
        Svc.PluginInterface.RemoveChatLinkHandler(); //We need to remove our registered links. Dalamud provides a method to unregister all at once, so we don't need to go through each one. 
    }
}
