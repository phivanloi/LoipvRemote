namespace LoipvRemote.Protocols.Abstractions;

/// <summary>Stores non-secret connector settings outside connector implementations.</summary>
public interface IExternalCredentialSettingsStore
{
    string? GetString(string scope, string name);

    bool GetBoolean(string scope, string name, bool defaultValue = false);

    void SetString(string scope, string name, string value);

    void SetBoolean(string scope, string name, bool value);
}
