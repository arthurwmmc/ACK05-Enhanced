using Ack05Enhanced.Core;

namespace Ack05Enhanced.App;

internal sealed class KeyCaptureDialog : Form
{
    private readonly Label _preview = new();
    private readonly Button _use = new();

    internal KeyCaptureDialog()
    {
        Text = "Capture Keyboard Shortcut";
        Width = 460;
        Height = 220;
        MinimumSize = new Size(420, 200);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            RowCount = 4,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        root.Controls.Add(new Label
        {
            Text = "Press the shortcut now. Modifiers and the main key are captured together.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);

        _preview.Text = "Waiting for input...";
        _preview.Dock = DockStyle.Fill;
        _preview.TextAlign = ContentAlignment.MiddleCenter;
        _preview.Font = new Font(Font.FontFamily, 16F, FontStyle.Bold);
        _preview.BorderStyle = BorderStyle.FixedSingle;
        root.Controls.Add(_preview, 0, 1);

        root.Controls.Add(new Label
        {
            Text = "Unsupported keys are ignored; the existing checklist remains available.",
            Dock = DockStyle.Fill,
            ForeColor = SystemColors.GrayText,
        }, 0, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
        };
        var cancel = new Button { Text = "Cancel", Width = 90 };
        cancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        _use.Text = "Use Shortcut";
        _use.Width = 110;
        _use.Enabled = false;
        _use.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };
        var clear = new Button { Text = "Clear", Width = 90 };
        clear.Click += (_, _) => SetCapturedKeys([]);
        actions.Controls.Add(cancel);
        actions.Controls.Add(_use);
        actions.Controls.Add(clear);
        root.Controls.Add(actions, 0, 3);
        Controls.Add(root);
    }

    internal Ack05KeyboardKey[] CapturedKeys { get; private set; } = [];

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        e.Handled = true;
        e.SuppressKeyPress = true;

        var keys = new List<Ack05KeyboardKey>();
        AddModifiers(e, keys);
        if (KeyCaptureMapper.TryMap(e.KeyCode, out var mainKey) &&
            !keys.Contains(mainKey))
        {
            keys.Add(mainKey);
        }

        if (keys.Count > 0)
        {
            SetCapturedKeys(keys);
        }
    }

    private void SetCapturedKeys(IEnumerable<Ack05KeyboardKey> keys)
    {
        CapturedKeys = keys.Distinct().ToArray();
        _preview.Text = CapturedKeys.Length == 0
            ? "Waiting for input..."
            : string.Join(" + ", CapturedKeys);
        _use.Enabled = CapturedKeys.Length > 0;
    }

    private static void AddModifiers(KeyEventArgs e, List<Ack05KeyboardKey> keys)
    {
        if (e.Control && e.KeyCode is not
            (Keys.ControlKey or Keys.LControlKey or Keys.RControlKey))
        {
            keys.Add(Ack05KeyboardKey.LeftControl);
        }

        if (e.Shift && e.KeyCode is not
            (Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey))
        {
            keys.Add(Ack05KeyboardKey.LeftShift);
        }

        if (e.Alt && e.KeyCode is not (Keys.Menu or Keys.LMenu or Keys.RMenu))
        {
            keys.Add(Ack05KeyboardKey.LeftAlt);
        }

        if (e.KeyCode == Keys.LWin)
        {
            keys.Add(Ack05KeyboardKey.LeftWindows);
        }
        else if (e.KeyCode == Keys.RWin)
        {
            keys.Add(Ack05KeyboardKey.RightWindows);
        }
    }
}

