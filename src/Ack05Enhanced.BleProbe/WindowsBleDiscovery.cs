using Ack05Enhanced.Windows;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Ack05Enhanced.BleProbe;

internal static class WindowsBleDiscovery
{
    public static async Task<BleCandidate[]> ScanAsync(CancellationToken token)
    {
        const string connected = "System.Devices.Aep.IsConnected";
        var query = string.Join(" OR ", new[] { "Shortcut Remote", "ShortcutRemote" }
            .Select(name => $"({BluetoothLEDevice.GetDeviceSelectorFromDeviceName(name)})"));
        var watcher = DeviceInformation.CreateWatcher(query, [connected],
            DeviceInformationKind.AssociationEndpoint);
        var found = new Dictionary<string, DeviceInformation>();
        void Added(DeviceWatcher _, DeviceInformation d) { lock (found) found[d.Id] = d; }
        void Updated(DeviceWatcher _, DeviceInformationUpdate d)
        {
            lock (found)
                if (found.TryGetValue(d.Id, out var info)) info.Update(d);
        }
        void Removed(DeviceWatcher _, DeviceInformationUpdate d) { lock (found) found.Remove(d.Id); }
        watcher.Added += Added;
        watcher.Updated += Updated;
        watcher.Removed += Removed;
        try
        {
            watcher.Start();
            await Task.Delay(TimeSpan.FromSeconds(4), token);
            if (watcher.Status == DeviceWatcherStatus.Aborted)
                throw new IOException("Windows endpoint discovery aborted.");
            lock (found)
                return BleReadOnlyDiagnostic.SelectCandidates(found.Values.Select(d => new BleCandidate(
                    d.Id, d.Name, d.Properties.TryGetValue(connected, out var state) && state is true)));
        }
        finally
        {
            if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
                watcher.Stop();
            watcher.Added -= Added;
            watcher.Updated -= Updated;
            watcher.Removed -= Removed;
        }
    }
}
