using Ack05Enhanced.Core;

namespace Ack05Enhanced.Windows;

/// <summary>
/// Applies logical output through SendInput and keeps a second safety registry.
/// </summary>
public sealed class SendInputAck05Output : IDisposable
{
    private readonly IAck05WindowsInputInjector _injector;
    private readonly HashSet<Ack05KeyboardKey> _pressedKeys = new();
    private readonly List<Ack05KeyboardKey> _pressOrder = new();
    private bool _disposed;

    public SendInputAck05Output()
        : this(new User32WindowsInputInjector())
    {
    }

    public SendInputAck05Output(IAck05WindowsInputInjector injector)
    {
        _injector = injector ?? throw new ArgumentNullException(nameof(injector));
    }

    public IReadOnlyCollection<Ack05KeyboardKey> PressedKeys => _pressOrder.AsReadOnly();

    public void Apply(Ack05OutputEvent output)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        switch (output.Kind)
        {
            case Ack05OutputEventKind.KeyDown when output.Key.HasValue:
                KeyDown(output.Key.Value);
                break;
            case Ack05OutputEventKind.KeyUp when output.Key.HasValue:
                KeyUp(output.Key.Value);
                break;
            case Ack05OutputEventKind.MouseWheel when output.WheelSteps != 0:
                _injector.MouseWheel(output.WheelSteps);
                break;
            default:
                throw new ArgumentException("Malformed logical output event.", nameof(output));
        }
    }

    public void Apply(IEnumerable<Ack05OutputEvent> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (var item in output)
        {
            Apply(item);
        }
    }

    public void ReleaseAll()
    {
        var failures = new List<Exception>();
        for (var index = _pressOrder.Count - 1; index >= 0; index--)
        {
            var key = _pressOrder[index];
            try
            {
                _injector.KeyUp(key);
                _pressedKeys.Remove(key);
                _pressOrder.RemoveAt(index);
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException("One or more synthetic keys could not be released.", failures);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            ReleaseAll();
        }
        finally
        {
            _disposed = true;
        }
    }

    private void KeyDown(Ack05KeyboardKey key)
    {
        if (_pressedKeys.Contains(key))
        {
            return;
        }

        _injector.KeyDown(key);
        _pressedKeys.Add(key);
        _pressOrder.Add(key);
    }

    private void KeyUp(Ack05KeyboardKey key)
    {
        if (!_pressedKeys.Contains(key))
        {
            return;
        }

        _injector.KeyUp(key);
        _pressedKeys.Remove(key);
        _pressOrder.Remove(key);
    }
}
