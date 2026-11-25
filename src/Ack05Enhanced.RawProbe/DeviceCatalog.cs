using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Ack05Enhanced.Windows;

namespace Ack05Enhanced.RawProbe;

internal sealed record Source(string Handle, string? Path, string? Instance, Guid? Container,
    string? Vid, string? Pid, string? Usage, string? Collection, string Attribution);

internal sealed class DeviceCatalog
{
    private readonly Dictionary<string, HidDeviceInfo> collections = new(StringComparer.OrdinalIgnoreCase);

    internal void Refresh()
    {
        collections.Clear();
        foreach (var collection in HidDiscovery.EnumerateAck05Collections())
        {
            var (instance, _) = SetupIdentity(collection.Path);
            if (instance != null) collections[instance] = collection;
        }
    }

    internal Source Resolve(nint handle, uint type)
    {
        uint count = 0;
        string? path = null;
        if (handle != 0 && GetRawInputDeviceInfo(handle, 0x20000007, null, ref count) == 0 && count > 0)
        {
            var buffer = new StringBuilder(checked((int)count));
            if (GetRawInputDeviceInfo(handle, 0x20000007, buffer, ref count) != uint.MaxValue)
                path = buffer.ToString();
        }
        var (instance, container) = path == null ? (null, null) : SetupIdentity(path);
        var id = instance ?? path ?? "";
        string? Match(string pattern)
        {
            var match = Regex.Match(id, pattern, RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
        }
        var vid = Match(@"VID_([0-9A-F]{4})") ?? Match(@"VID&[0-9A-F]{2}([0-9A-F]{4})");
        var pid = Match(@"PID[_&]([0-9A-F]{4})");
        string? usage = type switch { 0 => "0001:0002", 1 => "0001:0006", _ => null };
        var attribution = instance == null ? "unresolved" : "other";
        if (instance != null && collections.TryGetValue(instance, out var hid))
        {
            usage = $"{hid.UsagePage:X4}:{hid.Usage:X4}";
            attribution = instance.Contains("{00001812-0000-1000-8000-00805f9b34fb}", StringComparison.OrdinalIgnoreCase)
                ? "ACK05-Bluetooth" : "ACK05-other-transport";
        }
        else if (vid == "28BD" && pid == "0202") attribution = "unresolved";
        return new($"0x{handle:X}", path, instance, container, vid, pid, usage, Match(@"&COL([0-9A-F]{2})"), attribution);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceData { public int Size; public Guid Class; public uint Index; public nint Reserved; }
    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey { public Guid Format; public uint Index; }

    private static (string?, Guid?) SetupIdentity(string path)
    {
        var set = SetupDiCreateDeviceInfoList(0, 0);
        if (set == -1) return (null, null);
        try
        {
            var iface = new DeviceData { Size = Marshal.SizeOf<DeviceData>() };
            var node = iface;
            if (!SetupDiOpenDeviceInterface(set, path, 0, ref iface)) return (null, null);
            // The sizing call also returns the associated devnode (ERROR_INSUFFICIENT_BUFFER).
            SetupDiGetDeviceInterfaceDetail(set, ref iface, 0, 0, out _, ref node);
            var instance = new StringBuilder(256);
            if (!SetupDiGetDeviceInstanceId(set, ref node, instance, instance.Capacity, out _)) return (null, null);
            var key = new PropertyKey { Format = new("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"), Index = 2 };
            var ok = SetupDiGetDeviceProperty(set, ref node, ref key, out var type, out var container, 16, out _, 0);
            return (instance.ToString(), ok && type == 13 ? container : null);
        }
        finally { SetupDiDestroyDeviceInfoList(set); }
    }

    [DllImport("user32", CharSet=CharSet.Unicode)] static extern uint GetRawInputDeviceInfo(nint h, uint cmd, StringBuilder? data, ref uint size);
    [DllImport("setupapi")] static extern nint SetupDiCreateDeviceInfoList(nint cls, nint window);
    [DllImport("setupapi", CharSet=CharSet.Unicode)] static extern bool SetupDiOpenDeviceInterface(nint set, string path, uint flags, ref DeviceData data);
    [DllImport("setupapi", CharSet=CharSet.Unicode)] static extern bool SetupDiGetDeviceInterfaceDetail(nint set, ref DeviceData data, nint detail, uint len, out uint required, ref DeviceData node);
    [DllImport("setupapi", CharSet=CharSet.Unicode)] static extern bool SetupDiGetDeviceInstanceId(nint set, ref DeviceData node, StringBuilder id, int len, out uint required);
    [DllImport("setupapi", CharSet=CharSet.Unicode)] static extern bool SetupDiGetDeviceProperty(nint set, ref DeviceData node, ref PropertyKey key, out uint type, out Guid value, uint size, out uint required, uint flags);
    [DllImport("setupapi")] static extern bool SetupDiDestroyDeviceInfoList(nint set);
}
