using ECommons.EzIpcManager;
using HuntAlerts.Helpers;
using System;

namespace HuntAlerts.Services;

#nullable disable
public class IPCManager
{
    private IPCManager()
    {
        EzIPC.Init(this);
    }

    [EzIPCEvent] public Action<HuntTrainMessage> OnHuntTrainMessageReceived;
    [EzIPCEvent] public Action<HuntAlertMessage> OnHuntAlertMessageReceived;

    [EzIPC] public bool LifestreamIntegrationEnabled() => HuntAlerts.C.LifestreamIntegration;
    [EzIPC] public bool OpenMapOnArrivalEnabled() => HuntAlerts.C.OpenMapOnArrival;
}
