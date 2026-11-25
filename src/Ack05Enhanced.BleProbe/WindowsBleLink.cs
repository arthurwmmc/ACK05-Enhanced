using Ack05Enhanced.Windows;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace Ack05Enhanced.BleProbe;

internal sealed class WindowsBleLink : IReadOnlyBleLink, IBleNotificationSource
{
    private readonly BluetoothLEDevice _device;
    private readonly List<GattDeviceService> _services = [];
    private readonly Dictionary<(Guid, Guid), GattCharacteristic> _fields = [];
    private readonly List<Action> _detach = [];
    private readonly CancellationTokenSource _disconnected = new();
    private readonly object _sync = new();
    private bool _closed;

    public WindowsBleLink(BluetoothLEDevice device)
    {
        _device = device;
        device.ConnectionStatusChanged += StatusChanged;
    }

    public ulong Address => _device.BluetoothAddress;
    public CancellationToken ConnectionLost => _disconnected.Token;

    internal IDisposable TraceControlSession()
    {
        var session = _services.Single(s => s.Uuid == BleReadOnlyDiagnostic.Control).Session;
        void Snapshot(string reason) => Console.WriteLine($"{DateTimeOffset.UtcNow:O} {reason} connection={_device.ConnectionStatus} gatt={session.SessionStatus}");
        void Connection(BluetoothLEDevice _, object args) => Snapshot("ConnectionStatusChanged");
        void Status(GattSession _, GattSessionStatusChangedEventArgs args) => Snapshot($"SessionStatusChanged error={args.Error}");
        _device.ConnectionStatusChanged += Connection;
        session.SessionStatusChanged += Status;
        Snapshot("observation-start");
        return new Listener(() =>
        {
            _device.ConnectionStatusChanged -= Connection;
            session.SessionStatusChanged -= Status;
            try { Snapshot("observation-end"); }
            finally { session.Dispose(); }
        });
    }

    private void StatusChanged(BluetoothLEDevice device, object args)
    {
        lock (_sync)
        {
            if (!_closed && device.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
                _disconnected.Cancel();
        }
    }

    public async Task<Guid[]> ServicesAsync(CancellationToken token)
    {
        var result = await _device.GetGattServicesAsync(BluetoothCacheMode.Uncached).AsTask(token);
        Check(result.Status, result.ProtocolError, "service discovery");
        _services.AddRange(result.Services);
        if (_device.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            _disconnected.Cancel();
        return _services.Select(s => s.Uuid).ToArray();
    }

    public async Task<BleField[]> FieldsAsync(Guid service, CancellationToken token)
    {
        var gatt = _services.Single(s => s.Uuid == service);
        var access = await gatt.OpenAsync(GattSharingMode.SharedReadOnly).AsTask(token);
        if (access is not (GattOpenStatus.Success or GattOpenStatus.AlreadyOpened))
            throw new IOException($"Shared read-only access to {service}: {access}");
        var result = await gatt.GetCharacteristicsAsync(BluetoothCacheMode.Uncached).AsTask(token);
        Check(result.Status, result.ProtocolError, $"characteristic discovery on {service}");
        foreach (var field in result.Characteristics) _fields[(service, field.Uuid)] = field;
        return result.Characteristics.Select(c => new BleField(c.Uuid, c.CharacteristicProperties.ToString(),
            c.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Read),
            (c.CharacteristicProperties & (GattCharacteristicProperties.Notify | GattCharacteristicProperties.Indicate)) != 0))
            .ToArray();
    }

    public async Task<byte[]> ReadAsync(Guid service, Guid field, CancellationToken token)
    {
        var result = await _fields[(service, field)].ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(token);
        Check(result.Status, result.ProtocolError, "characteristic read");
        return Bytes(result.Value);
    }

    public async Task<string> ReadCccdAsync(Guid field, CancellationToken token)
    {
        var characteristic = _fields[(BleReadOnlyDiagnostic.Control, field)];
        var descriptor = await characteristic.ReadClientCharacteristicConfigurationDescriptorAsync().AsTask(token);
        Check(descriptor.Status, descriptor.ProtocolError, "CCCD read");
        return descriptor.ClientCharacteristicConfigurationDescriptor.ToString();
    }

    public async Task<string> ObserveExistingAsync(Guid field, Action<byte[]> onValue, CancellationToken token)
    {
        var mode = await ReadCccdAsync(field, token);
        if (mode == "None")
            return "CCCD off; no listener enabled";
        var registration = Listen(field, (data, _) => { if (!token.IsCancellationRequested) onValue(data); });
        _detach.Add(registration.Dispose);
        return $"CCCD already {mode}; local listener attached";
    }

    public IDisposable Listen(Guid field, Action<byte[], string> onValue)
    {
        var characteristic = _fields[(BleReadOnlyDiagnostic.Control, field)];
        TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> listener = (_, e) =>
        {
            lock (_sync)
                if (!_closed && !_disconnected.IsCancellationRequested)
                    onValue(Bytes(e.CharacteristicValue), _device.ConnectionStatus.ToString());
        };
        characteristic.ValueChanged += listener;
        return new Listener(() => characteristic.ValueChanged -= listener);
    }

    private sealed class Listener(Action detach) : IDisposable { public void Dispose() => detach(); }

    private static byte[] Bytes(IBuffer value)
    {
        using var reader = DataReader.FromBuffer(value);
        var bytes = new byte[reader.UnconsumedBufferLength];
        reader.ReadBytes(bytes);
        return bytes;
    }

    private static void Check(GattCommunicationStatus status, byte? protocolError, string operation)
    {
        if (status != GattCommunicationStatus.Success)
            throw new IOException($"{operation}: {status} (ATT {protocolError?.ToString("X2") ?? "n/a"})");
    }

    public void Dispose()
    {
        lock (_sync) _closed = true;
        _device.ConnectionStatusChanged -= StatusChanged;
        foreach (var detach in _detach) detach();
        foreach (var service in _services) service.Dispose();
        _device.Dispose();
        _disconnected.Dispose();
    }
}
