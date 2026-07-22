using Ack05Enhanced.Core;
using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace Ack05Enhanced.App;

internal sealed class Ack05DeviceMapControl : UserControl
{
    internal const string PaletteDragFormat = "Ack05Enhanced.PaletteSelection";

    private static readonly Color Canvas = InstrumentTheme.Canvas;
    private static readonly Color Surface = InstrumentTheme.Surface;
    private static readonly Color KeySurface = InstrumentTheme.Raised;
    private static readonly Color KeyHover = Color.White;
    private static readonly Color Accent = InstrumentTheme.Ink;

    private readonly Dictionary<Ack05Control, Ack05MapButton> _buttons = new();
    private readonly List<ComboBadge> _comboBadges = [];
    private readonly ToolTip _toolTip = new();
    private readonly Label _layerLabel = new();
    private readonly Ack05DeviceSurface _surface = new();
    private readonly Ack05RollerControl _roller = new();
    private Ack05Control? _selectedControl;

    internal Ack05DeviceMapControl()
    {
        BackColor = Canvas;
        Padding = new Padding(8);
        Dock = DockStyle.Fill;
        BuildLayout();
    }

    internal event Action<Ack05Control>? ControlSelected;
    internal event Action<Ack05Control>? ActionDropped;
    internal event Action<string>? ComboSelected;

    internal Ack05Control? SelectedControl
    {
        get => _selectedControl;
        set
        {
            if (_selectedControl == value)
            {
                return;
            }

            _selectedControl = value;
            UpdateSelection();
        }
    }

    internal void LoadMap(
        Ack05Profile profile,
        int layerIndex,
        bool showCombos = false)
    {
        var layer = profile.Layers.Single(item => item.Index == layerIndex);
        _layerLabel.Text = $"ACK05  •  Layer {layerIndex}";

        foreach (var pair in _buttons)
        {
            var action = ActionFor(layer, pair.Key);
            pair.Value.ActionText = CompactAction(action, profile);
            _toolTip.SetToolTip(pair.Value, $"{pair.Key}: {FullAction(action, profile)}");
        }

        _roller.LoadActions(
            ActionFor(layer, Ack05Control.RollerCounterClockwise),
            ActionFor(layer, Ack05Control.RollerClockwise),
            ActionFor(layer, Ack05Control.RollerPress));
        LoadComboBadges(profile, layerIndex, showCombos);
        UpdateSelection();
    }

    internal void HighlightControls(IEnumerable<Ack05Control> controls)
    {
        var highlighted = controls.ToHashSet();
        ClearComboHighlight();
        foreach (var pair in _buttons)
        {
            pair.Value.IsComboHighlighted = highlighted.Contains(pair.Key);
        }
        _roller.IsComboHighlighted = highlighted.Contains(Ack05Control.RollerPress);
    }

    private void BuildLayout()
    {
        _layerLabel.ForeColor = InstrumentTheme.Muted;
        _layerLabel.Dock = DockStyle.Top;
        _layerLabel.Height = 28;
        _layerLabel.Font = new Font(Font, FontStyle.Bold);
        _layerLabel.TextAlign = ContentAlignment.MiddleLeft;
        Controls.Add(_layerLabel);

        _surface.Dock = DockStyle.Fill;
        _surface.Layout += (_, _) => LayoutDevice();
        Controls.Add(_surface);
        _surface.BringToFront();

        _roller.ControlSelected += control =>
        {
            SelectedControl = control;
            ControlSelected?.Invoke(control);
        };
        _surface.Controls.Add(_roller);

        for (var index = 0; index < 10; index++)
        {
            var control = (Ack05Control)index;
            var button = new Ack05MapButton
            {
                KeyText = control.ToString(),
                AccessibleName = control.ToString(),
                AccessibleDescription = "Select this physical key to edit its action.",
                TabStop = true,
                AllowDrop = true,
            };
            button.Click += (_, _) => SelectKey(control, button);
            button.DragEnter += (_, eventArgs) =>
            {
                eventArgs.Effect = eventArgs.Data?.GetDataPresent(PaletteDragFormat) == true
                    ? DragDropEffects.Copy
                    : DragDropEffects.None;
            };
            button.DragDrop += (_, eventArgs) =>
            {
                if (eventArgs.Data?.GetDataPresent(PaletteDragFormat) != true)
                {
                    return;
                }

                ActionDropped?.Invoke(control);
            };
            _buttons.Add(control, button);
            _surface.Controls.Add(button);
        }
    }

