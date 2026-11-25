using System.Buffers.Binary;

namespace Ack05Enhanced.Windows;

public sealed record BleCandidate(string Id, string Name, bool Connected);
public sealed record BleField(Guid Id, string Properties, bool Readable, bool Signals);

// The diagnostic can inspect metadata, read a value or listen on an already-enabled channel.
public interface IReadOnlyBleLink : IDisposable
{
    ulong Address { get; }
    CancellationToken ConnectionLost { get; }
    Task<Guid[]> ServicesAsync(CancellationToken token);
    Task<BleField[]> FieldsAsync(Guid service, CancellationToken token);
    Task<byte[]> ReadAsync(Guid service, Guid field, CancellationToken token);
    Task<string> ObserveExistingAsync(Guid field, Action<byte[]> onValue, CancellationToken token);
}

public static class BleReadOnlyDiagnostic
{
    public static Guid ShortId(int id) => new($"{id:x8}-0000-1000-8000-00805f9b34fb");
    public static readonly Guid Control = ShortId(0xffe0);
    public static readonly Guid Command = ShortId(1), Ack = ShortId(2), Reports = ShortId(3);
    public static readonly Guid TelinkOta = new("00010203-0405-0607-0809-0a0b0c0d1912");
    public static readonly Guid Battery = ShortId(0x180f), Level = ShortId(0x2a19);
    public static readonly Guid DeviceInfo = ShortId(0x180a), PnpId = ShortId(0x2a50);

    public static BleCandidate[] SelectCandidates(IEnumerable<BleCandidate> devices)
    {
        var matches = devices.Where(d => string.Equals(d.Name.Replace(" ", ""), "ShortcutRemote",
            StringComparison.OrdinalIgnoreCase)).DistinctBy(d => d.Id).ToArray();
        var connected = matches.Where(d => d.Connected).ToArray();
        return connected.Length > 0 ? connected : matches;
    }

    public static async Task<bool> InspectAsync(IReadOnlyBleLink link, Action<string> log,
        Action<Guid, byte[]> onValue, CancellationToken token)
    {
        var lost = link.ConnectionLost;
        var services = await link.ServicesAsync(token);
        foreach (var service in services)
            log($"service {service}");
        log(services.Contains(TelinkOta)
            ? "Revision hint: Telink OTA UUID present (metadata only)."
            : "Revision unknown: no Telink marker observed.");

        var pnp = await ReadOptionalAsync(DeviceInfo, PnpId);
        if (pnp is { Length: 7 })
        {
            var vendor = BinaryPrimitives.ReadUInt16LittleEndian(pnp.AsSpan(1));
            var product = BinaryPrimitives.ReadUInt16LittleEndian(pnp.AsSpan(3));
            log($"PnP ID: source={pnp[0]} VID={vendor:X4} PID={product:X4}");
            if (pnp[0] == 2 && (vendor != 0x28BD || product != 0x0202))
            {
                log("USB VID/PID mismatch; skipping vendor channels.");
                return false;
            }
        }
        var battery = await ReadOptionalAsync(Battery, Level);
        if (battery is { Length: 1 } && battery[0] <= 100)
            log($"Standard battery level (read): {battery[0]}%");
        if (services.Count(s => s == Control) != 1)
        {
            log("Expected one FFE0 service; vendor endpoint unconfirmed.");
            return false;
        }
        BleField[] fields;
        try { fields = await link.FieldsAsync(Control, token); }
        catch (IOException e)
        {
            log(e.Message);
            return false;
        }
        foreach (var field in fields)
            log($"FFE0 characteristic {field.Id} [{field.Properties}]");
        if (new[] { Command, Ack, Reports }.Any(id => fields.Count(f => f.Id == id) != 1))
        {
            log("Incomplete or ambiguous FFE0 fingerprint.");
            return false;
        }

        foreach (var id in new[] { Ack, Reports })
        {
            if (!fields.Single(f => f.Id == id).Signals)
            {
                log($"{id}: Notify/Indicate unavailable.");
                continue;
            }
            try
            {
                var state = await link.ObserveExistingAsync(id, bytes =>
                {
                    if (!token.IsCancellationRequested && !lost.IsCancellationRequested)
                        onValue(id, bytes);
                }, token);
                log($"{id}: {state}");
            }
            catch (IOException e) { log($"{id}: {e.Message}"); }
        }
        return true;

        async Task<byte[]?> ReadOptionalAsync(Guid service, Guid field)
        {
            if (services.Count(s => s == service) != 1) return null;
            try
            {
                var matches = (await link.FieldsAsync(service, token)).Where(f => f.Id == field).ToArray();
                if (matches.Length == 1 && matches[0].Readable)
                    return await link.ReadAsync(service, field, token);
            }
            catch (IOException e) { log($"Optional read {field}: {e.Message}"); }
            return null;
        }
    }

    public static string Describe(Guid channel, byte[] value)
    {
        if (channel == Ack) return $"0002 status notification ({value.Length} bytes)";
        if (channel != Reports || value.Length < 2 || value[0] != 2)
            return $"unclassified notification ({value.Length} bytes)";
        return value[1] switch
        {
            0xf2 when value.Length >= 5 && value[3] <= 100 =>
                $"F2 battery {value[3]}%, charging flag {value[4]}",
            0xf0 when value.Length >= 8 => "F0 physical-state activity",
            0xf8 when value.Length >= 4 => "F8 device reconnect marker",
            _ => $"unclassified report ({value.Length} bytes)",
        };
    }
}
