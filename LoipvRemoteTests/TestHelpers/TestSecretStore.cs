using LoipvRemote.UseCases.Credentials;

namespace LoipvRemoteTests.TestHelpers;

public sealed class TestSecretStore : IStringSecretStore
{
    public static TestSecretStore Instance { get; } = new();

    private TestSecretStore()
    {
    }

    public string Protect(string plaintext, string purpose) => plaintext;

    public string Unprotect(string protectedValue, string purpose) => protectedValue;
}
