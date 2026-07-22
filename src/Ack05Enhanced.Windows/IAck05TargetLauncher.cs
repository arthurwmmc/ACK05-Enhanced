using System.Diagnostics;

namespace Ack05Enhanced.Windows;

public interface IAck05TargetLauncher
{
    void Open(string target);
}

public sealed class ShellAck05TargetLauncher : IAck05TargetLauncher
{
    public void Open(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        var expandedTarget = Environment.ExpandEnvironmentVariables(target);
        Process.Start(new ProcessStartInfo
        {
            FileName = expandedTarget,
            UseShellExecute = true,
            Verb = "open",
        });
    }
}
