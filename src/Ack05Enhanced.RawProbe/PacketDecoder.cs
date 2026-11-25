using System.Buffers.Binary;

namespace Ack05Enhanced.RawProbe;

internal sealed class PacketDecoder
{
    private readonly Dictionary<nint, SortedSet<string>> held = new();
    internal void Forget(nint handle) => held.Remove(handle);
    internal void Clear() => held.Clear();

    internal object Decode(uint type, nint handle, ReadOnlySpan<byte> data)
    {
        if (type == 1 && data.Length >= 16)
        {
            var makeCode = U16(data);
            var flags = U16(data[2..]);
            var vKey = U16(data[6..]);
            var isBreak = (flags & 1) != 0;
            var e0 = (flags & 2) != 0;
            var e1 = (flags & 4) != 0;
            if (!held.TryGetValue(handle, out var keys)) keys = new();
            if (handle != 0 && makeCode != 0xff && vKey != 0xff)
            {
                var key = $"{(e1 ? "E1-" : e0 ? "E0-" : "")}{(makeCode == 0 ? $"VK-{vKey:X2}" : $"{makeCode:X2}")}";
                if (isBreak) keys.Remove(key);
                else keys.Add(key);
                held[handle] = keys;
            }
            return new { kind = "keyboard", makeCode, vKey, flags, isBreak, e0, e1, held = keys.ToArray() };
        }
        if (type == 0 && data.Length >= 24)
        {
            var buttonFlags = U16(data[4..]);
            var delta = BinaryPrimitives.ReadInt16LittleEndian(data[6..]);
            return new { kind = "mouse", motionFlags = U16(data), buttonFlags, rawButtons = U32(data[8..]),
                x = BinaryPrimitives.ReadInt32LittleEndian(data[12..]), y = BinaryPrimitives.ReadInt32LittleEndian(data[16..]),
                wheel = (buttonFlags & 0x400) != 0 ? delta : 0, hWheel = (buttonFlags & 0x800) != 0 ? delta : 0 };
        }
        if (type == 2 && data.Length >= 8)
        {
            var size = U32(data);
            var count = U32(data[4..]);
            if (size > 0 && count > 0 && (ulong)size * count <= (ulong)(data.Length - 8))
            {
                var reports = new string[count];
                for (var i = 0; i < reports.Length; i++)
                    reports[i] = Convert.ToHexString(data.Slice(8 + i * (int)size, (int)size));
                return new { kind = "hid", size, count, reports };
            }
        }
        return new { kind = "malformed", type, hex = Convert.ToHexString(data) };
    }

    private static ushort U16(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadUInt16LittleEndian(bytes);
    private static uint U32(ReadOnlySpan<byte> bytes) => BinaryPrimitives.ReadUInt32LittleEndian(bytes);
}
