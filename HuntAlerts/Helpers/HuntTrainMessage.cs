using System;
using System.Numerics;

namespace HuntAlerts.Helpers;

public class HuntTrainMessage(string message, string huntType, string huntKind, string huntWorld,
    string currentworldName, string currentregionName, string huntregionName, string posted_Time,
    long postedEpoch, string startLocation, uint startLocationAetheryteId, string startZone, int instance,
    string locationCoords, bool openmaponArrival, bool lifestreamEnabled, string creatureName = "")
{
    public string Message = message;
    public string huntType = huntType;
    public string huntKind = huntKind;
    public string huntWorld = huntWorld;
    public string currentworldName = currentworldName;
    public string currentregionName = currentregionName;
    public string huntregionName = huntregionName;
    public string Posted_Time = posted_Time;
    public long PostedEpoch = postedEpoch;
    public string startLocation = startLocation;
    public uint startLocationAetheryteId = startLocationAetheryteId;
    public string startZone = startZone;
    public int instance = instance;
    public string locationCoords = locationCoords;
    public bool openmaponArrival = openmaponArrival;
    public bool lifestreamEnabled = lifestreamEnabled;
    public string creatureName = creatureName ?? "";
}

public record struct HuntAlertMessage(
    string Message,
    string HuntType,
    string HuntKind,
    uint HuntWorldId,
    uint CurrentWorldId,
    uint CurrentWorldRegionGroupId,
    uint HuntWorldRegionGroupId,
    DateTimeOffset PostedTime,
    long PostedEpoch,
    uint StartingAetheryteId,
    uint StartingTerritoryTypeId,
    int Instance,
    Vector2? MapLocationCoords,
    uint? creatureNameId
);
