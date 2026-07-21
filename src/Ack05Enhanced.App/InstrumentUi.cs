using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace Ack05Enhanced.App;

internal static class InstrumentTheme
{
    internal static readonly Color Canvas = Color.FromArgb(226, 227, 225);
    internal static readonly Color Surface = Color.FromArgb(242, 242, 240);
    internal static readonly Color Raised = Color.FromArgb(249, 249, 247);
    internal static readonly Color Ink = Color.FromArgb(17, 18, 17);
    internal static readonly Color Muted = Color.FromArgb(91, 92, 89);
    internal static readonly Color Border = Color.FromArgb(190, 191, 187);
    internal static readonly Color SoftBorder = Color.FromArgb(207, 208, 204);
    internal static readonly Color Disabled = Color.FromArgb(157, 158, 154);

    internal static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = Math.Max(2, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        var path = new GraphicsPath();
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    internal static Color Mix(Color from, Color to, float amount)
    {
        amount = Math.Clamp(amount, 0F, 1F);
        return Color.FromArgb(
            (int)Math.Round(from.A + ((to.A - from.A) * amount)),
            (int)Math.Round(from.R + ((to.R - from.R) * amount)),
            (int)Math.Round(from.G + ((to.G - from.G) * amount)),
            (int)Math.Round(from.B + ((to.B - from.B) * amount)));
    }

    internal static float EaseOutCubic(float value)
    {
        var inverse = 1F - Math.Clamp(value, 0F, 1F);
        return 1F - (inverse * inverse * inverse);
    }

    internal static Color ResolveParentBackground(Control control)
    {
        for (var current = control.Parent; current is not null; current = current.Parent)
        {
            if (current is InstrumentPanel instrumentPanel)
            {
                return instrumentPanel.SurfaceColor;
            }

            if (current.BackColor.A == 255)
            {
                return current.BackColor;
            }
        }

        return Canvas;
    }
}

internal sealed class InstrumentPanel : Panel
{
    internal int CornerRadius { get; set; } = 18;
    internal Color SurfaceColor { get; set; } = InstrumentTheme.Surface;
    internal Color BorderColor { get; set; } = InstrumentTheme.SoftBorder;

    internal InstrumentPanel()
    {
        DoubleBuffered = true;
        BackColor = Color.Transparent;
        Padding = new Padding(1);
        ResizeRedraw = true;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(InstrumentTheme.ResolveParentBackground(this));
        var bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        using var path = InstrumentTheme.RoundedRectangle(bounds, CornerRadius);
        using var fill = new SolidBrush(SurfaceColor);
        using var border = new Pen(BorderColor, 1F);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);
    }
}

internal sealed class BufferedTableLayoutPanel : TableLayoutPanel
{
    internal BufferedTableLayoutPanel()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }
}

internal sealed class BufferedFlowLayoutPanel : FlowLayoutPanel
{
    internal BufferedFlowLayoutPanel()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }
}

internal sealed class InstrumentSplitContainer : SplitContainer
{
    internal InstrumentSplitContainer()
    {
        DoubleBuffered = true;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var splitter = SplitterRectangle;
        if (splitter.Width <= 0 || splitter.Height <= 0)
        {
            return;
        }

        using var background = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(background, splitter);
        var handle = Orientation == Orientation.Horizontal
            ? new Rectangle(
                splitter.Left + ((splitter.Width - 42) / 2),
                splitter.Top + ((splitter.Height - 4) / 2),
                42,
                4)
            : new Rectangle(
                splitter.Left + ((splitter.Width - 4) / 2),
                splitter.Top + ((splitter.Height - 42) / 2),
                4,
                42);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = InstrumentTheme.RoundedRectangle(handle, 2);
        using var fill = new SolidBrush(InstrumentTheme.Border);
        e.Graphics.FillPath(fill, path);
    }
}

