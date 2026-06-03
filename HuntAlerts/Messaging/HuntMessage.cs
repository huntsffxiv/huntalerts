using System.Collections.Generic;

namespace HuntAlerts.Messaging;

public class HuntMessage
{
    public required string Type { get; set; }
    public required string Content { get; set; }
    public required string World { get; set; }
    public required string Kind { get; set; }
    public required long Posted_Epoch { get; set; }
    public required string CreatureName { get; set; }
    public required string LocationName { get; set; }
    public required string LocationCoords { get; set; }
    public required string AetheryteName { get; set; }
    public int Instance { get; set; }
    public long DeathTime { get; set; }
    public required Dictionary<string, object> AdditionalData { get; set; }
}
