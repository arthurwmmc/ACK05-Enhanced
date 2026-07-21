using Ack05Enhanced.Core;
using System.Security.Cryptography;
using System.Text.Json;

namespace Ack05Enhanced.App;

internal sealed record RecoveryDraft(
    string DraftJson,
    string SavedBaselineJson,
    string? SourcePath,
    string? SavedFileFingerprint,
    DateTimeOffset UpdatedAtUtc);

internal static class RecoveryDraftStore
{
    private const string DirectoryName = "ACK05 Enhanced";
    private const string FileName = "recovery-draft.json";
    private static readonly object SyncRoot = new();

    internal static string PathOnDisk => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        DirectoryName,
        FileName);

    internal static RecoveryDraft? TryLoad(out string? error)
    {
        error = null;
        if (!File.Exists(PathOnDisk))
        {
            return null;
        }

        try
        {
            var draft = JsonSerializer.Deserialize<RecoveryDraft>(File.ReadAllText(PathOnDisk))
                ?? throw new InvalidDataException("Recovery draft is empty.");
            _ = Ack05ConfigurationJson.Deserialize(draft.DraftJson);
            _ = Ack05ConfigurationJson.Deserialize(draft.SavedBaselineJson);
            return draft;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            QuarantineCorruptDraft();
            return null;
        }
    }

    internal static void Save(RecoveryDraft draft, Func<bool>? shouldSave = null)
    {
        lock (SyncRoot)
        {
            if (shouldSave?.Invoke() == false)
            {
                return;
            }

            var directory = Path.GetDirectoryName(PathOnDisk)
                ?? throw new InvalidOperationException("Recovery draft directory is unavailable.");
            Directory.CreateDirectory(directory);
            var temporaryPath = Path.Combine(directory, $".{FileName}.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(draft, new JsonSerializerOptions
            {
                WriteIndented = true,
            }));

            try
            {
                File.Move(temporaryPath, PathOnDisk, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }

    internal static void Delete()
    {
        lock (SyncRoot)
        {
            if (File.Exists(PathOnDisk))
            {
                File.Delete(PathOnDisk);
            }
        }
    }

    internal static string? Fingerprint(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static void QuarantineCorruptDraft()
    {
        try
        {
            var directory = Path.GetDirectoryName(PathOnDisk);
            if (string.IsNullOrWhiteSpace(directory) || !File.Exists(PathOnDisk))
            {
                return;
            }

            var quarantine = Path.Combine(
                directory,
                $"recovery-draft.corrupt-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json");
            File.Move(PathOnDisk, quarantine, overwrite: true);
        }
        catch
        {
            // A damaged recovery file must not prevent the editor from starting.
        }
    }
}
