using Ack05Enhanced.Core;
using System.Drawing.Drawing2D;

namespace Ack05Enhanced.App;

/// <summary>
/// Searchable action library with compact categories and drag-ready cards.
/// Keyboard cards can be combined; exclusive actions replace the current draft.
/// </summary>
internal sealed class Ack05ActionPalette : UserControl
{
    internal enum Usage
    {
        Binding,
        Tap,
        Hold,
        Combo,
    }

    private static readonly string[] CategoryOrder =
    [
        "Common",
        "Layers",
        "Navigation",
        "Normal Keys",
        "Modifiers",
        "Function Keys",
        "Numpad",
        "Media",
        "Other Keys",
    ];

    private readonly TextBox _search = new();
    private readonly ListBox _categories = new();
    private readonly BufferedFlowLayoutPanel _cards = new();
    private readonly Label _dragHint = new();
    private readonly Dictionary<string, List<PaletteEntry>> _entries = new(StringComparer.Ordinal);
    private readonly HashSet<Ack05KeyboardKey> _selectedKeys = new();
    private readonly List<PaletteCard> _visibleCards = new();
    private Ack05ActionKind? _exclusiveKind;
    private bool _pulseOnly;
    private Usage _usage;
    private bool _loading;

    internal Ack05ActionPalette()
    {
        Dock = DockStyle.Fill;
        BackColor = InstrumentTheme.Surface;
        Padding = new Padding(10);

        var layout = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = InstrumentTheme.Surface,
            Margin = Padding.Empty,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 218));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(layout);

        var categoriesPanel = new BufferedTableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            BackColor = InstrumentTheme.Surface,
            Padding = new Padding(0, 0, 12, 0),
        };
        categoriesPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        categoriesPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _search.Dock = DockStyle.Fill;
        _search.PlaceholderText = "Search actions...";
        _search.BorderStyle = BorderStyle.FixedSingle;
        _search.BackColor = InstrumentTheme.Raised;
        _search.ForeColor = InstrumentTheme.Ink;
        _search.TextChanged += (_, _) => RebuildCards();
        categoriesPanel.Controls.Add(_search, 0, 0);

        _categories.Dock = DockStyle.Fill;
        _categories.BorderStyle = BorderStyle.None;
        _categories.BackColor = InstrumentTheme.Surface;
        _categories.ForeColor = InstrumentTheme.Ink;
        _categories.IntegralHeight = false;
        _categories.ItemHeight = 20;
        _categories.SelectedIndexChanged += (_, _) =>
        {
            if (!_loading && _search.TextLength == 0)
            {
                RebuildCards();
            }
        };
        categoriesPanel.Controls.Add(_categories, 0, 1);
        layout.Controls.Add(categoriesPanel, 0, 0);

        _cards.Dock = DockStyle.Fill;
        _cards.FlowDirection = FlowDirection.LeftToRight;
        _cards.WrapContents = true;
        _cards.AutoScroll = true;
        _cards.BackColor = InstrumentTheme.Surface;
        _cards.Padding = new Padding(12, 2, 4, 2);
        layout.Controls.Add(_cards, 1, 0);

        _dragHint.Text = "Select once to assign • drag the selected action onto another key";
        _dragHint.Dock = DockStyle.Fill;
        _dragHint.ForeColor = InstrumentTheme.Muted;
        _dragHint.TextAlign = ContentAlignment.MiddleCenter;
        layout.Controls.Add(_dragHint, 1, 1);

        BuildEntries();
        _loading = true;
        _categories.Items.AddRange(CategoryOrder);
        _categories.SelectedIndex = 0;
        _loading = false;
        RebuildCards();
    }

    internal event EventHandler? SelectionChanged;

    internal Ack05ActionKind? SelectedKind => _exclusiveKind ??
        (_selectedKeys.Count > 0 ? Ack05ActionKind.KeyboardChord : null);

    internal IReadOnlyList<Ack05KeyboardKey> SelectedKeys =>
        Enum.GetValues<Ack05KeyboardKey>()
            .Where(_selectedKeys.Contains)
            .ToArray();

    internal bool HasExclusiveSelection => _exclusiveKind is not null;

    internal void LoadAction(Ack05Action action)
    {
        _selectedKeys.Clear();
        _exclusiveKind = null;
        if (action.Kind == Ack05ActionKind.KeyboardChord)
        {
            foreach (var key in action.Keys ?? [])
            {
                _selectedKeys.Add(key);
            }
        }
        else
        {
            _exclusiveKind = action.Kind;
        }

        var categoryChanged = SelectBestCategory(action);
        if (categoryChanged && _search.TextLength == 0)
        {
            RebuildCards();
        }
        else
        {
            RefreshVisibleCardStates();
        }
    }

    internal void SetKeyboardKeys(IEnumerable<Ack05KeyboardKey> keys)
    {
        _exclusiveKind = null;
        _selectedKeys.Clear();
        foreach (var key in keys)
        {
            _selectedKeys.Add(key);
        }

        var categoryChanged = SelectCategory(CategoryName(_selectedKeys.FirstOrDefault()));
        if (categoryChanged && _search.TextLength == 0)
        {
            RebuildCards();
        }
        else
        {
            RefreshVisibleCardStates();
        }
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void SetPulseOnly(bool pulseOnly)
    {
        if (_pulseOnly == pulseOnly)
        {
            return;
        }

        _pulseOnly = pulseOnly;
        RefreshVisibleCardStates();
    }

    internal void SetUsage(Usage usage)
    {
        if (_usage == usage)
        {
            return;
        }

        _usage = usage;
        UpdateDragHint();
        RefreshVisibleCardStates();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        UpdateDragHint();
        RefreshVisibleCardStates();
    }

    private void UpdateDragHint()
    {
        _dragHint.Text = !Enabled
            ? _usage switch
            {
                Usage.Tap or Usage.Hold => "Create or select a Tap & Hold definition to choose an action",
                Usage.Combo => "Create or select a Combo to choose its output",
                _ => "Select a key to choose its action",
            }
            : _usage switch
        {
            Usage.Tap => "Choose the action emitted by a short tap",
            Usage.Hold => "Choose the action held until the physical key is released",
            Usage.Combo => "Choose the output emitted by the selected two-key Combo",
            _ => "Select once to assign • drag the selected action onto another key",
        };
    }

    private void BuildEntries()
    {
        foreach (var category in CategoryOrder)
        {
            _entries[category] = [];
        }

        AddCommonExclusive(Ack05ActionKind.Disabled, "Disabled", "No output");
        AddCommonExclusive(Ack05ActionKind.Transparent, "Transparent", "Fall through");
        foreach (var key in new[]
                 {
                     Ack05KeyboardKey.Escape,
                     Ack05KeyboardKey.Enter,
                     Ack05KeyboardKey.Space,
                     Ack05KeyboardKey.Backspace,
                     Ack05KeyboardKey.Tab,
                 })
        {
            _entries["Common"].Add(PaletteEntry.Keyboard(key, DisplayName(key), "Keyboard"));
        }

        AddCommonExclusive(Ack05ActionKind.MomentaryLayer, "MO", "Hold layer");
        AddCommonExclusive(Ack05ActionKind.ToggleLayer, "Toggle", "Latch layer");
        AddCommonExclusive(Ack05ActionKind.MouseWheel, "Mouse Wheel", "Pulse");

        foreach (var key in Enum.GetValues<Ack05KeyboardKey>())
        {
            var category = CategoryName(key);
            _entries[category].Add(PaletteEntry.Keyboard(key, DisplayName(key), "Keyboard"));
        }

        _entries["Layers"].Add(PaletteEntry.Exclusive(
            Ack05ActionKind.MomentaryLayer,
            "Momentary (MO)",
            "Active while held"));
        _entries["Layers"].Add(PaletteEntry.Exclusive(
            Ack05ActionKind.ToggleLayer,
            "Toggle (TG)",
            "Press to latch"));
        _entries["Layers"].Add(PaletteEntry.Exclusive(
            Ack05ActionKind.TapToggleHoldLayer,
            "Tap / Hold",
            "Tap toggles, hold shifts"));
        _entries["Layers"].Add(PaletteEntry.Exclusive(
            Ack05ActionKind.Transparent,
            "Transparent",
            "Use lower layer"));
    }

    private void AddCommonExclusive(Ack05ActionKind kind, string title, string subtitle) =>
        _entries["Common"].Add(PaletteEntry.Exclusive(kind, title, subtitle));

    private void RebuildCards()
    {
        _cards.SuspendLayout();
        try
        {
            foreach (var card in _visibleCards)
            {
                card.Dispose();
            }

            _visibleCards.Clear();
            _cards.Controls.Clear();
            var query = _search.Text.Trim();
            var entries = query.Length > 0
                ? _entries.Values
                    .SelectMany(value => value)
                    .DistinctBy(entry => (entry.Key, entry.Kind))
                    .Where(entry => entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                    entry.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase))
                : _entries.TryGetValue(_categories.SelectedItem as string ?? "Common", out var categoryEntries)
                    ? categoryEntries
                    : _entries["Common"];

            foreach (var entry in entries)
            {
                var card = new PaletteCard(entry)
                {
                    Selected = IsSelected(entry),
                    Blocked = !Enabled || IsBlocked(entry),
                };
                card.Click += (_, _) => SelectEntry(entry);
                card.DragStarted += (_, _) => StartSelectedDrag(card, entry);
                _visibleCards.Add(card);
                _cards.Controls.Add(card);
            }
        }
        finally
        {
            _cards.ResumeLayout();
        }
    }

    private void SelectEntry(PaletteEntry entry)
    {
        if (IsBlocked(entry) && !IsSelected(entry))
        {
            return;
        }

        if (entry.Key is { } key)
        {
            _exclusiveKind = null;
            if (!_selectedKeys.Remove(key))
            {
                _selectedKeys.Add(key);
            }
        }
        else if (entry.Kind is { } kind && _exclusiveKind != kind)
        {
            _selectedKeys.Clear();
            _exclusiveKind = kind;
        }

        RefreshVisibleCardStates();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void StartSelectedDrag(Control source, PaletteEntry entry)
    {
        if (!IsSelected(entry) || IsBlocked(entry))
        {
            return;
        }

        var data = new DataObject();
        data.SetData(Ack05DeviceMapControl.PaletteDragFormat, true);
        source.DoDragDrop(data, DragDropEffects.Copy);
    }

    private void RefreshVisibleCardStates()
    {
        foreach (var card in _visibleCards)
        {
            card.Selected = IsSelected(card.Entry);
            card.Blocked = !Enabled || IsBlocked(card.Entry);
        }
    }

    private bool IsSelected(PaletteEntry entry) =>
        entry.Key is { } key ? _selectedKeys.Contains(key) : _exclusiveKind == entry.Kind;

    private bool IsBlocked(PaletteEntry entry)
    {
        if (_pulseOnly && entry.Kind is Ack05ActionKind.MomentaryLayer or
            Ack05ActionKind.TapToggleHoldLayer)
        {
            return true;
        }

        return _usage switch
        {
            Usage.Tap => entry.Kind is Ack05ActionKind.Transparent or
                Ack05ActionKind.MomentaryLayer or Ack05ActionKind.TapToggleHoldLayer,
            Usage.Hold => entry.Kind is Ack05ActionKind.Transparent or
                Ack05ActionKind.MouseWheel or Ack05ActionKind.ToggleLayer or
                Ack05ActionKind.TapToggleHoldLayer,
            Usage.Combo => entry.Kind is Ack05ActionKind.Transparent or
                Ack05ActionKind.TapToggleHoldLayer,
            _ => false,
        };
    }

    private bool SelectBestCategory(Ack05Action action)
    {
        if (action.Kind is Ack05ActionKind.MomentaryLayer or
            Ack05ActionKind.ToggleLayer or Ack05ActionKind.TapToggleHoldLayer)
        {
            return SelectCategory("Layers");
        }

        if (action.Kind == Ack05ActionKind.KeyboardChord && action.Keys?.FirstOrDefault() is { } key)
        {
            return SelectCategory(CategoryName(key));
        }

        return SelectCategory("Common");
    }

    private bool SelectCategory(string category)
    {
        var index = Array.IndexOf(CategoryOrder, category);
        var nextIndex = Math.Max(index, 0);
        if (_categories.SelectedIndex == nextIndex)
        {
            return false;
        }

        _loading = true;
        _categories.SelectedIndex = nextIndex;
        _categories.TopIndex = 0;
        _loading = false;
        return true;
    }

    private static string CategoryName(Ack05KeyboardKey key)
    {
        if (key is Ack05KeyboardKey.LeftControl or Ack05KeyboardKey.RightControl or
            Ack05KeyboardKey.LeftShift or Ack05KeyboardKey.RightShift or
            Ack05KeyboardKey.LeftAlt or Ack05KeyboardKey.RightAlt or
            Ack05KeyboardKey.LeftWindows or Ack05KeyboardKey.RightWindows)
        {
            return "Modifiers";
        }

        if (IsRange(key, Ack05KeyboardKey.F1, Ack05KeyboardKey.F24))
        {
            return "Function Keys";
        }

        if (IsRange(key, Ack05KeyboardKey.Numpad0, Ack05KeyboardKey.NumpadEnter) ||
            key == Ack05KeyboardKey.NumLock)
        {
            return "Numpad";
        }

        if (IsRange(key, Ack05KeyboardKey.VolumeMute, Ack05KeyboardKey.MediaPlayPause))
        {
            return "Media";
        }

        if (key is Ack05KeyboardKey.Insert or Ack05KeyboardKey.Delete or
            Ack05KeyboardKey.Home or Ack05KeyboardKey.End or
            Ack05KeyboardKey.PageUp or Ack05KeyboardKey.PageDown or
            Ack05KeyboardKey.ArrowUp or Ack05KeyboardKey.ArrowDown or
            Ack05KeyboardKey.ArrowLeft or Ack05KeyboardKey.ArrowRight)
        {
            return "Navigation";
        }

        if (key is Ack05KeyboardKey.CapsLock or Ack05KeyboardKey.PrintScreen or
            Ack05KeyboardKey.ScrollLock or Ack05KeyboardKey.Pause or Ack05KeyboardKey.Menu)
        {
            return "Other Keys";
        }

        return "Normal Keys";
    }

    private static bool IsRange(Ack05KeyboardKey key, Ack05KeyboardKey first, Ack05KeyboardKey last) =>
        (int)key >= (int)first && (int)key <= (int)last;

    private static string DisplayName(Ack05KeyboardKey key) => key switch
    {
        >= Ack05KeyboardKey.Digit0 and <= Ack05KeyboardKey.Digit9 =>
            ((int)key - (int)Ack05KeyboardKey.Digit0).ToString(),
        Ack05KeyboardKey.LeftControl => "Left Ctrl",
        Ack05KeyboardKey.RightControl => "Right Ctrl",
        Ack05KeyboardKey.LeftShift => "Left Shift",
        Ack05KeyboardKey.RightShift => "Right Shift",
        Ack05KeyboardKey.LeftAlt => "Left Alt",
        Ack05KeyboardKey.RightAlt => "Right Alt",
        Ack05KeyboardKey.LeftWindows => "Left Windows",
        Ack05KeyboardKey.RightWindows => "Right Windows",
        Ack05KeyboardKey.ArrowUp => "Up Arrow",
        Ack05KeyboardKey.ArrowDown => "Down Arrow",
        Ack05KeyboardKey.ArrowLeft => "Left Arrow",
        Ack05KeyboardKey.ArrowRight => "Right Arrow",
        Ack05KeyboardKey.PageUp => "Page Up",
        Ack05KeyboardKey.PageDown => "Page Down",
        Ack05KeyboardKey.PrintScreen => "Print Screen",
        Ack05KeyboardKey.ScrollLock => "Scroll Lock",
        Ack05KeyboardKey.NumLock => "Num Lock",
        Ack05KeyboardKey.NumpadAdd => "Numpad +",
        Ack05KeyboardKey.NumpadSubtract => "Numpad -",
        Ack05KeyboardKey.NumpadMultiply => "Numpad *",
        Ack05KeyboardKey.NumpadDivide => "Numpad /",
        Ack05KeyboardKey.NumpadDecimal => "Numpad .",
        Ack05KeyboardKey.NumpadEnter => "Numpad Enter",
        Ack05KeyboardKey.VolumeMute => "Mute",
        Ack05KeyboardKey.VolumeDown => "Volume Down",
        Ack05KeyboardKey.VolumeUp => "Volume Up",
        Ack05KeyboardKey.MediaNext => "Next Track",
        Ack05KeyboardKey.MediaPrevious => "Previous Track",
        Ack05KeyboardKey.MediaStop => "Stop Media",
        Ack05KeyboardKey.MediaPlayPause => "Play / Pause",
        _ => key.ToString(),
    };

    private sealed record PaletteEntry(
        Ack05KeyboardKey? Key,
        Ack05ActionKind? Kind,
        string Title,
        string Subtitle)
    {
        internal static PaletteEntry Keyboard(
            Ack05KeyboardKey key,
            string title,
            string subtitle) => new(key, null, title, subtitle);

        internal static PaletteEntry Exclusive(
            Ack05ActionKind kind,
            string title,
            string subtitle) => new(null, kind, title, subtitle);
    }

    private sealed class PaletteCard : Button
    {
        private bool _selected;
        private bool _blocked;
        private Point _mouseDown;

        internal PaletteCard(PaletteEntry entry)
        {
            Entry = entry;
            Text = entry.Title;
            AccessibleName = entry.Title;
            AccessibleDescription = entry.Subtitle;
            Width = 136;
            Height = 82;
            Margin = new Padding(6, 4, 6, 4);
            Cursor = Cursors.Hand;
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
        }

        internal event EventHandler? DragStarted;
        internal PaletteEntry Entry { get; }

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

        internal bool Blocked
        {
            get => _blocked;
            set
            {
                if (_blocked == value)
                {
                    return;
                }

                _blocked = value;
                Cursor = value ? Cursors.No : Cursors.Hand;
                TabStop = !value;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs eventArgs)
        {
            _mouseDown = eventArgs.Location;
            base.OnMouseDown(eventArgs);
        }

        protected override void OnMouseMove(MouseEventArgs eventArgs)
        {
            if (eventArgs.Button == MouseButtons.Left &&
                Math.Abs(eventArgs.X - _mouseDown.X) + Math.Abs(eventArgs.Y - _mouseDown.Y) >= 8)
            {
                DragStarted?.Invoke(this, EventArgs.Empty);
            }

            base.OnMouseMove(eventArgs);
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            eventArgs.Graphics.Clear(InstrumentTheme.Surface);
            var bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
            using var path = InstrumentTheme.RoundedRectangle(bounds, 11);
            using var fill = new SolidBrush(_selected ? InstrumentTheme.Ink : InstrumentTheme.Raised);
            using var border = new Pen(_selected ? InstrumentTheme.Ink : InstrumentTheme.Border, _selected ? 2F : 1F);
            eventArgs.Graphics.FillPath(fill, path);
            eventArgs.Graphics.DrawPath(border, path);

            var titleColor = _blocked
                ? InstrumentTheme.Disabled
                : _selected ? Color.White : InstrumentTheme.Ink;
            var subtitleColor = _blocked
                ? InstrumentTheme.Disabled
                : _selected ? Color.FromArgb(220, 220, 217) : InstrumentTheme.Muted;
            using var titleFont = new Font(Font.FontFamily, 10F, FontStyle.Regular);
            using var subtitleFont = new Font(Font.FontFamily, 8.2F, FontStyle.Regular);
            TextRenderer.DrawText(
                eventArgs.Graphics,
                Entry.Title,
                titleFont,
                new Rectangle(8, 16, Width - 16, 26),
                titleColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(
                eventArgs.Graphics,
                Entry.Subtitle,
                subtitleFont,
                new Rectangle(8, 43, Width - 16, 22),
                subtitleColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (Focused && ShowFocusCues)
            {
                var focusBounds = Rectangle.Inflate(bounds, -4, -4);
                ControlPaint.DrawFocusRectangle(
                    eventArgs.Graphics,
                    focusBounds,
                    _selected ? Color.White : InstrumentTheme.Ink,
                    _selected ? InstrumentTheme.Ink : InstrumentTheme.Raised);
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
    }
}
