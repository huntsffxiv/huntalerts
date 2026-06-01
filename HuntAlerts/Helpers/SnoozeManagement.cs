using ECommons.Logging;
using System;

namespace HuntAlerts
{
    public sealed partial class HuntAlerts
    {
        public DateTime? SnoozedUntilUtc { get; private set; }

        public bool IsSnoozed => SnoozedUntilUtc.HasValue && SnoozedUntilUtc.Value > DateTime.UtcNow;

        public TimeSpan SnoozeRemaining =>
            IsSnoozed ? SnoozedUntilUtc!.Value - DateTime.UtcNow : TimeSpan.Zero;

        public void Snooze(int minutes)
        {
            if (minutes <= 0) { ClearSnooze(); return; }
            SnoozedUntilUtc = DateTime.UtcNow.AddMinutes(minutes);
            PluginLog.Verbose($"Alerts snoozed for {minutes}m (until {SnoozedUntilUtc:HH:mm:ss} UTC).");
        }

        public void SnoozeDefault() => Snooze(Configuration?.SnoozeDefaultMinutes > 0 ? Configuration.SnoozeDefaultMinutes : 30);

        public void ClearSnooze()
        {
            SnoozedUntilUtc = null;
            PluginLog.Verbose("Alerts wake-up triggered; snooze cleared.");
        }

        public void ToggleSnooze()
        {
            if (IsSnoozed) ClearSnooze();
            else           SnoozeDefault();
        }
    }
}
