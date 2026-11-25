using Ack05Enhanced.RawProbe;
using System.Text.Json;

internal static class RawProbeTests
{
    internal static void Decode()
    {
        var decoder = new PacketDecoder();
        JsonElement Read(uint type, int handle, string hex) =>
            JsonSerializer.SerializeToElement(decoder.Decode(type, handle, Convert.FromHexString(hex)));
        int Held(int handle, string prefix) =>
            Read(1, handle, prefix + "000011000000000000000000").GetProperty("held").GetArrayLength();
        if (Held(1, "1D000000") != 1 || Held(2, "1D000200") != 1 ||
            Held(2, "1D000100") != 1 || Held(1, "1D000100") != 0 || Held(0, "1D000000") != 0)
            throw new Exception("Device isolation or E0/break");
        decoder.Forget(2);
        if (Held(2, "2A000000") != 1) throw new Exception("Reused handle");
        if (Held(2, "2A000000") != 1 || Held(2, "1D000400") != 2 || Held(2, "1D000500") != 1)
            throw new Exception("Repeat or E1 release");
        decoder.Clear();
        if (Held(2, "1D000000") != 1) throw new Exception("Focus loss or stage boundary");
        var mouse = Read(0, 3, "00000000000888FF" + new string('0', 32));
        if (mouse.GetProperty("hWheel").GetInt32() != -120) throw new Exception("Signed wheel");
        var hid = Read(2, 4, "0200000002000000AABBCCDD");
        if (hid.GetProperty("reports")[1].GetString() != "CCDD" ||
            Read(2, 4, "FFFFFFFF02000000").GetProperty("kind").GetString() != "malformed")
            throw new Exception("RAWHID bounds");
    }
}
