using Ack05Enhanced.Core;

namespace Ack05Enhanced.App;

internal static class ConfigFileStore
{
    public static Ack05Configuration Load(string path) =>
        Ack05ConfigurationJson.Deserialize(File.ReadAllText(path));

    public static void SaveAtomic(string path, Ack05Configuration configuration)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Configuration path must include a directory.");
        }

        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        var backupPath = path + ".bak";
        File.WriteAllText(tempPath, Ack05ConfigurationJson.Serialize(configuration));

        try
        {
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
