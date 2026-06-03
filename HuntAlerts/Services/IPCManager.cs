using ECommons.DalamudServices;
using ECommons.EzIpcManager;
using HuntAlerts.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HuntAlerts.Services;
public class IPCManager
{
    private IPCManager()
    {
        EzIPC.Init(this);
    }

    [EzIPCEvent] public Action<HuntTrainMessage> OnHuntTrainMessageReceived;
    [EzIPCEvent] public Action<HuntAlertMessage> OnHuntAlertMessageReceived;

    [EzIPC] public bool LifestreamIntegrationEnabled() => HuntAlerts.P.Configuration.LifestreamIntegration;
    [EzIPC] public bool OpenMapOnArrivalEnabled() => HuntAlerts.P.Configuration.OpenMapOnArrival;
}