    private void SelectKey(Ack05Control control, Control button)
    {
        SelectedControl = control;
        ControlSelected?.Invoke(control);
    }

    private void LayoutDevice()
    {
        if (_buttons.Count < 10)
        {
            return;
        }

        var available = Rectangle.Inflate(_surface.ClientRectangle, -8, -6);
        if (available.Width <= 0 || available.Height <= 0)
        {
            return;
        }

        const double aspect = 1.82;
        var width = Math.Min(available.Width, (int)Math.Round(available.Height * aspect));
        var height = Math.Min(available.Height, (int)Math.Round(width / aspect));
        var frame = new Rectangle(
            available.Left + (available.Width - width) / 2,
            available.Top + (available.Height - height) / 2,
            width,
            height);
        _surface.DeviceFrame = frame;

        SetBounds(_roller, frame, 0.025, 0.035, 0.325, 0.585);
        SetBounds(_buttons[Ack05Control.K1], frame, 0.400, 0.135, 0.125, 0.230);
        SetBounds(_buttons[Ack05Control.K2], frame, 0.540, 0.135, 0.125, 0.230);
        SetBounds(_buttons[Ack05Control.K3], frame, 0.680, 0.135, 0.125, 0.230);
        SetBounds(_buttons[Ack05Control.K7], frame, 0.820, 0.135, 0.125, 0.480);
        SetBounds(_buttons[Ack05Control.K4], frame, 0.400, 0.405, 0.125, 0.230);
        SetBounds(_buttons[Ack05Control.K5], frame, 0.540, 0.405, 0.125, 0.230);
        SetBounds(_buttons[Ack05Control.K6], frame, 0.680, 0.405, 0.125, 0.230);
        SetBounds(_buttons[Ack05Control.K8], frame, 0.400, 0.675, 0.125, 0.230);
        SetBounds(_buttons[Ack05Control.K9], frame, 0.540, 0.675, 0.265, 0.230);
        SetBounds(_buttons[Ack05Control.K10], frame, 0.820, 0.675, 0.125, 0.230);
        LayoutComboBadges(frame);
    }

    private void LoadComboBadges(Ack05Profile profile, int layerIndex, bool showCombos)
    {
        ClearComboHighlight();
        foreach (var badge in _comboBadges)
        {
            _surface.Controls.Remove(badge);
            badge.Dispose();
        }
        _comboBadges.Clear();

        if (!showCombos)
        {
            return;
        }

        foreach (var definition in profile.Combos.Where(combo =>
                     combo.Controls.Length == 2 && combo.Layers.Contains(layerIndex)))
        {
            var scope = ScopeLabel(definition, profile.Layers.Length);
            var badge = new ComboBadge
            {
                ComboId = definition.Id,
                MemberControls = [definition.Controls[0], definition.Controls[1]],
                OutputText = CompactAction(definition.Action, profile),
                ScopeText = scope,
                AccessibleName = $"Combo {definition.Controls[0]} plus {definition.Controls[1]} to {FullAction(definition.Action, profile)}",
                AccessibleDescription = "Open this definition in the Combos workspace.",
                TabStop = true,
            };
            _toolTip.SetToolTip(
                badge,
                $"{definition.Name}: {definition.Controls[0]} + {definition.Controls[1]} → {FullAction(definition.Action, profile)} ({scope})");
            badge.MouseEnter += (_, _) => HighlightCombo(badge.MemberControls);
            badge.MouseLeave += (_, _) => ClearComboHighlight();
            badge.GotFocus += (_, _) => HighlightCombo(badge.MemberControls);
            badge.LostFocus += (_, _) => ClearComboHighlight();
            badge.Click += (_, _) => ComboSelected?.Invoke(badge.ComboId);
            _comboBadges.Add(badge);
            _surface.Controls.Add(badge);
            badge.BringToFront();
        }

        LayoutComboBadges(_surface.DeviceFrame);
    }

