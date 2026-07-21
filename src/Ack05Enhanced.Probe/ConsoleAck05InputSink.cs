using Ack05Enhanced.Core;

namespace Ack05Enhanced.Probe;

internal sealed class ConsoleAck05InputSink : IAck05InputSink
{
    public ValueTask OnConnectionStatusAsync(
        Ack05ConnectionStatus status,
        CancellationToken cancellationToken)
    {
        var line = status.State switch
        {
            Ack05ConnectionState.WaitingForDevice when status.Message is null =>
                "Waiting for ACK05 vendor collection...",
            Ack05ConnectionState.WaitingForDevice =>
                $"Waiting for ACK05: {status.Message}",
            Ack05ConnectionState.Connected =>
                "ACK05 connected.",
            Ack05ConnectionState.Disconnected =>
                $"ACK05 disconnected: {status.Message}",
            Ack05ConnectionState.Stopped =>
                "ACK05 input service stopped.",
            _ => status.State.ToString(),
        };

        Console.WriteLine(line);
        return ValueTask.CompletedTask;
    }

    public ValueTask OnInputAsync(
        LayerRoutedEvent input,
        CancellationToken cancellationToken)
    {
        if (input.Kind == LayerRouteKind.LayerChanged)
        {
            Console.WriteLine($"layer -> {input.Layer}");
        }
        else
        {
            Console.WriteLine($"L{input.Layer} {input.Control} {input.InputType}");
        }

        return ValueTask.CompletedTask;
    }
}
