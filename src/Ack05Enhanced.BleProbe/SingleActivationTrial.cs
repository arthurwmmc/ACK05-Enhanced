using System.Diagnostics;
using Ack05Enhanced.Windows;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using static Ack05Enhanced.Windows.BleReadOnlyDiagnostic;

namespace Ack05Enhanced.BleProbe;

internal static class SingleActivationTrial
{
    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new IOException(detail);
    }

    private static void CheckOfficialApp()
    {
        var apps = Process.GetProcessesByName("XPPenTablet");
        foreach (var app in apps) app.Dispose();
        Require(apps.Length == 0, "Official XP-Pen application is running.");
    }

    private static void Log(string message) => Console.WriteLine($"{DateTimeOffset.UtcNow:O} {message}");

    public static async Task<int> RunAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            CheckOfficialApp();
            var token = timeout.Token;
            var candidates = await WindowsBleDiscovery.ScanAsync(token);
            Require(candidates.Length == 1, "Expected one named candidate.");
            using var device = await BluetoothLEDevice.FromIdAsync(candidates[0].Id).AsTask(token)
                ?? throw new IOException("BLE device unavailable.");
            await VerifyIdentity(device, token);
            var result = await device.GetGattServicesForUuidAsync(Control, BluetoothCacheMode.Uncached).AsTask(token);
            try
            {
                Require(result.Status == GattCommunicationStatus.Success && result.Services.Count == 1, "FFE0 unavailable or ambiguous.");
                var service = result.Services[0];
                var open = await service.OpenAsync(GattSharingMode.SharedReadAndWrite).AsTask(token);
                Log($"FFE0 SharedReadAndWrite {open}");
                Require(open == GattOpenStatus.Success, "Shared open failed.");
                var fields = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(token);
                Require(fields.Status == GattCommunicationStatus.Success, "Characteristic discovery failed.");
                var cmd = fields.Characteristics.Single(c => c.Uuid == Command);
                var ack = fields.Characteristics.Single(c => c.Uuid == Ack);
                var reports = fields.Characteristics.Single(c => c.Uuid == Reports);
                Log($"0001 properties={cmd.CharacteristicProperties}");
                Require(cmd.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse), "Required write type unavailable.");
                foreach (var field in new[] { ack, reports })
                {
                    var cccd = await field.ReadClientCharacteristicConfigurationDescriptorAsync().AsTask(token);
                    Require(field.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify) &&
                        cccd.Status == GattCommunicationStatus.Success &&
                        cccd.ClientCharacteristicConfigurationDescriptor == GattClientCharacteristicConfigurationDescriptorValue.Notify,
                        "Notify precondition failed.");
                    Log($"{field.Uuid} CCCD=Notify");
                }
                return await ObserveAndWrite(device, cmd, ack, reports, timeout);
            }
            finally { foreach (var s in result.Services) s.Dispose(); }
        }
        catch (Exception e) { Log($"STOP {e.GetType().Name}: {e.Message}"); return 3; }
    }

    private static async Task VerifyIdentity(BluetoothLEDevice device, CancellationToken token)
    {
        var services = await device.GetGattServicesForUuidAsync(DeviceInfo, BluetoothCacheMode.Uncached).AsTask(token);
        try
        {
            Require(services.Status == GattCommunicationStatus.Success, "No Device Information.");
            var service = services.Services.Single();
            Require(await service.OpenAsync(GattSharingMode.SharedReadOnly).AsTask(token) == GattOpenStatus.Success, "Device Information open failed.");
            var fields = await service.GetCharacteristicsForUuidAsync(PnpId, BluetoothCacheMode.Uncached).AsTask(token);
            Require(fields.Status == GattCommunicationStatus.Success, "PnP ID unavailable.");
            var read = await fields.Characteristics.Single().ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(token);
            Require(read.Status == GattCommunicationStatus.Success && read.Value.Length == 7, "PnP ID read failed.");
            using var reader = DataReader.FromBuffer(read.Value);
            reader.ByteOrder = ByteOrder.LittleEndian;
            Require(reader.ReadByte() == 2 && reader.ReadUInt16() == 0x28bd && reader.ReadUInt16() == 0x0202, "PnP ID mismatch.");
            Log("PnP confirmed 28BD:0202");
        }
        finally { foreach (var s in services.Services) s.Dispose(); }
    }

    private static async Task<int> ObserveAndWrite(BluetoothLEDevice device, GattCharacteristic cmd,
        GattCharacteristic ack, GattCharacteristic reports, CancellationTokenSource timeout)
    {
        var heartbeat = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new object();
        var closed = false;
        long? previous = null;
        int seq = 0;
        void Status(BluetoothLEDevice d, object _) { if (d.ConnectionStatus == BluetoothConnectionStatus.Disconnected) lost.TrySetResult(); }
        void Packet(GattCharacteristic c, GattValueChangedEventArgs e)
        {
            lock (gate)
            {
                if (closed) return;
                using var reader = DataReader.FromBuffer(e.CharacteristicValue);
                var bytes = new byte[reader.UnconsumedBufferLength];
                reader.ReadBytes(bytes);
                var now = Stopwatch.GetTimestamp();
                var delta = previous is long p ? Stopwatch.GetElapsedTime(p, now).TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) : "n/a";
                previous = now;
                Log($"seq={++seq} channel={c.Uuid} size={bytes.Length} hex={Convert.ToHexString(bytes)} delta_ms={delta} state={device.ConnectionStatus}");
                if (c.Uuid == Reports && bytes is [2, 0xf2, _, <= 100, _, ..]) heartbeat.TrySetResult();
            }
        }
        device.ConnectionStatusChanged += Status;
        try
        {
            ack.ValueChanged += Packet;
            try
            {
                reports.ValueChanged += Packet;
                try
                {
                    Log("Listeners installed 0002 then 0003");
                    await Task.WhenAny(heartbeat.Task, lost.Task).WaitAsync(timeout.Token);
                    CheckOfficialApp();
                    Require(heartbeat.Task.IsCompleted && !lost.Task.IsCompleted && device.ConnectionStatus == BluetoothConnectionStatus.Connected, "No live heartbeat.");
                    using var writer = new DataWriter();
                    writer.WriteBytes([2, 0xb0, 4, 0, 0, 0, 0, 0, 0, 0]);
                    Log("SEND 0001 WriteWithoutResponse hex=02B00400000000000000");
                    var written = await cmd.WriteValueWithResultAsync(writer.DetachBuffer(), GattWriteOption.WriteWithoutResponse).AsTask(timeout.Token);
                    Log($"WRITE_RESULT {written.Status} ATT={written.ProtocolError}");
                    Require(written.Status == GattCommunicationStatus.Success, "Write failed.");
                    var until = Task.Delay(TimeSpan.FromSeconds(120));
                    while (!until.IsCompleted)
                    {
                        await Task.WhenAny(lost.Task, until, Task.Delay(1000));
                        Require(!lost.Task.IsCompleted, "Disconnected.");
                        CheckOfficialApp();
                    }
                    return 0;
                }
                finally { lock (gate) closed = true; reports.ValueChanged -= Packet; }
            }
            finally { ack.ValueChanged -= Packet; }
        }
        finally { device.ConnectionStatusChanged -= Status; Log("Observation ended; releasing handles."); }
    }
}