internal static class KeyCaptureMapper
{
    internal static bool TryMap(Keys key, out Ack05KeyboardKey mapped)
    {
        if (key is >= Keys.A and <= Keys.Z)
        {
            mapped = Ack05KeyboardKey.A + (key - Keys.A);
            return true;
        }

        if (key is >= Keys.D0 and <= Keys.D9)
        {
            mapped = Ack05KeyboardKey.Digit0 + (key - Keys.D0);
            return true;
        }

        if (key is >= Keys.F1 and <= Keys.F24)
        {
            mapped = Ack05KeyboardKey.F1 + (key - Keys.F1);
            return true;
        }

        if (key is >= Keys.NumPad0 and <= Keys.NumPad9)
        {
            mapped = Ack05KeyboardKey.Numpad0 + (key - Keys.NumPad0);
            return true;
        }

        mapped = key switch
        {
            Keys.Escape => Ack05KeyboardKey.Escape,
            Keys.Tab => Ack05KeyboardKey.Tab,
            Keys.CapsLock => Ack05KeyboardKey.CapsLock,
            Keys.Back => Ack05KeyboardKey.Backspace,
            Keys.Enter => Ack05KeyboardKey.Enter,
            Keys.Space => Ack05KeyboardKey.Space,
            Keys.Insert => Ack05KeyboardKey.Insert,
            Keys.Delete => Ack05KeyboardKey.Delete,
            Keys.Home => Ack05KeyboardKey.Home,
            Keys.End => Ack05KeyboardKey.End,
            Keys.PageUp => Ack05KeyboardKey.PageUp,
            Keys.PageDown => Ack05KeyboardKey.PageDown,
            Keys.Up => Ack05KeyboardKey.ArrowUp,
            Keys.Down => Ack05KeyboardKey.ArrowDown,
            Keys.Left => Ack05KeyboardKey.ArrowLeft,
            Keys.Right => Ack05KeyboardKey.ArrowRight,
            Keys.PrintScreen => Ack05KeyboardKey.PrintScreen,
            Keys.Scroll => Ack05KeyboardKey.ScrollLock,
            Keys.Pause => Ack05KeyboardKey.Pause,
            Keys.ControlKey or Keys.LControlKey => Ack05KeyboardKey.LeftControl,
            Keys.RControlKey => Ack05KeyboardKey.RightControl,
            Keys.ShiftKey or Keys.LShiftKey => Ack05KeyboardKey.LeftShift,
            Keys.RShiftKey => Ack05KeyboardKey.RightShift,
            Keys.Menu or Keys.LMenu => Ack05KeyboardKey.LeftAlt,
            Keys.RMenu => Ack05KeyboardKey.RightAlt,
            Keys.LWin => Ack05KeyboardKey.LeftWindows,
            Keys.RWin => Ack05KeyboardKey.RightWindows,
            Keys.Apps => Ack05KeyboardKey.Menu,
            Keys.NumLock => Ack05KeyboardKey.NumLock,
            Keys.Add => Ack05KeyboardKey.NumpadAdd,
            Keys.Subtract => Ack05KeyboardKey.NumpadSubtract,
            Keys.Multiply => Ack05KeyboardKey.NumpadMultiply,
            Keys.Divide => Ack05KeyboardKey.NumpadDivide,
            Keys.Decimal => Ack05KeyboardKey.NumpadDecimal,
            Keys.OemSemicolon => Ack05KeyboardKey.Semicolon,
            Keys.Oemplus => Ack05KeyboardKey.Equal,
            Keys.Oemcomma => Ack05KeyboardKey.Comma,
            Keys.OemMinus => Ack05KeyboardKey.Minus,
            Keys.OemPeriod => Ack05KeyboardKey.Period,
            Keys.OemQuestion => Ack05KeyboardKey.Slash,
            Keys.Oemtilde => Ack05KeyboardKey.Backquote,
            Keys.OemOpenBrackets => Ack05KeyboardKey.LeftBracket,
            Keys.OemPipe => Ack05KeyboardKey.Backslash,
            Keys.OemCloseBrackets => Ack05KeyboardKey.RightBracket,
            Keys.OemQuotes => Ack05KeyboardKey.Quote,
            Keys.VolumeMute => Ack05KeyboardKey.VolumeMute,
            Keys.VolumeDown => Ack05KeyboardKey.VolumeDown,
            Keys.VolumeUp => Ack05KeyboardKey.VolumeUp,
            Keys.MediaNextTrack => Ack05KeyboardKey.MediaNext,
            Keys.MediaPreviousTrack => Ack05KeyboardKey.MediaPrevious,
            Keys.MediaStop => Ack05KeyboardKey.MediaStop,
            Keys.MediaPlayPause => Ack05KeyboardKey.MediaPlayPause,
            _ => default,
        };

        return key is Keys.Escape or Keys.Tab or Keys.CapsLock or Keys.Back or
            Keys.Enter or Keys.Space or Keys.Insert or Keys.Delete or Keys.Home or
            Keys.End or Keys.PageUp or Keys.PageDown or Keys.Up or Keys.Down or
            Keys.Left or Keys.Right or Keys.PrintScreen or Keys.Scroll or Keys.Pause or
            Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or Keys.ShiftKey or
            Keys.LShiftKey or Keys.RShiftKey or Keys.Menu or Keys.LMenu or Keys.RMenu or
            Keys.LWin or Keys.RWin or Keys.Apps or Keys.NumLock or Keys.Add or
            Keys.Subtract or Keys.Multiply or Keys.Divide or Keys.Decimal or
            Keys.OemSemicolon or Keys.Oemplus or Keys.Oemcomma or Keys.OemMinus or
            Keys.OemPeriod or Keys.OemQuestion or Keys.Oemtilde or Keys.OemOpenBrackets or
            Keys.OemPipe or Keys.OemCloseBrackets or Keys.OemQuotes or Keys.VolumeMute or
            Keys.VolumeDown or Keys.VolumeUp or Keys.MediaNextTrack or
            Keys.MediaPreviousTrack or Keys.MediaStop or Keys.MediaPlayPause;
    }
}
