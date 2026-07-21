using Ack05Enhanced.Core;

namespace Ack05Enhanced.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var initialPath = args.Length > 0 ? args[0] : null;
        var initialConfiguration = Ack05Configuration.CreateDefault();
        string? savedBaselineJson = null;
        string? sourceFingerprint = null;
        if (!string.IsNullOrWhiteSpace(initialPath) && File.Exists(initialPath))
        {
            try
            {
                initialConfiguration = Ack05ConfigurationJson.Deserialize(File.ReadAllText(initialPath));
                sourceFingerprint = RecoveryDraftStore.Fingerprint(initialPath);
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"Could not open {Path.GetFileName(initialPath)}. A default configuration will be used.\n\n{exception.Message}",
                    "ACK05 configuration load failed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                initialPath = null;
            }
        }

        var recovery = RecoveryDraftStore.TryLoad(out var recoveryError);
        if (!string.IsNullOrWhiteSpace(recoveryError))
        {
            MessageBox.Show(
                $"The recovery draft was damaged and has been quarantined. The saved configuration will be used.\n\n{recoveryError}",
                "ACK05 draft recovery",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        else if (recovery is not null)
        {
            var source = string.IsNullOrWhiteSpace(recovery.SourcePath)
                ? "an unsaved configuration"
                : Path.GetFileName(recovery.SourcePath);
            var choice = MessageBox.Show(
                $"A working draft from {recovery.UpdatedAtUtc.ToLocalTime():g} was found for {source}.\n\n" +
                "Choose Yes to restore the draft, or No to discard it and use the saved configuration.",
                "Restore working draft?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (choice == DialogResult.Yes)
            {
                initialConfiguration = Ack05ConfigurationJson.Deserialize(recovery.DraftJson);
                initialPath = recovery.SourcePath;
                savedBaselineJson = recovery.SavedBaselineJson;
                sourceFingerprint = recovery.SavedFileFingerprint;
            }
            else
            {
                RecoveryDraftStore.Delete();
            }
        }

        Application.Run(new KeymapEditorForm(
            initialConfiguration,
            initialPath,
            savedBaselineJson,
            sourceFingerprint));
    }
}
