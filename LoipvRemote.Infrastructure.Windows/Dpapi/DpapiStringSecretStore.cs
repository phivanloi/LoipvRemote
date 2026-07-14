using System.Security.Cryptography;
using System.Text;
using LoipvRemote.UseCases.Credentials;

namespace LoipvRemote.Infrastructure.Windows.Dpapi;

/// <summary>
/// Stores UTF-8 settings values with versioned, current-user DPAPI protection.
/// </summary>
public sealed class DpapiStringSecretStore(IUserSecretProtector protector) : IStringSecretStore
{
    private const string Prefix = "dpapi:v1:";
    private readonly IUserSecretProtector _protector = protector ?? throw new ArgumentNullException(nameof(protector));

    public string Protect(string plaintext, string purpose)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        if (plaintext.Length == 0)
            return string.Empty;

        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        try
        {
            byte[] protectedBytes = _protector.Protect(plaintextBytes, purpose);
            try
            {
                return Prefix + Convert.ToBase64String(protectedBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    public string Unprotect(string protectedValue, string purpose)
    {
        ArgumentNullException.ThrowIfNull(protectedValue);

        if (protectedValue.Length == 0)
            return string.Empty;
        if (!protectedValue.StartsWith(Prefix, StringComparison.Ordinal))
            throw new FormatException("The secret does not use the supported DPAPI format.");

        byte[] protectedBytes = Convert.FromBase64String(protectedValue[Prefix.Length..]);
        try
        {
            byte[] plaintextBytes = _protector.Unprotect(protectedBytes, purpose);
            try
            {
                return Encoding.UTF8.GetString(plaintextBytes);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintextBytes);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedBytes);
        }
    }
}
