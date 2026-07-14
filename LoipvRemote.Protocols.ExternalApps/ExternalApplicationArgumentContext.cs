namespace LoipvRemote.Protocols.ExternalApps;

/// <summary>
/// Values available when expanding a configured external application's arguments.
/// Values must already include any credential fallback selected by the caller.
/// </summary>
public sealed record ExternalApplicationArgumentContext(
    string? Name,
    string? Hostname,
    int Port,
    string? Username,
    string? Password,
    string? Domain,
    string? Description,
    string? MacAddress,
    string? UserField);
