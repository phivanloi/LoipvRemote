namespace LoipvRemote.Connectors.Abstractions;

/// <summary>Secret material returned by an external credential provider.</summary>
public sealed record ExternalCredential(string Username, string Password, string Domain, string PrivateKey);
