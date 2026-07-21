namespace Ack05Enhanced.Windows;

public static class Ack05HidCommands
{
    public static async Task EnableBitmaskModeAsync(
        FileStream stream,
        HidDeviceInfo device,
        CancellationToken cancellationToken)
    {
        var outputLength = device.OutputReportLength >= 10
            ? device.OutputReportLength
            : 32;
        var command = new byte[outputLength];
        command[0] = 0x02;
        command[1] = 0xB0;
        command[2] = 0x04;

        await stream.WriteAsync(command, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}
