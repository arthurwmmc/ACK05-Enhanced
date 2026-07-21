namespace Ack05Enhanced.Core;

public enum PhysicalEventType
{
    Down,
    Up,
    Pulse,
}

public readonly record struct PhysicalInputEvent(
    Ack05Control Control,
    PhysicalEventType Type);
