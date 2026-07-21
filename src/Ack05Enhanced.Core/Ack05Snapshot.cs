namespace Ack05Enhanced.Core;

public readonly record struct Ack05Snapshot(ushort HeldButtons, RollerPulse Roller)
{
    public const ushort ValidButtonMask = 0x03FF;

    public bool IsHeld(Ack05Button button)
    {
        return (HeldButtons & (1 << (int)button)) != 0;
    }
}
