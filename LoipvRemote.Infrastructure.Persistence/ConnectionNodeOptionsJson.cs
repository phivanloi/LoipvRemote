using System.Text.Json;
using LoipvRemote.Domain.Connections;

namespace LoipvRemote.Infrastructure.Persistence;

internal static class ConnectionNodeOptionsJson
{
    public static string? Serialize(ConnectionNodeOptions? options) =>
        options is null ? null : JsonSerializer.Serialize(options);

    public static ConnectionNodeOptions? Deserialize(string? value)
    {
        if (value is null)
            return null;

        try
        {
            ConnectionNodeOptions? options = JsonSerializer.Deserialize<ConnectionNodeOptions>(value);
            if (options is null)
                throw new InvalidDataException("Connection options payload is invalid.");
            options.Validate();
            return options;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Connection options payload is invalid.", exception);
        }
    }
}
