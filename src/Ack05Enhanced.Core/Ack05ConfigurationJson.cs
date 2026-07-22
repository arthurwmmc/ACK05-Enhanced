using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ack05Enhanced.Core;

public static class Ack05ConfigurationJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static Ack05Configuration Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            var configuration = JsonSerializer.Deserialize<Ack05Configuration>(json, Options);
            if (configuration?.SchemaVersion == 1)
            {
                configuration = Ack05ConfigurationMigrator.MigrateV1(configuration);
            }
            else if (configuration?.SchemaVersion == 2)
            {
                configuration = Ack05ConfigurationMigrator.MigrateV2(configuration);
            }
            else if (configuration?.SchemaVersion == 3)
            {
                configuration = Ack05ConfigurationMigrator.MigrateV3(configuration);
            }

            Ack05ConfigurationValidator.ValidateOrThrow(configuration);
            return configuration!;
        }
        catch (Ack05ConfigurationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new Ack05ConfigurationException(
                $"Configuration JSON is invalid: {exception.Message}",
                exception);
        }
    }

    public static string Serialize(Ack05Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        Ack05ConfigurationValidator.ValidateOrThrow(configuration);
        return JsonSerializer.Serialize(configuration, Options) + Environment.NewLine;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        };
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