internal sealed class InstrumentButton : Button
{
    private readonly System.Windows.Forms.Timer _motionTimer = new() { Interval = 15 };
    private readonly Stopwatch _motionClock = new();
    private float _hoverAmount;
    private float _hoverFrom;
    private float _hoverTarget;
    private bool _selected;

    internal InstrumentButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        Height = 38;
        Padding = new Padding(12, 0, 12, 0);
        _motionTimer.Tick += (_, _) => TickMotion();
    }

    internal bool Primary { get; set; }

    internal bool Selected
    {
        get => _selected;
        set
        {
            if (_selected == value)
            {
                return;
            }

            _selected = value;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        StartMotion(1F);
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        StartMotion(0F);
        base.OnMouseLeave(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(InstrumentTheme.ResolveParentBackground(this));
        var bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
        using var path = InstrumentTheme.RoundedRectangle(bounds, Math.Max(8, bounds.Height / 2));

        var dark = Primary || Selected;
        var baseFill = dark ? InstrumentTheme.Ink : InstrumentTheme.Raised;
        var hoverFill = dark ? Color.FromArgb(48, 49, 47) : Color.White;
        using var fill = new SolidBrush(InstrumentTheme.Mix(baseFill, hoverFill, _hoverAmount));
        using var border = new Pen(dark ? InstrumentTheme.Ink : InstrumentTheme.Border, 1F);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);

        var textColor = !Enabled
            ? InstrumentTheme.Disabled
            : dark ? Color.White : InstrumentTheme.Ink;
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            bounds,
            textColor,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);

        if (Focused && ShowFocusCues)
        {
            var focusBounds = Rectangle.Inflate(bounds, -4, -4);
            ControlPaint.DrawFocusRectangle(
                e.Graphics,
                focusBounds,
                dark ? Color.White : InstrumentTheme.Ink,
                dark ? InstrumentTheme.Ink : InstrumentTheme.Raised);
        }
    }

    protected override void OnGotFocus(EventArgs e)
    {
        Invalidate();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(EventArgs e)
    {
        Invalidate();
        base.OnLostFocus(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _motionTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void StartMotion(float target)
    {
        _hoverFrom = _hoverAmount;
        _hoverTarget = target;
        _motionClock.Restart();
        _motionTimer.Start();
    }

    private void TickMotion()
    {
        const float durationMs = 145F;
        var progress = Math.Clamp((float)_motionClock.Elapsed.TotalMilliseconds / durationMs, 0F, 1F);
        var eased = InstrumentTheme.EaseOutCubic(progress);
        _hoverAmount = _hoverFrom + ((_hoverTarget - _hoverFrom) * eased);
        Invalidate();
        if (progress < 1F)
        {
            return;
        }

        _hoverAmount = _hoverTarget;
        _motionTimer.Stop();
    }
}

internal sealed class EasedHeightPanel : Panel
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 15 };
    private readonly Stopwatch _clock = new();
    private int _fromHeight;
    private int _targetHeight;

    internal EasedHeightPanel()
    {
        DoubleBuffered = true;
        _timer.Tick += (_, _) => TickMotion();
    }

    internal int ExpandedHeight { get; set; } = 96;
    internal bool IsExpanded => _targetHeight > 0;

    internal void SetExpanded(bool expanded, bool animate = true)
    {
        _fromHeight = Height;
        _targetHeight = expanded ? ExpandedHeight : 0;
        if (!animate)
        {
            Height = _targetHeight;
            return;
        }

        _clock.Restart();
        _timer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void TickMotion()
    {
        const float durationMs = 180F;
        var progress = Math.Clamp((float)_clock.Elapsed.TotalMilliseconds / durationMs, 0F, 1F);
        var eased = InstrumentTheme.EaseOutCubic(progress);
        Height = (int)Math.Round(_fromHeight + ((_targetHeight - _fromHeight) * eased));
        if (progress < 1F)
        {
            return;
        }

        Height = _targetHeight;
        _timer.Stop();
    }
}
