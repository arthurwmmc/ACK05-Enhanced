using Ack05Enhanced.Core;

namespace Ack05Enhanced.Windows;

internal static class WindowsVirtualKeyMap
{
    internal readonly record struct Mapping(ushort VirtualKey, bool Extended = false);

    internal static Mapping Get(Ack05KeyboardKey key) => key switch
    {
        >= Ack05KeyboardKey.A and <= Ack05KeyboardKey.Z =>
            new((ushort)(0x41 + key - Ack05KeyboardKey.A)),
        >= Ack05KeyboardKey.Digit0 and <= Ack05KeyboardKey.Digit9 =>
            new((ushort)(0x30 + key - Ack05KeyboardKey.Digit0)),
        >= Ack05KeyboardKey.F1 and <= Ack05KeyboardKey.F24 =>
            new((ushort)(0x70 + key - Ack05KeyboardKey.F1)),
        Ack05KeyboardKey.Escape => new(0x1B),
        Ack05KeyboardKey.Tab => new(0x09),
        Ack05KeyboardKey.CapsLock => new(0x14),
        Ack05KeyboardKey.Backspace => new(0x08),
        Ack05KeyboardKey.Enter => new(0x0D),
        Ack05KeyboardKey.Space => new(0x20),
        Ack05KeyboardKey.Insert => new(0x2D, true),
        Ack05KeyboardKey.Delete => new(0x2E, true),
        Ack05KeyboardKey.Home => new(0x24, true),
        Ack05KeyboardKey.End => new(0x23, true),
        Ack05KeyboardKey.PageUp => new(0x21, true),
        Ack05KeyboardKey.PageDown => new(0x22, true),
        Ack05KeyboardKey.ArrowUp => new(0x26, true),
        Ack05KeyboardKey.ArrowDown => new(0x28, true),
        Ack05KeyboardKey.ArrowLeft => new(0x25, true),
        Ack05KeyboardKey.ArrowRight => new(0x27, true),
        Ack05KeyboardKey.PrintScreen => new(0x2C, true),
        Ack05KeyboardKey.ScrollLock => new(0x91),
        Ack05KeyboardKey.Pause => new(0x13),
        Ack05KeyboardKey.LeftControl => new(0xA2),
        Ack05KeyboardKey.RightControl => new(0xA3, true),
        Ack05KeyboardKey.LeftShift => new(0xA0),
        Ack05KeyboardKey.RightShift => new(0xA1),
        Ack05KeyboardKey.LeftAlt => new(0xA4),
        Ack05KeyboardKey.RightAlt => new(0xA5, true),
        Ack05KeyboardKey.LeftWindows => new(0x5B, true),
        Ack05KeyboardKey.RightWindows => new(0x5C, true),
        Ack05KeyboardKey.Menu => new(0x5D, true),
        Ack05KeyboardKey.NumLock => new(0x90, true),
        >= Ack05KeyboardKey.Numpad0 and <= Ack05KeyboardKey.Numpad9 =>
            new((ushort)(0x60 + key - Ack05KeyboardKey.Numpad0)),
        Ack05KeyboardKey.NumpadAdd => new(0x6B),
        Ack05KeyboardKey.NumpadSubtract => new(0x6D),
        Ack05KeyboardKey.NumpadMultiply => new(0x6A),
        Ack05KeyboardKey.NumpadDivide => new(0x6F, true),
        Ack05KeyboardKey.NumpadDecimal => new(0x6E),
        Ack05KeyboardKey.NumpadEnter => new(0x0D, true),
        Ack05KeyboardKey.Semicolon => new(0xBA),
        Ack05KeyboardKey.Equal => new(0xBB),
        Ack05KeyboardKey.Comma => new(0xBC),
        Ack05KeyboardKey.Minus => new(0xBD),
        Ack05KeyboardKey.Period => new(0xBE),
        Ack05KeyboardKey.Slash => new(0xBF),
        Ack05KeyboardKey.Backquote => new(0xC0),
        Ack05KeyboardKey.LeftBracket => new(0xDB),
        Ack05KeyboardKey.Backslash => new(0xDC),
        Ack05KeyboardKey.RightBracket => new(0xDD),
        Ack05KeyboardKey.Quote => new(0xDE),
        Ack05KeyboardKey.VolumeMute => new(0xAD, true),
        Ack05KeyboardKey.VolumeDown => new(0xAE, true),
        Ack05KeyboardKey.VolumeUp => new(0xAF, true),
        Ack05KeyboardKey.MediaNext => new(0xB0, true),
        Ack05KeyboardKey.MediaPrevious => new(0xB1, true),
        Ack05KeyboardKey.MediaStop => new(0xB2, true),
        Ack05KeyboardKey.MediaPlayPause => new(0xB3, true),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unsupported keyboard key."),
    };
}
