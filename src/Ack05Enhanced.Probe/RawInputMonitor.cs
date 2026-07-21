using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Ack05Enhanced.Probe;

internal static class RawInputMonitor
{
    private const uint RidInput = 0x10000003;
    private const uint RidiDeviceName = 0x20000007;
    private const uint RidevInputSink = 0x00000100;
    private const uint WmInput = 0x00FF;
    private const uint WmClose = 0x0010;
    private const uint WmDestroy = 0x0002;
    private static readonly WindowProcedureDelegateType WindowProcedureDelegate = WindowProcedure;
    private static int ignoredMouseNoticeCount;
    private static int ignoredKeyboardNoticeCount;
    private static int ignoredHidNoticeCount;
    private static int rawInputErrorNoticeCount;
    private static bool diagnoseOtherSources;

    internal static Task RunAsync(
        bool diagnoseNonAck05Sources,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                diagnoseOtherSources = diagnoseNonAck05Sources;
                RunMessageLoop(cancellationToken);
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "ACK05 Raw Input Monitor",
        };

        thread.Start();
        return completion.Task;
    }

    internal static void ListDevices()
    {
        uint count = 0;
        var entrySize = (uint)Marshal.SizeOf<RawInputDeviceListEntry>();
        if (GetRawInputDeviceList(IntPtr.Zero, ref count, entrySize) == uint.MaxValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not count Raw Input devices.");
        }

        var buffer = Marshal.AllocHGlobal(checked((int)(count * entrySize)));
        try
        {
            if (GetRawInputDeviceList(buffer, ref count, entrySize) == uint.MaxValue)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not list Raw Input devices.");
            }

            var ack05Index = 0;
            for (var index = 0; index < count; index++)
            {
                var entry = Marshal.PtrToStructure<RawInputDeviceListEntry>(
                    IntPtr.Add(buffer, checked((int)(index * entrySize))));
                var name = ReadDeviceName(entry.Device);
                if (!IsAck05DeviceName(name))
                {
                    continue;
                }

                Console.WriteLine($"[{ack05Index++}] type={FormatType(entry.Type)} {name}");
            }

            if (ack05Index == 0)
            {
                Console.WriteLine("No ACK05 Windows Raw Input devices found.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void RunMessageLoop(CancellationToken cancellationToken)
    {
        var instance = GetModuleHandle(null);
        var className = $"Ack05EnhancedRawInput_{Environment.ProcessId}";
        var windowClass = new WindowClass
        {
            Instance = instance,
            WindowProcedure = Marshal.GetFunctionPointerForDelegate(WindowProcedureDelegate),
            ClassName = className,
        };

        if (RegisterClass(ref windowClass) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the Raw Input window class.");
        }

        var window = CreateWindowEx(
            0,
            className,
            "ACK05 Raw Input Monitor",
            0,
            0,
            0,
            0,
            0,
            IntPtr.Zero,
            IntPtr.Zero,
            instance,
            IntPtr.Zero);
        if (window == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create the Raw Input message window.");
        }

        using var cancellationRegistration = cancellationToken.Register(
            () => PostMessage(window, WmClose, UIntPtr.Zero, IntPtr.Zero));

        var registrations = new[]
        {
            new RawInputDevice(0x01, 0x02, RidevInputSink, window),
            new RawInputDevice(0x01, 0x06, RidevInputSink, window),
            new RawInputDevice(0x0D, 0x02, RidevInputSink, window),
        };
        if (!RegisterRawInputDevices(
                registrations,
                (uint)registrations.Length,
                (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register for Raw Input.");
        }

        Console.WriteLine("Read-only Windows Raw Input monitor: no command was sent to the device.");
        Console.WriteLine("ACK05 keyboard, mouse, and digitizer events will be shown.");
        Console.WriteLine("Press Ctrl+C to stop.");
        Console.Out.Flush();

        while (true)
        {
            var result = GetMessage(out var message, IntPtr.Zero, 0, 0);
            if (result == 0)
            {
                break;
            }

            if (result == -1)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Raw Input message loop failed.");
            }

            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }
    }

    private static IntPtr WindowProcedure(
        IntPtr window,
        uint message,
        UIntPtr wordParameter,
        IntPtr longParameter)
    {
        if (message == WmInput)
        {
            ProcessRawInput(longParameter);
        }
        else if (message == WmClose)
        {
            DestroyWindow(window);
            return IntPtr.Zero;
        }
        else if (message == WmDestroy)
        {
            PostQuitMessage(0);
            return IntPtr.Zero;
        }

        return DefWindowProc(window, message, wordParameter, longParameter);
    }

    private static void ProcessRawInput(IntPtr rawInputHandle)
    {
        uint size = 0;
        var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        if (GetRawInputData(rawInputHandle, RidInput, IntPtr.Zero, ref size, headerSize) != 0 ||
            size < headerSize)
        {
            WriteRawInputError("could not determine event size");
            return;
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(rawInputHandle, RidInput, buffer, ref size, headerSize) != size)
            {
                WriteRawInputError("could not read event payload");
                return;
            }

            var header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            var deviceName = ReadDeviceName(header.Device);
            if (!IsAck05DeviceName(deviceName))
            {
                if (diagnoseOtherSources && ShouldWriteIgnoredEventNotice(header.Type))
                {
                    Console.WriteLine(
                        $"raw-input diagnostic: event from another {FormatType(header.Type)} device ignored" +
                        (header.Type == 0 ? "." : $"; source={deviceName ?? "unknown"}"));
                    Console.Out.Flush();
                }

                return;
            }

            var payloadLength = checked((int)(size - headerSize));
            var payload = new byte[payloadLength];
            Marshal.Copy(IntPtr.Add(buffer, (int)headerSize), payload, 0, payloadLength);
            Console.WriteLine(
                $"raw-input {DateTimeOffset.Now:HH:mm:ss.fff} " +
                $"type={FormatType(header.Type)} bytes={Convert.ToHexString(payload)}");
            Console.Out.Flush();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void WriteRawInputError(string message)
    {
        if (Interlocked.Increment(ref rawInputErrorNoticeCount) <= 5)
        {
            Console.WriteLine($"raw-input diagnostic: {message} (Win32 {Marshal.GetLastWin32Error()}).");
            Console.Out.Flush();
        }
    }

    private static bool ShouldWriteIgnoredEventNotice(uint type) => type switch
    {
        0 => Interlocked.Increment(ref ignoredMouseNoticeCount) <= 5,
        1 => Interlocked.Increment(ref ignoredKeyboardNoticeCount) <= 10,
        _ => Interlocked.Increment(ref ignoredHidNoticeCount) <= 10,
    };

    private static string? ReadDeviceName(IntPtr device)
    {
        uint characterCount = 0;
        if (GetRawInputDeviceInfo(device, RidiDeviceName, IntPtr.Zero, ref characterCount) == uint.MaxValue ||
            characterCount == 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal(checked((int)(characterCount + 1) * sizeof(char)));
        try
        {
            if (GetRawInputDeviceInfo(device, RidiDeviceName, buffer, ref characterCount) == uint.MaxValue)
            {
                return null;
            }

            return Marshal.PtrToStringUni(buffer, (int)characterCount)?.TrimEnd('\0');
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool IsAck05DeviceName(string? deviceName)
    {
        if (deviceName is null)
        {
            return false;
        }

        return deviceName.Contains("28bd", StringComparison.OrdinalIgnoreCase) &&
               deviceName.Contains("0202", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatType(uint type) => type switch
    {
        0 => "mouse",
        1 => "keyboard",
        2 => "hid",
        _ => $"unknown-{type}",
    };

    private delegate IntPtr WindowProcedureDelegateType(
        IntPtr window,
        uint message,
        UIntPtr wordParameter,
        IntPtr longParameter);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClass(ref WindowClass windowClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(
        IntPtr window,
        uint message,
        UIntPtr wordParameter,
        IntPtr longParameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(
        IntPtr window,
        uint message,
        UIntPtr wordParameter,
        IntPtr longParameter);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(
        out Message message,
        IntPtr window,
        uint minimumMessage,
        uint maximumMessage);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(ref Message message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref Message message);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(
        RawInputDevice[] rawInputDevices,
        uint deviceCount,
        uint structureSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr rawInput,
        uint command,
        IntPtr data,
        ref uint size,
        uint headerSize);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetRawInputDeviceInfo(
        IntPtr device,
        uint command,
        IntPtr data,
        ref uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList(
        IntPtr rawInputDeviceList,
        ref uint deviceCount,
        uint structureSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        internal uint Style;
        internal IntPtr WindowProcedure;
        internal int ClassExtraBytes;
        internal int WindowExtraBytes;
        internal IntPtr Instance;
        internal IntPtr Icon;
        internal IntPtr Cursor;
        internal IntPtr BackgroundBrush;
        internal string? MenuName;
        internal string ClassName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct RawInputDevice(
        ushort UsagePage,
        ushort Usage,
        uint Flags,
        IntPtr TargetWindow);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        internal uint Type;
        internal uint Size;
        internal IntPtr Device;
        internal UIntPtr WordParameter;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceListEntry
    {
        internal IntPtr Device;
        internal uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        internal IntPtr Window;
        internal uint Value;
        internal UIntPtr WordParameter;
        internal IntPtr LongParameter;
        internal uint Time;
        internal Point Point;
        internal uint Private;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        internal int X;
        internal int Y;
    }
}
