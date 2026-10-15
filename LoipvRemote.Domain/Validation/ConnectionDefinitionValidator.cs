using LoipvRemote.Domain.Connections;

namespace LoipvRemote.Domain.Validation;

public static class ConnectionDefinitionValidator
{
    public static void Validate(ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.Id == Guid.Empty)
            throw new ArgumentException("Connection ID is required.", nameof(definition));
        if (string.IsNullOrWhiteSpace(definition.Name))
            throw new ArgumentException("Connection name is required.", nameof(definition));
        if (definition.SortOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(definition), "Connection sort order cannot be negative.");
        definition.Credential.Validate();
        definition.GatewayCredential?.Validate();
        definition.Options?.Validate();
        if (string.IsNullOrWhiteSpace(definition.Host) && !AllowsBlankHost(definition.Protocol))
            throw new ArgumentException("Connection host is required.", nameof(definition));
        if (definition.Port is < 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(definition), "Connection port must be between 0 and 65535.");

        if (!Enum.IsDefined(definition.Protocol))
            throw new ArgumentOutOfRangeException(nameof(definition), "Connection protocol is not supported.");
    }

    private static bool AllowsBlankHost(ProtocolKind protocol) => false;
}
