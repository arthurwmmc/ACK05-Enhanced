using Ack05Enhanced.Windows;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace Ack05Enhanced.BleProbe;

internal static class Ffe0Inspection
{
    internal static async Task<int> RunAsync(bool readWrite)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var token = timeout.Token;
        var mode = readWrite ? GattSharingMode.SharedReadAndWrite : GattSharingMode.SharedReadOnly;
        try
        {
            var candidates = await WindowsBleDiscovery.ScanAsync(token);
            Console.WriteLine($"Candidates={candidates.Length}; requested={mode}");
            if (candidates.Length != 1) return 3;
            using var device = await BluetoothLEDevice.FromIdAsync(candidates[0].Id).AsTask(token);
            if (device is null) return 3;
            var result = await device.GetGattServicesForUuidAsync(BleReadOnlyDiagnostic.Control,
                BluetoothCacheMode.Uncached).AsTask(token);
            try
            {
                Console.WriteLine($"FFE0 discovery={result.Status}; instances={result.Services.Count}");
                if (result.Status != GattCommunicationStatus.Success || result.Services.Count != 1) return 3;
                var service = result.Services[0];
                Console.WriteLine($"DeviceId={service.DeviceId}");
                var opened = await service.OpenAsync(mode).AsTask(token);
                Console.WriteLine($"Open={opened}; granted={(opened == GattOpenStatus.Success ? mode.ToString() : "unverified")}");
                if (opened != GattOpenStatus.Success) return 3;
                var fields = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(token);
                Console.WriteLine($"Characteristics={fields.Status}");
                if (fields.Status != GattCommunicationStatus.Success) return 3;
                foreach (var field in fields.Characteristics)
                {
                    Console.WriteLine($"{field.Uuid} properties={field.CharacteristicProperties}");
                    if (field.Uuid != BleReadOnlyDiagnostic.Command && field.Uuid != BleReadOnlyDiagnostic.Ack &&
                        field.Uuid != BleReadOnlyDiagnostic.Reports) continue;
                    var descriptors = await field.GetDescriptorsAsync(BluetoothCacheMode.Uncached).AsTask(token);
                    Console.WriteLine($"Descriptors={descriptors.Status} {string.Join(", ", descriptors.Descriptors.Select(d => d.Uuid))}");
                    if (descriptors.Descriptors.Any(d => d.Uuid == BleReadOnlyDiagnostic.ShortId(0x2902)))
                    {
                        var config = await field.ReadClientCharacteristicConfigurationDescriptorAsync().AsTask(token);
                        Console.WriteLine($"CCCD read={config.Status}; value={(config.Status == GattCommunicationStatus.Success ? config.ClientCharacteristicConfigurationDescriptor.ToString() : "unknown")}");
                    }
                    if (field.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read))
                    {
                        var read = await field.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(token);
                        Console.WriteLine($"Value read={read.Status}");
                        if (read.Status == GattCommunicationStatus.Success)
                        {
                            using var reader = DataReader.FromBuffer(read.Value);
                            var bytes = new byte[reader.UnconsumedBufferLength];
                            reader.ReadBytes(bytes);
                            Console.WriteLine($"Value={Convert.ToHexString(bytes)}");
                        }
                    }
                }
                return 0;
            }
            finally { foreach (var service in result.Services) service.Dispose(); }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Inspection interrupted: {e.GetType().Name} 0x{e.HResult:X8}");
            return 3;
        }
    }
}
