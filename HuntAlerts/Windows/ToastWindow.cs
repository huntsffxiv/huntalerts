using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using HuntAlerts.Helpers;
using HuntAlerts.Services;
using System;
using System.Numerics;

namespace HuntAlerts.Windows;

public class ToastWindow : Window
{
    private HuntTrainMessage? msg;
    private long shownAtMs = -1;

    public bool PositionMode;
    private bool resetPos;
    private bool posApplied;

    private const float FadeMs    = 450f;
    private const float RiseMs    = 220f;
    private const float SlideDist = 320f;

    public ToastWindow() : base("##huntalerts_toast",
        ImGuiWindowFlags.NoTitleBar      |
        ImGuiWindowFlags.NoResize        |
        ImGuiWindowFlags.NoScrollbar     |
        ImGuiWindowFlags.NoScrollWithMouse |
        ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoFocusOnAppearing |
        ImGuiWindowFlags.NoNav           |
        ImGuiWindowFlags.NoSavedSettings |
        ImGuiWindowFlags.NoBackground)
    {
        IsOpen              = true;
        RespectCloseHotkey  = false;
        DisableWindowSounds = true;
    }

    public void Show(HuntTrainMessage m)
    {
        msg       = m;
        shownAtMs = Environment.TickCount64;
    }

    public void ResetPosition()
    {
        resetPos     = true;
        PositionMode = true;
    }

    private float DurationMs =>
        Math.Max(1, HuntAlerts.C.ToastDurationSeconds) * 1000f;

    public override bool DrawConditions()
    {
        if (PositionMode) return true;
        if (!HuntAlerts.C.ToastEnabled) return false;
        if (msg == null || shownAtMs < 0) return false;
        return Environment.TickCount64 - shownAtMs < DurationMs;
    }

    private float Presence()
    {
        if (PositionMode) return 1f;
        var elapsed   = Environment.TickCount64 - shownAtMs;
        var remaining = DurationMs - elapsed;
        var rise      = Math.Clamp(elapsed / RiseMs, 0f, 1f);
        var fall      = remaining < FadeMs ? Math.Clamp(remaining / FadeMs, 0f, 1f) : 1f;
        return Math.Min(rise, fall);
    }

    public override void PreDraw()
    {
        var anim     = HuntAlerts.C.ToastAnimation;
        var presence = Presence();
        var ease     = 1f - MathF.Pow(1f - presence, 3f);
        var slides   = !PositionMode && (anim == ToastAnimation.Slide || anim == ToastAnimation.SlideFade);
        var fades    = PositionMode ? false : (anim == ToastAnimation.Fade || anim == ToastAnimation.SlideFade);

        var vp = ImGui.GetMainViewport();
        var defaultPos = new Vector2(vp.Pos.X + vp.Size.X * 0.5f - 150f, vp.Pos.Y + 70f);
        var rest = HuntAlerts.C.ToastPosSet
            ? new Vector2(HuntAlerts.C.ToastPosX, HuntAlerts.C.ToastPosY)
            : defaultPos;

        if (!PositionMode) posApplied = false;

        if (resetPos)
        {
            HuntAlerts.C.ToastPosSet = false;
            HuntAlerts.C.Save();
            ImGui.SetNextWindowPos(defaultPos, ImGuiCond.Always);
            posApplied = true;
            resetPos   = false;
        }
        else if (PositionMode)
        {
            if (!posApplied)
            {
                ImGui.SetNextWindowPos(rest, ImGuiCond.Always);
                posApplied = true;
            }
        }
        else
        {
            var off = slides && presence < 1f ? (1f - ease) * SlideDist : 0f;
            ImGui.SetNextWindowPos(new Vector2(rest.X + off, rest.Y), ImGuiCond.Always);
        }

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 9f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(14f, 6f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(2);
    }

    private float FadeAlpha()
    {
        var anim = HuntAlerts.C.ToastAnimation;
        if (PositionMode) return 1f;
        if (anim == ToastAnimation.Fade || anim == ToastAnimation.SlideFade) return Presence();
        return 1f;
    }

    public override void Draw()
    {
        bool   isTrain;
        string kind, world, time;

        if (msg != null)
        {
            isTrain = msg.huntType == "new_hunt";
            kind    = string.IsNullOrEmpty(msg.huntKind)    ? "Hunt" : msg.huntKind;
            world   = msg.huntWorld    ?? "";
            time    = msg.Posted_Time  ?? "";
        }
        else if (PositionMode)
        {
            isTrain = true; kind = "Dawntrail"; world = "Hyperion"; time = "12:00 PM";
        }
        else return;

        var accent = isTrain ? Theme.TrainBorder : Theme.SRankBorder;

        if (!PositionMode && msg != null && ImGui.IsWindowHovered())
        {
            shownAtMs = Environment.TickCount64 - (long)(DurationMs * 0.5f);
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                Service.NotifyWindow.CurrentMessage = msg;
                Service.NotifyWindow.IsOpen = true;
                shownAtMs = -1;
                return;
            }
        }

        if (PositionMode && !ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            var p = ImGui.GetWindowPos();
            if (!HuntAlerts.C.ToastPosSet ||
                MathF.Abs(p.X - HuntAlerts.C.ToastPosX) > 0.5f ||
                MathF.Abs(p.Y - HuntAlerts.C.ToastPosY) > 0.5f)
            {
                HuntAlerts.C.ToastPosSet = true;
                HuntAlerts.C.ToastPosX   = p.X;
                HuntAlerts.C.ToastPosY   = p.Y;
                HuntAlerts.C.Save();
            }
        }

        var fade = FadeAlpha();
        DrawBackground(accent, fade);

        var rowTop = ImGui.GetCursorPosY();
        var padY   = Theme.BadgePadding.Y;

        void Badge(string text, BadgeStyle s)
        {
            ImGui.SetCursorPosY(rowTop);
            Components.Badge(text, s, fade);
        }
        void Text(string text, uint color)
        {
            ImGui.SetCursorPosY(rowTop + padY);
            ImGui.PushStyleColor(ImGuiCol.Text, Theme.WithAlpha(color, fade));
            ImGui.TextUnformatted(text);
            ImGui.PopStyleColor();
        }

        ImGui.Dummy(new Vector2(2f, 0f));
        ImGui.SameLine();
        Badge(isTrain ? "TRAIN" : "S RANK", isTrain ? BadgeStyle.Train : BadgeStyle.SRank);

        ImGui.SameLine();
        Text($"{kind} {(isTrain ? "train" : "S Rank")}", Theme.Text);

        if (!string.IsNullOrEmpty(world))
        {
            ImGui.SameLine();
            Text("·", Theme.Subtle);
            ImGui.SameLine();
            Badge(world, BadgeStyle.World);
        }

        if (!string.IsNullOrEmpty(time))
        {
            ImGui.SameLine();
            Text($"·  {time}", Theme.Subtle);
        }

        if (PositionMode)
        {
            ImGui.SameLine();
            Text("   (drag to move)", Theme.Accent);
        }
    }

