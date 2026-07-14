using LoipvRemote.Domain.Credentials;

namespace LoipvRemote.Domain.Connections;

/// <summary>UI- and platform-independent connection definition.</summary>
public sealed record ConnectionDefinition(
    Guid Id,
    string Name,
    string Host,
    int Port,
    ProtocolKind Protocol,
    CredentialReference Credential,
    ExternalApplicationDefinition? ExternalApplication = null,
    Guid? ParentFolderId = null,
    int SortOrder = 0,
    ConnectionNodeOptions? Options = null,
    CredentialReference? GatewayCredential = null);
