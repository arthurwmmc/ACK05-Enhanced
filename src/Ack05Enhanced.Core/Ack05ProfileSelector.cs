namespace Ack05Enhanced.Core;

public sealed class Ack05ProfileSelector
{
    private readonly Ack05Configuration _configuration;
    private readonly Ack05Profile _defaultProfile;
    private readonly Dictionary<string, Ack05Profile> _applicationProfiles;

    public Ack05ProfileSelector(Ack05Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Ack05ConfigurationValidator.ValidateOrThrow(configuration);
        _configuration = configuration;
        _defaultProfile = configuration.Profiles.Single(profile =>
            string.Equals(
                profile.Id,
                configuration.DefaultProfileId,
                StringComparison.OrdinalIgnoreCase));
        _applicationProfiles = configuration.Profiles
            .Where(profile => profile.ApplicationMatch?.ProcessName is not null)
            .ToDictionary(
                profile => NormalizeProcessName(profile.ApplicationMatch!.ProcessName!),
                StringComparer.OrdinalIgnoreCase);
    }

    public Ack05Configuration Configuration => _configuration;

    public Ack05Profile Select(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return _defaultProfile;
        }

        return _applicationProfiles.TryGetValue(
            NormalizeProcessName(processName),
            out var profile)
            ? profile
            : _defaultProfile;
    }

    internal static string NormalizeProcessName(string processName)
    {
        var normalized = processName.Trim();
        return normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^4]
            : normalized;
    }
}
