namespace Ack05Enhanced.Core;

public static class Ack05RawReportDecoder
{
    public const byte VendorReportId = 0x02;
    public const byte RawPadMarker = 0xF0;
    public const int MinimumReportLength = 8;

    public static bool TryDecode(
        ReadOnlySpan<byte> report,
        out Ack05Snapshot snapshot,
        out string? error)
    {
        snapshot = default;

        if (report.Length < MinimumReportLength)
        {
            error = $"Report is {report.Length} bytes; at least {MinimumReportLength} are required.";
            return false;
        }

        if (report[0] != VendorReportId || report[1] != RawPadMarker)
        {
            error = $"Not an ACK05 raw pad report (expected 02-F0, got {report[0]:X2}-{report[1]:X2}).";
            return false;
        }

        var heldButtons = (ushort)(report[2] | ((report[3] & 0x03) << 8));
        var roller = report[7] switch
        {
            0x00 => RollerPulse.None,
            0x01 => RollerPulse.Clockwise,
            0x02 or 0xFF => RollerPulse.CounterClockwise,
            _ => RollerPulse.None,
        };

        snapshot = new Ack05Snapshot(
            (ushort)(heldButtons & Ack05Snapshot.ValidButtonMask),
            roller);
        error = null;
        return true;
    }
}
