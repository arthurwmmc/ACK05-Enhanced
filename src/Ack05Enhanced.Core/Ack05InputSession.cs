namespace Ack05Enhanced.Core;

public sealed class Ack05InputSession
{
    private readonly Ack05TransitionTracker _tracker = new();
    private readonly Ack05LayerRouter _router;

    public Ack05InputSession(Ack05LayerRouter? router = null)
    {
        _router = router ?? new Ack05LayerRouter();
    }

    public int ActiveLayer => _router.ActiveLayer;

    public IReadOnlyList<LayerRoutedEvent> ProcessReport(ReadOnlySpan<byte> report)
    {
        return Ack05RawReportDecoder.TryDecode(report, out var snapshot, out _)
            ? ProcessSnapshot(snapshot)
            : Array.Empty<LayerRoutedEvent>();
    }

    public IReadOnlyList<LayerRoutedEvent> ProcessSnapshot(Ack05Snapshot snapshot)
    {
        var events = new List<LayerRoutedEvent>();
        foreach (var physicalEvent in _tracker.Apply(snapshot))
        {
            events.AddRange(_router.Route(physicalEvent));
        }

        return events;
    }

    public IReadOnlyList<LayerRoutedEvent> Reset()
    {
        var events = new List<LayerRoutedEvent>();
        foreach (var physicalEvent in _tracker.Reset())
        {
            events.AddRange(_router.Route(physicalEvent));
        }

        events.AddRange(_router.Reset());
        return events;
    }
}
