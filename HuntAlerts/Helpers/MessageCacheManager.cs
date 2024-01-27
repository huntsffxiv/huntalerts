using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.CircularBuffers;
using ECommons.DalamudServices;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HuntAlerts.Helpers;
public class MessageCacheManager : IDisposable //IDisposable because we will need to indicate that this class is to be manually disposed, it's not required but considered good to add
{
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
        var nextCommand = CommandCount % 20;
        CommandCount++;
        Messages[nextCommand] = message;
        return PayloadList[nextCommand];
    }

    void ProcessLinkPayload(uint cmd, SeString str)
    {
        if (Messages[cmd] != null)
        {
            HuntAlerts.P.NotifyWindow.IsOpen = true;
            HuntAlerts.P.NotifyWindow.CurrentMessage = Messages[cmd];
        }
    }

    public void Dispose() 
    {
        Svc.PluginInterface.RemoveChatLinkHandler(); //We need to remove our registered links. Dalamud provides a method to unregister all at once, so we don't need to go through each one. 
    }
}
