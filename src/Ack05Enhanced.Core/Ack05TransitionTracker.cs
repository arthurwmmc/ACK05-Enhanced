namespace Ack05Enhanced.Core;

public sealed class Ack05TransitionTracker
{
    private ushort _previousHeldButtons;

    public ushort HeldButtons => _previousHeldButtons;

    public IReadOnlyList<PhysicalInputEvent> Apply(Ack05Snapshot snapshot)
    {
        var events = new List<PhysicalInputEvent>();
        var changed = (ushort)(_previousHeldButtons ^ snapshot.HeldButtons);

        var buttons = Enum.GetValues<Ack05Button>();
        for (var index = 0; index < buttons.Length; index++)
        {
            var bit = (ushort)(1 << index);
            if ((changed & bit) == 0)
            {
                continue;
            }

            var type = (snapshot.HeldButtons & bit) != 0
                ? PhysicalEventType.Down
                : PhysicalEventType.Up;
            var control = buttons[index] == Ack05Button.RollerPress
                ? Ack05Control.RollerPress
                : (Ack05Control)index;
            events.Add(new PhysicalInputEvent(control, type));
        }

        if (snapshot.Roller == RollerPulse.Clockwise)
        {
            events.Add(new PhysicalInputEvent(
                Ack05Control.RollerClockwise,
                PhysicalEventType.Pulse));
        }
        else if (snapshot.Roller == RollerPulse.CounterClockwise)
        {
            events.Add(new PhysicalInputEvent(
                Ack05Control.RollerCounterClockwise,
                PhysicalEventType.Pulse));
        }

        _previousHeldButtons = snapshot.HeldButtons;
        return events;
    }

    public IReadOnlyList<PhysicalInputEvent> Reset()
    {
        return Apply(new Ack05Snapshot(0, RollerPulse.None));
    }
}
