using System.ComponentModel;
using System.Runtime.InteropServices;
using Ack05Enhanced.Core;

namespace Ack05Enhanced.Windows;

public sealed class User32WindowsInputInjector : IAck05WindowsInputInjector
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint MouseEventWheel = 0x0800;
    private const int WheelDelta = 120;

    public void KeyDown(Ack05KeyboardKey key) => SendKey(key, keyUp: false);

    public void KeyUp(Ack05KeyboardKey key) => SendKey(key, keyUp: true);

    public void MouseWheel(int steps)
    {
        if (steps == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(steps), "Wheel steps cannot be zero.");
        }

        var input = new NativeInput
        {
            Type = InputMouse,
            Union = new NativeInputUnion
            {
                Mouse = new NativeMouseInput
                {
                    MouseData = unchecked((uint)(steps * WheelDelta)),
                    Flags = MouseEventWheel,
                },
            },
        };
        Send(input);
    }

    private static void SendKey(Ack05KeyboardKey key, bool keyUp)
    {
        var mapping = WindowsVirtualKeyMap.Get(key);
        var flags = (keyUp ? KeyEventKeyUp : 0u) |
            (mapping.Extended ? KeyEventExtendedKey : 0u);
        var input = new NativeInput
        {
            Type = InputKeyboard,
            Union = new NativeInputUnion
            {
                Keyboard = new NativeKeyboardInput
                {
                    VirtualKey = mapping.VirtualKey,
                    Flags = flags,
                },
            },
        };
        Send(input);
    }

    private static void Send(NativeInput input)
    {
        var inputs = new[] { input };
        if (SendInput(1, inputs, Marshal.SizeOf<NativeInput>()) != 1)
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Windows SendInput did not inject the requested event. UIPI may have blocked it.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint inputCount,
        [In] NativeInput[] inputs,
        int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        internal uint Type;
        internal NativeInputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct NativeInputUnion
    {
        [FieldOffset(0)]
        internal NativeMouseInput Mouse;

        [FieldOffset(0)]
        internal NativeKeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMouseInput
    {
        internal int X;
        internal int Y;
        internal uint MouseData;
        internal uint Flags;
        internal uint Time;
        internal UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeKeyboardInput
    {
        internal ushort VirtualKey;
        internal ushort ScanCode;
        internal uint Flags;
        internal uint Time;
        internal UIntPtr ExtraInfo;
    }
}