    private static uint Pack(int a, int b, int g, int r) =>
        ((uint)a << 24) | ((uint)b << 16) | ((uint)g << 8) | (uint)r;

    private static uint TintDark(uint accent, float t, int floorB, int floorG, int floorR)
    {
        var ab = (int)((accent >> 16) & 0xFF);
        var ag = (int)((accent >> 8)  & 0xFF);
        var ar = (int)(accent & 0xFF);
        var b  = Math.Clamp(floorB + (int)(ab * t), 0, 255);
        var g  = Math.Clamp(floorG + (int)(ag * t), 0, 255);
        var r  = Math.Clamp(floorR + (int)(ar * t), 0, 255);
        return Pack(0xF0, b, g, r);
    }

    private void DrawBackground(uint accent, float fade)
    {
        var origin = ImGui.GetWindowPos();
        var size   = ImGui.GetWindowSize();
        var draw   = ImGui.GetWindowDrawList();
        var min    = origin;
        var max    = origin + size;

        var darkL = Theme.WithAlpha(TintDark(accent, 0.22f, 16, 14, 20), fade);
        var darkR = Theme.WithAlpha(Pack(0xF0, 14, 12, 18),              fade);

        draw.AddRectFilled(min, max, darkR, 9f);
        var inMin = new Vector2(min.X + 1.5f, min.Y + 1.5f);
        var inMax = new Vector2(max.X - 1.5f, max.Y - 1.5f);

        draw.AddRectFilledMultiColor(inMin, inMax, darkL, darkR, darkR, darkL);

        var sheen = Theme.WithAlpha(0x22FFFFFFu, fade);
        var clear = Theme.WithAlpha(0x00FFFFFFu, fade);
        draw.AddRectFilledMultiColor(
            inMin, new Vector2(inMax.X, min.Y + size.Y * 0.5f),
            sheen, sheen, clear, clear);

        var glowL = Theme.WithAlpha(0x70000000u | (accent & 0x00FFFFFFu), fade);
        var glowR = Theme.WithAlpha(0x00000000u | (accent & 0x00FFFFFFu), fade);
        draw.AddRectFilledMultiColor(
            inMin, new Vector2(min.X + 120f, inMax.Y),
            glowL, glowR, glowR, glowL);

        draw.AddRectFilled(min, new Vector2(min.X + 4f, max.Y), Theme.WithAlpha(accent, fade), 9f);

        var borderCol = Theme.WithAlpha(PositionMode ? accent : 0x55FFFFFFu, fade);
        draw.AddRect(min, max, borderCol, 9f, ImDrawFlags.None, PositionMode ? 2f : 1.2f);
    }
}