    private void LayoutComboBadges(Rectangle frame)
    {
        if (frame.Width <= 0 || frame.Height <= 0 || _comboBadges.Count == 0)
        {
            return;
        }

        var occupied = new List<Rectangle>();
        foreach (var badge in _comboBadges)
        {
            if (!TryGetControlBounds(badge.MemberControls[0], out var first) ||
                !TryGetControlBounds(badge.MemberControls[1], out var second))
            {
                badge.Visible = false;
                continue;
            }

            badge.Visible = true;
            var firstCenter = CenterOf(first);
            var secondCenter = CenterOf(second);
            var midpoint = new Point(
                (firstCenter.X + secondCenter.X) / 2,
                (firstCenter.Y + secondCenter.Y) / 2);
            var width = Math.Clamp(frame.Width / 13, 58, 82);
            var height = Math.Clamp(frame.Height / 11, 26, 34);
            var bounds = new Rectangle(midpoint.X - width / 2, midpoint.Y - height / 2, width, height);
            var horizontalPair = Math.Abs(firstCenter.X - secondCenter.X) >=
                Math.Abs(firstCenter.Y - secondCenter.Y) * 2;
            if (horizontalPair && Math.Min(first.Top, second.Top) - height - 4 >= frame.Top)
            {
                bounds.Y = Math.Min(first.Top, second.Top) - height - 4;
            }
            else if (!horizontalPair)
            {
                var right = Math.Max(first.Right, second.Right) + 4;
                if (right + width <= frame.Right)
                {
                    bounds.X = right;
                }
            }
            bounds.X = Math.Clamp(bounds.X, frame.Left + 4, Math.Max(frame.Left + 4, frame.Right - bounds.Width - 4));
            bounds.Y = Math.Clamp(bounds.Y, frame.Top + 4, Math.Max(frame.Top + 4, frame.Bottom - bounds.Height - 4));

            for (var attempt = 0; attempt < 5 && occupied.Any(item => item.IntersectsWith(bounds)); attempt++)
            {
                bounds.Offset(0, height + 3);
                if (bounds.Bottom > frame.Bottom - 4)
                {
                    bounds.Y = Math.Max(frame.Top + 4, midpoint.Y - ((attempt + 2) * (height + 3)));
                }
            }

            badge.Bounds = bounds;
            occupied.Add(bounds);
            badge.BringToFront();
        }
    }

    private void HighlightCombo(Ack05Control[] controls)
    {
        ClearComboHighlight();
        foreach (var control in controls)
        {
            if (_buttons.TryGetValue(control, out var button))
            {
                button.IsComboHighlighted = true;
            }
            else if (control == Ack05Control.RollerPress)
            {
                _roller.IsComboHighlighted = true;
            }
        }

        if (controls.Length == 2 &&
            TryGetControlBounds(controls[0], out var first) &&
            TryGetControlBounds(controls[1], out var second))
        {
            _surface.ComboConnector = (CenterOf(first), CenterOf(second));
        }
    }

    private void ClearComboHighlight()
    {
        foreach (var button in _buttons.Values)
        {
            button.IsComboHighlighted = false;
        }
        _roller.IsComboHighlighted = false;
        _surface.ComboConnector = null;
    }

    private bool TryGetControlBounds(Ack05Control control, out Rectangle bounds)
    {
        if (_buttons.TryGetValue(control, out var button))
        {
            bounds = button.Bounds;
            return true;
        }

        if (control == Ack05Control.RollerPress)
        {
            bounds = _roller.CenterBoundsInParent;
            return true;
        }

        bounds = Rectangle.Empty;
        return false;
    }

