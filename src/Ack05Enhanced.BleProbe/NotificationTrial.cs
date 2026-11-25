using System.Diagnostics;
using Ack05Enhanced.Windows;
using Windows.Devices.Bluetooth;

namespace Ack05Enhanced.BleProbe;

internal static class NotificationTrial
{
    internal static async Task<int> RunAsync(int seconds)
    {
        using var end = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; end.Cancel(); };
        Console.CancelKeyPress += cancel;
        var capture = new BleNotificationCapture(Console.WriteLine);
        var sessions = 0;
        var losses = 0;
        try
        {
            while (!end.IsCancellationRequested)
            {
                var apps = Process.GetProcessesByName("XPPenTablet");
                foreach (var app in apps) app.Dispose();
                if (apps.Length > 0) throw new IOException("XPPenTablet is running.");
                var candidates = await WindowsBleDiscovery.ScanAsync(end.Token);
                if (candidates.Length != 1)
                {
                    Console.WriteLine($"Eligible endpoints={candidates.Length}; scanning again.");
                    continue;
                }
                var device = await BluetoothLEDevice.FromIdAsync(candidates[0].Id).AsTask(end.Token);
                if (device is null) throw new IOException("Device unavailable.");
                using var link = new WindowsBleLink(device);
                using var session = CancellationTokenSource.CreateLinkedTokenSource(end.Token, link.ConnectionLost);
                try
                {
                    var services = await link.ServicesAsync(session.Token);
                    if (services.Count(s => s == BleReadOnlyDiagnostic.Control) != 1) throw new IOException("FFE0 unavailable or ambiguous.");
                    var identity = await link.FieldsAsync(BleReadOnlyDiagnostic.DeviceInfo, session.Token);
                    if (identity.Count(f => f.Id == BleReadOnlyDiagnostic.PnpId && f.Readable) != 1 ||
                        await link.ReadAsync(BleReadOnlyDiagnostic.DeviceInfo, BleReadOnlyDiagnostic.PnpId, session.Token)
                            is not [2, 0xbd, 0x28, 2, 2, _, _])
                        throw new IOException("ACK05 PnP ID mismatch or unavailable.");
                    Console.WriteLine("PnP confirmed 28BD:0202");
                    var fields = await link.FieldsAsync(BleReadOnlyDiagnostic.Control, session.Token);
                    using var trace = link.TraceControlSession();
                    foreach (var id in new[] { BleReadOnlyDiagnostic.Ack, BleReadOnlyDiagnostic.Reports })
                        if (fields.Count(f => f.Id == id && f.Signals) != 1) throw new IOException($"Missing notify channel {id}");
                    await capture.ObserveAsync(link, () =>
                    {
                        if (++sessions == 1) end.CancelAfter(TimeSpan.FromSeconds(seconds));
                        Console.WriteLine($"Session {sessions} ready {DateTimeOffset.UtcNow:O}; window={seconds}s; SharedReadOnly");
                    }, session.Token);
                }
                catch (OperationCanceledException) when (link.ConnectionLost.IsCancellationRequested && !end.IsCancellationRequested)
                {
                    losses++;
                    Console.WriteLine("Disconnected; rediscovering.");
                }
            }
        }
        catch (OperationCanceledException) when (end.IsCancellationRequested) { }
        catch (Exception e) { Console.Error.WriteLine(e.Message); return 3; }
        finally
        {
            Console.CancelKeyPress -= cancel;
            Console.WriteLine($"End {DateTimeOffset.UtcNow:O}; sessions={sessions} disconnects={losses} 0002={capture.AckCount} 0003={capture.ReportCount}");
        }
        return sessions == 0 ? 3 : 0;
    }
}
