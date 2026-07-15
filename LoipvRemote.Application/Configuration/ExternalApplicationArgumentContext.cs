namespace LoipvRemote.ApplicationServices.Configuration;

/// <summary>Values available to external-tool argument expansion.</summary>
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
