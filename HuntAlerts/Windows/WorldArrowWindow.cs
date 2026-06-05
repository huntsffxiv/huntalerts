using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ECommons.Logging;
using HuntAlerts.Helpers;
using HuntAlerts.Services;
using Lumina.Excel.Sheets;
using System;
using System.Numerics;

namespace HuntAlerts.Windows;

public class WorldArrowWindow : Window
{
    private const float ArrowAreaSize     = 80f;
    private const float ArrowRadius       = 28f;
    private const float ArrivalRadiusYalm = 12f;

    public WorldArrowWindow() : base("##huntalerts_world_arrow",
        ImGuiWindowFlags.NoTitleBar    |
        ImGuiWindowFlags.NoResize      |
        ImGuiWindowFlags.NoScrollbar   |
        ImGuiWindowFlags.NoBackground  |
        ImGuiWindowFlags.NoCollapse    |
        ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoFocusOnAppearing)
    {
        IsOpen              = true;
        RespectCloseHotkey  = false;
        DisableWindowSounds = true;
        Position            = new Vector2(200, 200);
        PositionCondition   = ImGuiCond.FirstUseEver;
    }

    private static long _lastAngleLog;

    public override bool DrawConditions()
    {
        if (!HuntAlerts.C.WorldArrowEnabled && !ArrowWaypoint.Forced) return false;
        if (!Svc.ClientState.IsLoggedIn || Svc.Objects.LocalPlayer == null) return false;
        if (!ArrowWaypoint.IsActive) return false;
        if (Svc.ClientState.TerritoryType != ArrowWaypoint.TerritoryTypeId) return false;

        if (!string.IsNullOrEmpty(ArrowWaypoint.WorldName))
        {
            var world = Svc.Objects.LocalPlayer?.CurrentWorld.ValueNullable?.Name.ExtractText() ?? "";
            if (!world.Equals(ArrowWaypoint.WorldName, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    public override void Draw()
    {
        var player = Svc.Objects.LocalPlayer;
        if (player == null || !ArrowWaypoint.IsActive) return;

        try
        {
            var territory = Svc.Data.GetExcelSheet<TerritoryType>().GetRow(ArrowWaypoint.TerritoryTypeId);
            var mapRef    = territory.Map.ValueNullable;
            if (mapRef == null) return;

            var sizeFactor = mapRef.Value.SizeFactor == 0 ? (ushort)100 : mapRef.Value.SizeFactor;
            var scale      = sizeFactor / 100f;
            var worldX     = ((ArrowWaypoint.MapX - 1f) * 2048f / 41f) - (1024f / scale) - mapRef.Value.OffsetX;
            var worldZ     = ((ArrowWaypoint.MapY - 1f) * 2048f / 41f) - (1024f / scale) - mapRef.Value.OffsetY;
            var target     = new Vector3(worldX, player.Position.Y, worldZ);

            var dx   = target.X - player.Position.X;
            var dz   = target.Z - player.Position.Z;
            var dist = MathF.Sqrt(dx * dx + dz * dz);

            var pPos = player.Position;
            Svc.GameGui.WorldToScreen(pPos,                           out var s0);
            Svc.GameGui.WorldToScreen(pPos + new Vector3(1f, 0f, 0f), out var sx);
            Svc.GameGui.WorldToScreen(pPos + new Vector3(0f, 0f, 1f), out var sz);

            var bx = sx - s0;
            var bz = sz - s0;

            float rotation;
            var det = bx.X * bz.Y - bz.X * bx.Y;
            if (MathF.Abs(det) > 1e-3f)
            {
                var upX = bz.X / det;  var upZ = -bx.X / det;
                var rtX = bz.Y / det;  var rtZ = -bx.Y / det;
                var camAz   = MathF.Atan2(upX, upZ);
                var camRtAz = MathF.Atan2(rtX, rtZ);
                var hand    = NormalizeAngle(camRtAz - camAz) > 0f ? 1f : -1f;
                var targetAz = MathF.Atan2(dx, dz);
                rotation = hand * NormalizeAngle(targetAz - camAz);
            }
            else
            {
                var screenVec = bx * dx + bz * dz;
                rotation = MathF.Atan2(screenVec.Y, screenVec.X) + MathF.PI / 2f;
            }
            var color = 0xFFE08828u;

            if (Environment.TickCount64 - _lastAngleLog > 1000)
            {
                _lastAngleLog = Environment.TickCount64;
                PluginLog.Verbose($"[WorldArrow] dx={dx:F1} dz={dz:F1} rotDeg={rotation * 180f / MathF.PI:F0} dist={dist:F0}");
            }

            var cursorScreen = ImGui.GetCursorScreenPos();
            var arrowCenter  = new Vector2(cursorScreen.X + ArrowAreaSize * 0.5f,
                                           cursorScreen.Y + ArrowAreaSize * 0.5f);

            var hovered = ImGui.IsWindowHovered();
            if (hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                ArrowWaypoint.Clear();
                return;
            }
            if (hovered)
                ImGui.SetTooltip($"HuntAlerts Nav\n({ArrowWaypoint.MapX:F1}, {ArrowWaypoint.MapY:F1})\nRight-click to clear");

            var ring = ImGui.GetWindowDrawList();
            ring.AddCircleFilled(arrowCenter, ArrowAreaSize * 0.5f, hovered ? 0x40FFFFFFu : 0x18000000u);

            if (dist < ArrivalRadiusYalm)
                DrawArrived(arrowCenter, color);
            else
                DrawArrow(arrowCenter, rotation, color);

            ImGui.Dummy(new Vector2(ArrowAreaSize, ArrowAreaSize));

            var distText  = dist < ArrivalRadiusYalm ? "Arrived" : $"{dist:F0}y";
            var textSize  = ImGui.CalcTextSize(distText);
            var textStart = new Vector2(
                cursorScreen.X + (ArrowAreaSize - textSize.X) * 0.5f - 6f,
                ImGui.GetCursorScreenPos().Y);
            var bgMin = textStart;
            var bgMax = textStart + new Vector2(textSize.X + 12f, textSize.Y + 4f);
            var draw  = ImGui.GetWindowDrawList();
            draw.AddRectFilled(bgMin, bgMax, 0xCC000000u, 6f);
            draw.AddText(new Vector2(textStart.X + 6f, textStart.Y + 2f), 0xFFFFFFFFu, distText);
            ImGui.Dummy(new Vector2(ArrowAreaSize, textSize.Y + 6f));
        }
        catch (Exception ex)
        {
            PluginLog.Verbose($"World arrow draw failed: {ex.Message}");
        }
    }

    private static void DrawArrow(Vector2 center, float angle, uint fill)
    {
        var drawList = ImGui.GetWindowDrawList();
        var r = ArrowRadius;

        var tip   = center + Rotate(new Vector2(0,          -r),         angle);
        var left  = center + Rotate(new Vector2(-r * 0.72f,  r * 0.55f), angle);
        var notch = center + Rotate(new Vector2(0,           r * 0.28f), angle);
        var right = center + Rotate(new Vector2( r * 0.72f,  r * 0.55f), angle);

        var tipO   = center + Rotate(new Vector2(0,              -r - 2.5f),       angle);
        var leftO  = center + Rotate(new Vector2(-r * 0.72f - 2f, r * 0.55f + 2f), angle);
        var notchO = center + Rotate(new Vector2(0,               r * 0.28f + 2f), angle);
        var rightO = center + Rotate(new Vector2( r * 0.72f + 2f, r * 0.55f + 2f), angle);
        drawList.AddQuadFilled(tipO, leftO, notchO, rightO, 0xB0000000u);

        var lit    = 0xFFFFC850u;
        var shadow = 0xFFC05A08u;
        drawList.AddTriangleFilled(tip, left,  notch, lit);
        drawList.AddTriangleFilled(tip, notch, right, shadow);
    }

    private static void DrawArrived(Vector2 center, uint fill)
    {
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddCircle(center, ArrowRadius * 0.8f, 0xCC000000u, 0, 5f);
        drawList.AddCircle(center, ArrowRadius * 0.8f, fill, 0, 3f);
        drawList.AddCircleFilled(center, 5f, fill);
    }

    private static Vector2 Rotate(Vector2 v, float angle)
    {
        var c = MathF.Cos(angle);
        var s = MathF.Sin(angle);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }

    private static float NormalizeAngle(float a)
    {
        while (a >   MathF.PI) a -= MathF.PI * 2f;
        while (a <= -MathF.PI) a += MathF.PI * 2f;
        return a;
    }
}