    private static Point CenterOf(Rectangle bounds) =>
        new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);

    private static string ScopeLabel(Ack05ComboDefinition definition, int layerCount)
    {
        var layers = definition.Layers.Order().ToArray();
        if (layers.Length == layerCount && layers.SequenceEqual(Enumerable.Range(0, layerCount)))
        {
            return "ALL";
        }

        return "L" + string.Join(",", layers);
    }

    private static void SetBounds(
        Control control,
        Rectangle frame,
        double x,
        double y,
        double width,
        double height) =>
        control.Bounds = new Rectangle(
            frame.Left + (int)Math.Round(frame.Width * x),
            frame.Top + (int)Math.Round(frame.Height * y),
            Math.Max(24, (int)Math.Round(frame.Width * width)),
            Math.Max(24, (int)Math.Round(frame.Height * height)));

    private void UpdateSelection()
    {
        foreach (var pair in _buttons)
        {
            pair.Value.IsSelected = pair.Key == _selectedControl;
        }

        _roller.SelectedControl = _selectedControl;
    }

    private static Ack05Action ActionFor(Ack05Layer layer, Ack05Control control) =>
        layer.Bindings.SingleOrDefault(binding => binding.Control == control)?.Action
            ?? Ack05Action.Disabled();

    private static string CompactAction(Ack05Action action, Ack05Profile? profile = null)
    {
        var value = action.Kind switch
        {
            Ack05ActionKind.Disabled => "—",
            Ack05ActionKind.Transparent => "TRNS",
            Ack05ActionKind.KeyboardChord => string.Join("+", action.Keys ?? []),
            Ack05ActionKind.MouseWheel => $"Wheel {action.WheelSteps:+#;-#;0}",
            Ack05ActionKind.OpenTarget => "OPEN",
            Ack05ActionKind.MomentaryLayer => $"MO({action.TargetLayer})",
            Ack05ActionKind.ToggleLayer => $"TG({action.TargetLayer})",
            Ack05ActionKind.TapToggleHoldLayer => $"T/H({action.TargetLayer})",
            Ack05ActionKind.TapHold => CompactTapHold(action, profile),
            _ => action.Kind.ToString(),
        };
        return value.Length <= 11 ? value : value[..10] + "…";
    }

    private static string CompactTapHold(Ack05Action action, Ack05Profile? profile)
    {
        var definition = profile?.TapHolds.FirstOrDefault(item => string.Equals(
            item.Id,
            action.TapHoldId,
            StringComparison.OrdinalIgnoreCase));
        return definition is null
            ? $"TH({action.TapHoldId})"
            : $"{CompactTapHoldSlot(definition.OnTap)}/{CompactTapHoldSlot(definition.OnHold)}";
    }

    private static string CompactTapHoldSlot(Ack05Action action) => action.Kind switch
    {
        Ack05ActionKind.Disabled => "—",
        Ack05ActionKind.KeyboardChord => string.Join("+", (action.Keys ?? []).Select(CompactKeyboardKey)),
        Ack05ActionKind.MouseWheel => $"Wh{action.WheelSteps:+#;-#;0}",
        Ack05ActionKind.OpenTarget => "Open",
        Ack05ActionKind.MomentaryLayer => $"MO({action.TargetLayer})",
        Ack05ActionKind.ToggleLayer => $"TG({action.TargetLayer})",
        _ => CompactAction(action),
    };

    private static string CompactKeyboardKey(Ack05KeyboardKey key) => key switch
    {
        Ack05KeyboardKey.Backspace => "Bksp",
        Ack05KeyboardKey.Escape => "Esc",
        Ack05KeyboardKey.LeftControl => "LCtrl",
        Ack05KeyboardKey.RightControl => "RCtrl",
        Ack05KeyboardKey.LeftShift => "LShift",
        Ack05KeyboardKey.RightShift => "RShift",
        Ack05KeyboardKey.LeftAlt => "LAlt",
        Ack05KeyboardKey.RightAlt => "RAlt",
        Ack05KeyboardKey.LeftWindows => "LWin",
        Ack05KeyboardKey.RightWindows => "RWin",
        Ack05KeyboardKey.ArrowUp => "Up",
        Ack05KeyboardKey.ArrowDown => "Down",
        Ack05KeyboardKey.ArrowLeft => "Left",
        Ack05KeyboardKey.ArrowRight => "Right",
        _ => key.ToString(),
    };

    private static string FullAction(Ack05Action action, Ack05Profile? profile = null) =>
        action.Kind switch
        {
            Ack05ActionKind.Disabled => "Disabled",
            Ack05ActionKind.Transparent => "Transparent (fall through)",
            Ack05ActionKind.KeyboardChord => string.Join(" + ", action.Keys ?? []),
            Ack05ActionKind.MouseWheel => $"Mouse wheel {action.WheelSteps:+#;-#;0}",
            Ack05ActionKind.OpenTarget => $"Open {action.Target}",
            Ack05ActionKind.MomentaryLayer => $"Momentary Layer {action.TargetLayer}",
            Ack05ActionKind.ToggleLayer => $"Toggle Layer {action.TargetLayer}",
            Ack05ActionKind.TapToggleHoldLayer => $"Tap Toggle / Hold Layer {action.TargetLayer}",
            Ack05ActionKind.TapHold => FullTapHold(action, profile),
            _ => action.Kind.ToString(),
        };

    private static string FullTapHold(Ack05Action action, Ack05Profile? profile)
    {
        var definition = profile?.TapHolds.FirstOrDefault(item => string.Equals(
            item.Id,
            action.TapHoldId,
            StringComparison.OrdinalIgnoreCase));
        return definition is null
            ? $"Tap & Hold {action.TapHoldId}"
            : $"Tap & Hold {definition.Id}: Tap {FullAction(definition.OnTap)} / Hold {FullAction(definition.OnHold)}";
    }

    private sealed class Ack05DeviceSurface : Panel
    {
        private Rectangle _deviceFrame;
        private (Point First, Point Second)? _comboConnector;

        internal Ack05DeviceSurface()
        {
            DoubleBuffered = true;
            BackColor = Canvas;
            ResizeRedraw = true;
        }

        internal Rectangle DeviceFrame
        {
            get => _deviceFrame;
            set
            {
                _deviceFrame = value;
                Invalidate();
            }
        }

        internal (Point First, Point Second)? ComboConnector
        {
            get => _comboConnector;
            set
            {
                _comboConnector = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_deviceFrame.Width <= 0 || _deviceFrame.Height <= 0)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var body = new Rectangle(
                _deviceFrame.Left + _deviceFrame.Width * 7 / 100,
                _deviceFrame.Top + _deviceFrame.Height * 10 / 100,
                _deviceFrame.Width * 91 / 100,
                _deviceFrame.Height * 86 / 100);
            using var path = RoundedRectangle(body, Math.Max(12, _deviceFrame.Height / 18));
            using var fill = new SolidBrush(Surface);
            using var border = new Pen(InstrumentTheme.Ink, 2F);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);

            if (_comboConnector is { } connector)
            {
                using var connectorPen = new Pen(InstrumentTheme.Ink, 2F)
                {
                    DashStyle = DashStyle.Dot,
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                };
                e.Graphics.DrawLine(connectorPen, connector.First, connector.Second);
            }
        }
    }

    private sealed class Ack05MapButton : Button
    {
        private bool _hovered;
        private bool _isSelected;
        private bool _isComboHighlighted;
        private readonly System.Windows.Forms.Timer _motionTimer = new() { Interval = 15 };
        private readonly Stopwatch _motionClock = new();
        private float _motionAmount;
        private float _motionFrom;
        private float _motionTarget;
        private string _keyText = string.Empty;
        private string _actionText = "—";

        internal Ack05MapButton()
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
            BackColor = KeySurface;
            ForeColor = Color.White;
            Margin = Padding.Empty;
            _motionTimer.Tick += (_, _) => TickMotion();
        }

        internal string KeyText
        {
            get => _keyText;
            set
            {
                if (string.Equals(_keyText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _keyText = value;
                Invalidate();
            }
        }

        internal string ActionText
        {
            get => _actionText;
            set
            {
                if (string.Equals(_actionText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _actionText = value;
                Invalidate();
            }
        }

        internal bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                StartMotion();
            }
        }

        internal bool IsComboHighlighted
        {
            get => _isComboHighlighted;
            set
            {
                if (_isComboHighlighted == value)
                {
                    return;
                }

                _isComboHighlighted = value;
                StartMotion();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            StartMotion();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            StartMotion();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Surface);
            var bounds = Rectangle.Inflate(ClientRectangle, -2, -2);
            var radius = Math.Max(8, Math.Min(bounds.Width, bounds.Height) / 7);
            using var path = RoundedRectangle(bounds, radius);
            var targetFill = _isSelected ? Accent : _hovered || _isComboHighlighted ? KeyHover : KeySurface;
            var fillColor = InstrumentTheme.Mix(KeySurface, targetFill, _motionAmount);
            var borderColor = InstrumentTheme.Mix(
                InstrumentTheme.Border,
                _isSelected || _isComboHighlighted ? InstrumentTheme.Ink : InstrumentTheme.Border,
                _motionAmount);
            using var fill = new SolidBrush(fillColor);
            using var border = new Pen(borderColor, _isSelected || _isComboHighlighted ? 2.2F : 1.5F);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);

            // K7 is tall and K9 is wide, so deriving type size from Width alone
            // makes those two keys visually inconsistent with the square keys.
            var typeScale = Math.Min(Width, Height);
            var keySize = Math.Clamp(typeScale / 3.8F, 12F, 22F);
            var actionSize = Math.Clamp(typeScale / 10F, 7F, 9F);
            using var keyFont = new Font(Font.FontFamily, keySize, FontStyle.Regular, GraphicsUnit.Pixel);
            using var actionFont = new Font(Font.FontFamily, actionSize, FontStyle.Bold, GraphicsUnit.Pixel);
            var keyRect = new Rectangle(2, Height / 6, Width - 4, Height / 2);
            var actionRect = new Rectangle(3, Height * 58 / 100, Width - 6, Height * 30 / 100);
            TextRenderer.DrawText(
                e.Graphics,
                _keyText,
                keyFont,
                keyRect,
                _isSelected && _motionAmount > 0.45F ? Color.White : InstrumentTheme.Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(
                e.Graphics,
                _actionText,
                actionFont,
                actionRect,
                _isSelected && _motionAmount > 0.45F ? Color.FromArgb(225, 225, 222) : InstrumentTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)
            {
                var focusBounds = Rectangle.Inflate(bounds, -5, -5);
                ControlPaint.DrawFocusRectangle(
                    e.Graphics,
                    focusBounds,
                    _isSelected ? Color.White : InstrumentTheme.Ink,
                    _isSelected ? InstrumentTheme.Ink : KeySurface);
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

        private void StartMotion()
        {
            _motionFrom = _motionAmount;
            _motionTarget = _isSelected || _hovered || _isComboHighlighted ? 1F : 0F;
            _motionClock.Restart();
            _motionTimer.Start();
        }

        private void TickMotion()
        {
            const float durationMs = 165F;
            var progress = Math.Clamp((float)_motionClock.Elapsed.TotalMilliseconds / durationMs, 0F, 1F);
            var eased = InstrumentTheme.EaseOutCubic(progress);
            _motionAmount = _motionFrom + ((_motionTarget - _motionFrom) * eased);
            Invalidate();
            if (progress < 1F)
            {
                return;
            }

            _motionAmount = _motionTarget;
            _motionTimer.Stop();
        }
    }

    private sealed class ComboBadge : Button
    {
        private bool _hovered;

        internal ComboBadge()
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
            BackColor = Color.Transparent;
        }

        internal string ComboId { get; init; } = string.Empty;
        internal Ack05Control[] MemberControls { get; init; } = [];
        internal string OutputText { get; init; } = string.Empty;
        internal string ScopeText { get; init; } = string.Empty;

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Surface);
            var bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
            using var path = RoundedRectangle(bounds, Math.Max(7, bounds.Height / 3));
            using var fill = new SolidBrush(_hovered || Focused ? KeyHover : KeySurface);
            using var border = new Pen(InstrumentTheme.Ink, _hovered || Focused ? 2F : 1.5F);
            e.Graphics.FillPath(fill, path);
            e.Graphics.DrawPath(border, path);

            var foreground = InstrumentTheme.Ink;
            using var outputFont = new Font(Font.FontFamily, Math.Clamp(Height / 3.2F, 8F, 10F), FontStyle.Bold, GraphicsUnit.Pixel);
            using var scopeFont = new Font(Font.FontFamily, Math.Clamp(Height / 4.5F, 6F, 8F), FontStyle.Regular, GraphicsUnit.Pixel);
            TextRenderer.DrawText(
                e.Graphics,
                OutputText,
                outputFont,
                new Rectangle(3, 2, Width - 6, Height * 3 / 5),
                foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(
                e.Graphics,
                ScopeText,
                scopeFont,
                new Rectangle(3, Height / 2, Width - 6, Height / 2 - 2),
                foreground,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private sealed class Ack05RollerControl : Control
    {
        private readonly ToolTip _toolTip = new();
        private Ack05Action _counterClockwise = Ack05Action.Disabled();
        private Ack05Action _clockwise = Ack05Action.Disabled();
        private Ack05Action _press = Ack05Action.Disabled();
        private Ack05Control? _selectedControl;
        private bool _isComboHighlighted;

        internal Ack05RollerControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.UserPaint,
                true);
            SetStyle(ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
            AccessibleName = "Roller directions and center switch";
            AccessibleDescription = "Use Left, Right, or Down Arrow to select counter-clockwise, clockwise, or center press.";
            _toolTip.SetToolTip(this, "Turn left/right or press the center to select its binding.");
        }

        internal event Action<Ack05Control>? ControlSelected;

        internal Ack05Control? SelectedControl
        {
            get => _selectedControl;
            set
            {
                if (_selectedControl == value)
                {
                    return;
                }

                _selectedControl = value;
                Invalidate();
            }
        }

        internal bool IsComboHighlighted
        {
            get => _isComboHighlighted;
            set
            {
                if (_isComboHighlighted == value)
                {
                    return;
                }

                _isComboHighlighted = value;
                Invalidate();
            }
        }

        internal Rectangle CenterBoundsInParent
        {
            get
            {
                var bounds = CenterBounds();
                bounds.Offset(Left, Top);
                return bounds;
            }
        }

        internal void LoadActions(
            Ack05Action counterClockwise,
            Ack05Action clockwise,
            Ack05Action press)
        {
            _counterClockwise = counterClockwise;
            _clockwise = clockwise;
            _press = press;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            Focus();
            var circle = DialBounds();
            var center = new Point(circle.Left + circle.Width / 2, circle.Top + circle.Height / 2);
            var centerRadius = circle.Width * 0.17;
            var dx = e.X - center.X;
            var dy = e.Y - center.Y;
            if ((dx * dx) + (dy * dy) <= centerRadius * centerRadius)
            {
                SelectedControl = Ack05Control.RollerPress;
                ControlSelected?.Invoke(Ack05Control.RollerPress);
                base.OnMouseClick(e);
                return;
            }

            var control = e.X < center.X
                ? Ack05Control.RollerCounterClockwise
                : Ack05Control.RollerClockwise;
            SelectedControl = control;
            ControlSelected?.Invoke(control);
            base.OnMouseClick(e);
        }

        protected override bool IsInputKey(Keys keyData) =>
            keyData is Keys.Left or Keys.Right or Keys.Down || base.IsInputKey(keyData);

        protected override void OnKeyDown(KeyEventArgs e)
        {
            var control = e.KeyCode switch
            {
                Keys.Left => Ack05Control.RollerCounterClockwise,
                Keys.Right => Ack05Control.RollerClockwise,
                Keys.Down => Ack05Control.RollerPress,
                Keys.Enter or Keys.Space => _selectedControl ?? Ack05Control.RollerClockwise,
                _ => (Ack05Control?)null,
            };
            if (control is not null)
            {
                SelectedControl = control;
                ControlSelected?.Invoke(control.Value);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }

            base.OnKeyDown(e);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var circle = DialBounds();
            using var dialFill = new SolidBrush(InstrumentTheme.Raised);
            using var dialBorder = new Pen(InstrumentTheme.Ink, 2.4F);
            e.Graphics.FillEllipse(dialFill, circle);
            e.Graphics.DrawEllipse(dialBorder, circle);

            var inner = Rectangle.Inflate(circle, -(int)(circle.Width * 0.32), -(int)(circle.Height * 0.32));
            using var innerFill = new SolidBrush(InstrumentTheme.Surface);
            using var innerBorder = new Pen(InstrumentTheme.Ink, 2F);
            e.Graphics.FillEllipse(innerFill, inner);
            e.Graphics.DrawEllipse(innerBorder, inner);

            if (_selectedControl == Ack05Control.RollerPress || _isComboHighlighted)
            {
                using var centerSelection = new Pen(Accent, _isComboHighlighted ? 4F : 3F);
                e.Graphics.DrawEllipse(centerSelection, Rectangle.Inflate(inner, -2, -2));
            }

            using var pressFont = new Font(
                FontFamily.GenericSansSerif,
                Math.Clamp(inner.Height / 8F, 7F, 9F),
                FontStyle.Bold,
                GraphicsUnit.Pixel);
            var pressText = CompactAction(_press);
            TextRenderer.DrawText(
                e.Graphics,
                _press.Kind == Ack05ActionKind.Disabled ? "PRESS" : pressText,
                pressFont,
                inner,
                InstrumentTheme.Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            DrawDirection(
                e.Graphics,
                new Rectangle(circle.Left + circle.Width / 12, circle.Bottom - circle.Height / 4, circle.Width * 5 / 12, circle.Height / 6),
                "CCW",
                CompactAction(_counterClockwise),
                _selectedControl == Ack05Control.RollerCounterClockwise);
            DrawDirection(
                e.Graphics,
                new Rectangle(circle.Left + circle.Width / 2, circle.Bottom - circle.Height / 4, circle.Width * 5 / 12, circle.Height / 6),
                "CW",
                CompactAction(_clockwise),
                _selectedControl == Ack05Control.RollerClockwise);

            if (Focused && ShowFocusCues)
            {
                var focusBounds = Rectangle.Inflate(circle, -5, -5);
                using var focus = new Pen(InstrumentTheme.Ink, 1F)
                {
                    DashStyle = DashStyle.Dot,
                };
                e.Graphics.DrawEllipse(focus, focusBounds);
            }

        }

        private Rectangle DialBounds()
        {
            var size = Math.Max(1, Math.Min(Width - 2, Height - 2));
            return new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
        }

        private Rectangle CenterBounds()
        {
            var circle = DialBounds();
            return Rectangle.Inflate(
                circle,
                -(int)(circle.Width * 0.32),
                -(int)(circle.Height * 0.32));
        }

        private static void DrawDirection(
            Graphics graphics,
            Rectangle bounds,
            string direction,
            string action,
            bool selected)
        {
            if (selected)
            {
                using var path = RoundedRectangle(bounds, Math.Max(5, bounds.Height / 2));
                using var selectedFill = new SolidBrush(Accent);
                graphics.FillPath(selectedFill, path);
            }

            using var font = new Font(FontFamily.GenericSansSerif, Math.Clamp(bounds.Height / 3F, 7F, 10F), FontStyle.Bold, GraphicsUnit.Pixel);
            var label = action == "—" ? direction : $"{direction} {action}";
            TextRenderer.DrawText(
                graphics,
                label,
                font,
                bounds,
                selected ? Color.White : InstrumentTheme.Ink,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
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
}
