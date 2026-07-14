namespace LoipvRemote.UseCases.Credentials;

public interface IStringSecretStore
{
    string Protect(string plaintext, string purpose);

    string Unprotect(string protectedValue, string purpose);
}
