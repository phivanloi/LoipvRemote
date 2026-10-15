using LoipvRemote.Domain.Connections;

namespace LoipvRemote.Application.Configuration;

/// <summary>
/// A deliberately portable connection export. Credential values are plaintext
/// only while serializing the export file selected by the user.
/// </summary>
public sealed record ConnectionExportPackage(
    ConnectionTreeDefinition Tree,
    IReadOnlyDictionary<Guid, PortableConnectionCredential> Credentials);

/// <summary>Credentials associated with one exported connection.</summary>
public sealed record PortableConnectionCredential(
    string UserName,
    string Password,
    string GatewayPassword = "");
