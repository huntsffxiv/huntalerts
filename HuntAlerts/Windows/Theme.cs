using System.Numerics;

namespace HuntAlerts.Windows;

internal static class Theme
{
    public static readonly uint Accent  = 0xFFF2B36D;
    public static readonly uint Subtle  = 0xFF888888;
    public static readonly uint Text    = 0xFFE0E0E0;

    public static readonly uint SRankBg     = 0xFF1E1E5C;
    public static readonly uint SRankBorder = 0xFF4040A0;
    public static readonly uint SRankText   = 0xFF8A8AFF;

    public static readonly uint TrainBg     = 0xFF5C3A1E;
    public static readonly uint TrainBorder = 0xFFC08040;
    public static readonly uint TrainText   = 0xFFFFC68A;

    public static readonly uint KindBg      = 0xFF2A2A2A;
    public static readonly uint KindBorder  = 0xFF4A4A4A;
    public static readonly uint KindText    = 0xFFB8B8B8;

    public static readonly uint WorldBg     = 0xFF1E3A1A;
    public static readonly uint WorldBorder = 0xFF508040;
    public static readonly uint WorldText   = 0xFF90D080;

    public static readonly uint ButtonOn        = 0xFF408040;
    public static readonly uint ButtonOnHover   = 0xFF50A050;
    public static readonly uint ButtonOnActive  = 0xFF306030;
    public static readonly uint ButtonOff       = 0xFF4040A0;
    public static readonly uint ButtonOffHover  = 0xFF5050C0;
    public static readonly uint ButtonOffActive = 0xFF303080;

    public static readonly uint AccentBtn         = 0xFFB5853D;
    public static readonly uint AccentBtnHover    = 0xFFD5A55D;
    public static readonly uint AccentBtnActive   = 0xFF8D652D;

    public static readonly uint WarnBtn           = 0xFF1070B5;
    public static readonly uint WarnBtnHover      = 0xFF3090D5;
    public static readonly uint WarnBtnActive     = 0xFF00508D;

    public static readonly uint InfoBtn           = 0xFF601BD8;
    public static readonly uint InfoBtnHover      = 0xFF804BF8;
    public static readonly uint InfoBtnActive     = 0xFF4000A8;

    public static readonly uint DangerBtn         = 0xFF3D3DB5;
    public static readonly uint DangerBtnHover    = 0xFF5D5DD5;
    public static readonly uint DangerBtnActive   = 0xFF2D2D8D;

    public static readonly uint SuccessBtn        = 0xFF3CA63C;
    public static readonly uint SuccessBtnHover   = 0xFF5CC65C;
    public static readonly uint SuccessBtnActive  = 0xFF2C862C;

    public static Vector2 BadgePadding => new(6, 1);

    public static readonly uint CardBg        = 0x18FFFFFF;
    public static readonly uint CardBgHover   = 0x30FFFFFF;
    public static readonly uint CardBorder    = 0x40FFFFFF;
}

internal enum BadgeStyle { SRank, Train, Kind, World }

internal enum ButtonRole { Accent, Warn, Info, Danger, Success }
