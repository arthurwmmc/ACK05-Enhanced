using Ack05Enhanced.Core;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace Ack05Enhanced.App;

internal sealed class RadialMenuOverlayForm : Form
{
    private const int OverlaySize = 520;
    private const int SafeMargin = 16;
    private const int EscapeHotKeyId = 0xA05;
    private const int WmHotKey = 0x0312;
    private const int WsExNoActivate = 0x08000000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTransparent = 0x00000020;

    private Ack05RadialMenuState? _state;
    private bool _hotKeyRegistered;

    internal RadialMenuOverlayForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        ClientSize = new Size(OverlaySize, OverlaySize);
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        Font = new Font("Cascadia Mono", 9F, FontStyle.Regular, GraphicsUnit.Point);
        DoubleBuffered = true;
        AccessibleName = "ACK05 Radial Menu";
    }

    internal event EventHandler? CancelRequested;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExNoActivate | WsExToolWindow | WsExTransparent;
            return parameters;
        }
    }

    internal void ShowState(Ack05RadialMenuState state)
    {
        if (!state.IsOpen)
        {
            HideOverlay();
            return;
        }

        _state = state;
        PlaceOverlay(state.Position);
        if (!Visible)
        {
            Show();
        }

        RegisterEscape();
        Invalidate();
    }

    internal void HideOverlay()
    {
        _state = null;
        UnregisterEscape();
        Hide();
    }

    protected override void OnPaint(PaintEventArgs eventArgs)
    {
        base.OnPaint(eventArgs);
        if (_state is not { } state || state.Labels.Length == 0)
        {
            return;
        }

        var graphics = eventArgs.Graphics;
        // A WinForms TransparencyKey window cannot preserve partially transparent
        // edge pixels. Aliased geometry and glyphs avoid blending against the key
        // color, which would otherwise leave a magenta fringe around the wheel.
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        var center = new PointF(ClientSize.Width / 2F, ClientSize.Height / 2F);
        const float outerRadius = 146F;
        const float innerRadius = 40F;
        const float labelRadius = 202F;
        var sweep = 360F / state.Labels.Length;

        using var normalFill = new SolidBrush(Color.FromArgb(248, 248, 245));
        using var selectedFill = new SolidBrush(Color.FromArgb(24, 24, 23));
        using var border = new Pen(Color.FromArgb(24, 24, 23), 1.4F);
        using var leader = new Pen(Color.FromArgb(78, 78, 74), 1F);
        using var selectedText = new SolidBrush(Color.White);
        using var normalText = new SolidBrush(Color.FromArgb(24, 24, 23));
        using var centerFont = new Font(Font.FontFamily, 8F, FontStyle.Regular);
        using var iconFont = new Font("Segoe MDL2 Assets", 18F, FontStyle.Regular);
        using var labelFont = new Font(Font.FontFamily, 9F, FontStyle.Regular);
        using var leaderHalo = new Pen(Color.White, 3F);

        for (var index = 0; index < state.Labels.Length; index++)
        {
            // Segment zero is centered at twelve o'clock; subsequent segments
            // advance clockwise in the same order shown by the editor.
            var start = -90F - sweep / 2F + index * sweep;
            using var wedge = RingWedge(center, innerRadius, outerRadius, start, sweep);
            var selected = index == state.SelectedIndex;
            graphics.FillPath(selected ? selectedFill : normalFill, wedge);
            graphics.DrawPath(border, wedge);

            var mid = DegreesToRadians(start + sweep / 2F);
            var typePoint = PointOnCircle(center, 98F, mid);
            var icon = ActionIcon(state.ActionKinds[index], state.Labels[index]);
            DrawCenteredText(graphics, icon, iconFont, selected ? Color.White : border.Color, typePoint);

            var lineStart = PointOnCircle(center, outerRadius + 5F, mid);
            var lineEnd = PointOnCircle(center, labelRadius - 22F, mid);
            graphics.DrawLine(leaderHalo, lineStart, lineEnd);
            graphics.DrawLine(leader, lineStart, lineEnd);

            var labelPoint = PointOnCircle(center, labelRadius, mid);
            var label = state.Labels[index];
            var size = graphics.MeasureString(label, labelFont, PointF.Empty, StringFormat.GenericTypographic);
            var cosine = Math.Cos(mid);
            var x = cosine > 0.2 ? labelPoint.X : cosine < -0.2 ? labelPoint.X - size.Width : labelPoint.X - size.Width / 2F;
            var y = labelPoint.Y - size.Height / 2F;
            DrawTextHalo(graphics, label, labelFont, new PointF(x, y));
            graphics.DrawString(label, labelFont, normalText, new PointF(x, y), StringFormat.GenericTypographic);

            var tickStart = PointOnCircle(center, outerRadius + 2F, mid);
            var tickEnd = PointOnCircle(center, outerRadius + 8F, mid);
            graphics.DrawLine(border, tickStart, tickEnd);
        }

        using var centerFill = new SolidBrush(Color.FromArgb(239, 239, 235));
        var centerBounds = new RectangleF(
            center.X - innerRadius,
            center.Y - innerRadius,
            innerRadius * 2F,
            innerRadius * 2F);
        graphics.FillEllipse(centerFill, centerBounds);
        graphics.DrawEllipse(border, centerBounds);
        DrawCenteredText(graphics, "release", centerFont, border.Color, center);
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotKey && message.WParam.ToInt32() == EscapeHotKeyId)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.WndProc(ref message);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UnregisterEscape();
        }

        base.Dispose(disposing);
    }

    private void PlaceOverlay(Ack05RadialMenuPosition position)
    {
        var pointer = Cursor.Position;
        var screen = Screen.FromPoint(pointer);
        var working = screen.WorkingArea;
        var desiredCenter = position == Ack05RadialMenuPosition.ActiveMonitorCenter
            ? new Point(working.Left + working.Width / 2, working.Top + working.Height / 2)
            : pointer;
        var desired = new Point(
            desiredCenter.X - Width / 2,
            desiredCenter.Y - Height / 2);
        var minimumX = working.Left + SafeMargin;
        var maximumX = Math.Max(minimumX, working.Right - SafeMargin - Width);
        var minimumY = working.Top + SafeMargin;
        var maximumY = Math.Max(minimumY, working.Bottom - SafeMargin - Height);
        Location = new Point(
            Math.Clamp(desired.X, minimumX, maximumX),
            Math.Clamp(desired.Y, minimumY, maximumY));
    }

    private void RegisterEscape()
    {
        if (_hotKeyRegistered || !IsHandleCreated)
        {
            return;
        }

        _hotKeyRegistered = RegisterHotKey(Handle, EscapeHotKeyId, 0, (uint)Keys.Escape);
    }

    private void UnregisterEscape()
    {
        if (!_hotKeyRegistered || !IsHandleCreated)
        {
            return;
        }

        UnregisterHotKey(Handle, EscapeHotKeyId);
        _hotKeyRegistered = false;
    }

    private static GraphicsPath RingWedge(
        PointF center,
        float innerRadius,
        float outerRadius,
        float startAngle,
        float sweepAngle)
    {
        var path = new GraphicsPath();
        var outer = CircleBounds(center, outerRadius);
        var inner = CircleBounds(center, innerRadius);
        path.AddArc(outer, startAngle, sweepAngle);
        path.AddArc(inner, startAngle + sweepAngle, -sweepAngle);
        path.CloseFigure();
        return path;
    }

    private static RectangleF CircleBounds(PointF center, float radius) =>
        new(center.X - radius, center.Y - radius, radius * 2F, radius * 2F);

    private static PointF PointOnCircle(PointF center, float radius, double radians) =>
        new(
            center.X + (float)(Math.Cos(radians) * radius),
            center.Y + (float)(Math.Sin(radians) * radius));

    private static double DegreesToRadians(float degrees) => degrees * Math.PI / 180D;

    private static void DrawCenteredText(
        Graphics graphics,
        string text,
        Font font,
        Color color,
        PointF center)
    {
        var size = graphics.MeasureString(text, font, PointF.Empty, StringFormat.GenericTypographic);
        using var brush = new SolidBrush(color);
        graphics.DrawString(
            text,
            font,
            brush,
            new PointF(center.X - size.Width / 2F, center.Y - size.Height / 2F),
            StringFormat.GenericTypographic);
    }

    private static void DrawTextHalo(Graphics graphics, string text, Font font, PointF location)
    {
        using var halo = new SolidBrush(Color.White);
        foreach (var offset in new[]
        {
            new PointF(-1F, 0F),
            new PointF(1F, 0F),
            new PointF(0F, -1F),
            new PointF(0F, 1F),
        })
        {
            graphics.DrawString(
                text,
                font,
                halo,
                new PointF(location.X + offset.X, location.Y + offset.Y),
                StringFormat.GenericTypographic);
        }
    }

    private static string ActionIcon(Ack05ActionKind kind, string label)
    {
        var semanticIcon = label.Trim().ToUpperInvariant() switch
        {
            "CUT" => "\uE8C6",
            "UNDO" => "\uE7A7",
            "ZOOM" => "\uE721",
            "BRUSH" => "\uE70F",
            "PLAY" => "\uE768",
            "MARK" => "\uE7C1",
            "BLADE" => "\uE8C6",
            "RIPPLE" => "\uE895",
            _ => string.Empty,
        };
        if (semanticIcon.Length > 0)
        {
            return semanticIcon;
        }

        return kind switch
        {
            Ack05ActionKind.KeyboardChord => "\uE765",
            Ack05ActionKind.MouseWheel => "\uE962",
            Ack05ActionKind.OpenTarget => "\uE8E5",
            Ack05ActionKind.ToggleLayer => "\uE8AB",
            _ => string.Empty,
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
