namespace Ack05Enhanced.Windows;

public sealed record HidDeviceInfo(
    string Path,
    ushort VendorId,
    ushort ProductId,
    ushort UsagePage,
    ushort Usage,
    ushort InputReportLength,
    ushort OutputReportLength,
    string? Manufacturer,
    string? Product)
{
    public bool IsAck05 => VendorId == 0x28BD && ProductId == 0x0202;

    public bool IsAck05VendorCollection =>
        IsAck05 && UsagePage == 0xFF0A && Usage == 0x0001;
}
