using System.Security.Cryptography;
using System.Text;
using LoipvRemote.UseCases.Credentials;

namespace LoipvRemote.Infrastructure.Windows.Dpapi;

/// <summary>DPAPI implementation bound to the current Windows user and an explicit non-secret purpose.</summary>
public sealed class WindowsDpapiSecretProtector : IUserSecretProtector
{
    private const string EntropyPrefix = "LoipvRemote.DPAPI.v1:";

    public byte[] Protect(byte[] plaintext, string purpose)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        byte[] entropy = CreateEntropy(purpose);
        try
        {
            return ProtectedData.Protect(plaintext, entropy, DataProtectionScope.CurrentUser);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(entropy);
        }
    }

    public byte[] Unprotect(byte[] protectedData, string purpose)
    {
        ArgumentNullException.ThrowIfNull(protectedData);
        byte[] entropy = CreateEntropy(purpose);
        try
        {
            return ProtectedData.Unprotect(protectedData, entropy, DataProtectionScope.CurrentUser);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(entropy);
        }
    }

    private static byte[] CreateEntropy(string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        return SHA256.HashData(Encoding.UTF8.GetBytes(EntropyPrefix + purpose));
    }
}
