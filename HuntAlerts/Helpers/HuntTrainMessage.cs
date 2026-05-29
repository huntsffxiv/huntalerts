namespace HuntAlerts.Helpers;
public class HuntTrainMessage
{
    public string Message;
    public string huntType;
    public string huntKind;
    public string huntWorld;
    public string currentworldName;
    public string currentregionName;
    public string huntregionName;
    public string Posted_Time;
    public long   PostedEpoch;
    public string startLocation;
    public uint startLocationAetheryteId;
    public string startZone;
    public int instance;
    public string locationCoords;
    public bool openmaponArrival;
    public bool lifestreamEnabled;

    public HuntTrainMessage(string message, string huntType, string huntKind, string huntWorld,
        string currentworldName, string currentregionName, string huntregionName, string posted_Time,
        long postedEpoch, string startLocation, uint startLocationAetheryteId, string startZone, int instance,
        string locationCoords, bool openmaponArrival, bool lifestreamEnabled)
    {
        this.Message = message;
        this.huntType = huntType;
        this.huntKind = huntKind;
        this.huntWorld = huntWorld;
        this.currentworldName = currentworldName;
        this.currentregionName = currentregionName;
        this.huntregionName = huntregionName;
        this.Posted_Time = posted_Time;
        this.PostedEpoch = postedEpoch;
        this.startLocation = startLocation;
        this.startLocationAetheryteId = startLocationAetheryteId;
        this.startZone = startZone;
        this.instance = instance;
        this.locationCoords = locationCoords;
        this.openmaponArrival = openmaponArrival;
        this.lifestreamEnabled = lifestreamEnabled;
    }
}
