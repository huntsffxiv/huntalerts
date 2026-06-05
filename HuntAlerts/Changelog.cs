namespace HuntAlerts
{
    public static class Changelog
    {
        public const int Revision = 1;

        public sealed record Entry(string Version, string[] Features, string[] BugFixes);

        public static readonly Entry[] Entries =
        {
            new("1.4.0.10",
                Features: new[]
                {
                    "Toast notifications - a positionable on-screen banner for new alerts with fade / slide animations.",
                    "Navigation arrow that points toward a hunt while you are in its zone:",
                    "- Picks up map coordinates from Yell and Shout chat automatically (on by default). Enable Party chat too for hunt trains or treasure maps.",
                    "- Opening an s-rank notification window will set its destination to the mark.",
                    "- New Nav button on S Rank notifications to force it as the arrow's destination.",
                    "Option to disable chat alerts while keeping the popup, toast, and arrow.",
                    "Reorganized the settings page to accommodate the new features.",
                },
                BugFixes: new string[0]),
        };
    }
}
