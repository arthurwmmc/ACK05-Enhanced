namespace Ack05Enhanced.Core;

public enum Ack05OutputEventKind
{
    KeyDown,
    KeyUp,
    MouseWheel,
}

public readonly record struct Ack05OutputEvent(
    Ack05OutputEventKind Kind,
    Ack05KeyboardKey? Key = null,
    int WheelSteps = 0)
{
    public static Ack05OutputEvent KeyDown(Ack05KeyboardKey key) =>
        new(Ack05OutputEventKind.KeyDown, key);

    public static Ack05OutputEvent KeyUp(Ack05KeyboardKey key) =>
        new(Ack05OutputEventKind.KeyUp, key);

    public static Ack05OutputEvent MouseWheel(int steps) =>
        new(Ack05OutputEventKind.MouseWheel, WheelSteps: steps);
}
