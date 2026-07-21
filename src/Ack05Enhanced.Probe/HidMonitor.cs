using Ack05Enhanced.Core;
using Ack05Enhanced.Windows;

namespace Ack05Enhanced.Probe;

internal static class HidMonitor
{
    internal static async Task RunAsync(
        HidDeviceInfo device,
        bool enableBitmask,
        CancellationToken cancellationToken)
    {
        var handle = HidDiscovery.OpenForMonitoring(device, enableBitmask);
        await using var stream = new FileStream(
            handle,
            enableBitmask ? FileAccess.ReadWrite : FileAccess.Read,
            Math.Max(64, (int)device.InputReportLength),
            isAsync: true);

        if (enableBitmask)
        {
            await Ack05HidCommands.EnableBitmaskModeAsync(
                stream,
                device,
                cancellationToken);
            Console.WriteLine("Known ACK05 raw-mode command sent once.");
        }
        else
        {
            Console.WriteLine("Read-only monitor: no command was sent to the device.");
        }

        Console.WriteLine("Press Ctrl+C to stop.");
        var buffer = new byte[Math.Max(64, (int)device.InputReportLength)];
        var tracker = new Ack05TransitionTracker();
        var router = new Ack05LayerRouter();

        while (!cancellationToken.IsCancellationRequested)
        {
            var count = await stream.ReadAsync(buffer, cancellationToken);
            if (count == 0)
            {
                throw new EndOfStreamException("ACK05 returned an empty read; it may have disconnected.");
            }

            ProcessReport(buffer, count, tracker, router);
        }
    }

    private static void ProcessReport(
        byte[] buffer,
        int count,
        Ack05TransitionTracker tracker,
        Ack05LayerRouter router)
    {
        var report = buffer.AsSpan(0, count);
        Console.WriteLine($"raw {DateTimeOffset.Now:HH:mm:ss.fff} {Convert.ToHexString(report)}");

        if (!Ack05RawReportDecoder.TryDecode(report, out var snapshot, out _))
        {
            return;
        }

        foreach (var physicalEvent in tracker.Apply(snapshot))
        {
            foreach (var routedEvent in router.Route(physicalEvent))
            {
                if (routedEvent.Kind == LayerRouteKind.LayerChanged)
                {
                    Console.WriteLine($"  layer -> {routedEvent.Layer}");
                }
                else
                {
                    Console.WriteLine(
                        $"  L{routedEvent.Layer} {routedEvent.Control} {routedEvent.InputType}");
                }
            }
        }
    }
}
