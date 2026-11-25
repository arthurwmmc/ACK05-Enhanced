using Ack05Enhanced.BleProbe;
using Ack05Enhanced.Windows;
using Windows.Devices.Bluetooth;

if (args is ["--p08-single-write"])
    return await SingleActivationTrial.RunAsync();

if (args.FirstOrDefault() == "--capture")
{
    var duration = 60;
    if (args.Length > 2 || (args.Length == 2 && (!int.TryParse(args[1], out duration) || duration is < 1 or > 600)))
        return 2;
    return await NotificationTrial.RunAsync(duration);
}

if (args is ["--inspect-ffe0", var mode] && mode is "ro" or "rw")
    return await Ffe0Inspection.RunAsync(mode == "rw");

var seconds = 45;
if (args.Length != 0 &&
    (args.Length != 2 || args[0] != "--seconds" || !int.TryParse(args[1], out seconds) || seconds is < 5 or > 600))
{
    Console.Error.WriteLine("Usage: Ack05Enhanced.BleProbe [--seconds 5..600]");
    return 2;
}

using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
var token = deadline.Token;
Console.CancelKeyPress += (_, e) => { e.Cancel = true; deadline.Cancel(); };
var sessions = 0;
var reports = 0;
var confirmed = false;
ulong? previousAddress = null;
static void Log(string message) => Console.WriteLine($"{DateTimeOffset.UtcNow:HH:mm:ss.fff}Z {message}");
try
{
    var adapter = await BluetoothAdapter.GetDefaultAsync().AsTask(token);
    if (adapter is null || !adapter.IsCentralRoleSupported)
    {
        Log("No available Windows BLE central adapter.");
        return 3;
    }
    while (!token.IsCancellationRequested)
    {
        try
        {
            var candidates = await WindowsBleDiscovery.ScanAsync(token);
            Log($"Discovery: {candidates.Length} eligible Shortcut Remote endpoint(s).");
            if (candidates.Length == 1)
            {
                var candidate = candidates[0];
                Log($"Opening {candidate.Name} (Windows connected={candidate.Connected}).");
                var device = await BluetoothLEDevice.FromIdAsync(candidate.Id).AsTask(token);
                if (device is null) throw new IOException("Windows did not grant an endpoint handle.");
                using var link = new WindowsBleLink(device);
                Log(previousAddress is null ? "First Windows-reported address in this run."
                    : previousAddress == link.Address ? "Windows-reported address unchanged."
                    : "Windows-reported address changed between named candidates; identity not proven.");
                previousAddress = link.Address;
                using var observation = CancellationTokenSource.CreateLinkedTokenSource(token, link.ConnectionLost);
                try
                {
                    confirmed |= await BleReadOnlyDiagnostic.InspectAsync(link, Log, (id, data) =>
                    {
                        Interlocked.Increment(ref reports);
                        Log(BleReadOnlyDiagnostic.Describe(id, data));
                    }, observation.Token);
                    sessions++;
                    Log("Inventory finished; observing the link until disconnect or deadline.");
                    await Task.Delay(Timeout.InfiniteTimeSpan, observation.Token);
                }
                catch (OperationCanceledException) when (link.ConnectionLost.IsCancellationRequested && !token.IsCancellationRequested)
                {
                    Log("Disconnected; discarding this session and rediscovering by name.");
                }
            }
            else if (candidates.Length > 1) Log("Ambiguous identity; no connection attempted.");
        }
        catch (IOException e) { Log(e.Message); }
        catch (UnauthorizedAccessException) { Log("Windows denied Bluetooth access."); break; }
        catch (System.Runtime.InteropServices.COMException e) { Log($"Windows BLE error 0x{e.HResult:X8}."); }
        await Task.Delay(TimeSpan.FromSeconds(2), token);
    }
}
catch (OperationCanceledException) when (token.IsCancellationRequested) { }
catch (Exception e)
{
    Log($"Diagnostic interrupted: {e.GetType().Name} (0x{e.HResult:X8}).");
    return 3;
}
finally { Log($"Inventory attempts completed={sessions}; FFE0 characteristic set verified={confirmed}; notifications={reports}."); }
return confirmed ? 0 : 3;
