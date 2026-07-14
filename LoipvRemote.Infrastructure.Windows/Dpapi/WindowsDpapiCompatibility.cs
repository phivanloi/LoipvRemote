using System.Security.Cryptography;
using System.Text;

namespace LoipvRemote.Infrastructure.Windows.Dpapi;

public static class WindowsDpapiCompatibility
{
    public static string UnprotectLocalMachineUnicode(string ciphertext)
    {
        ArgumentException.ThrowIfNullOrEmpty(ciphertext);
        byte[] protectedData = Convert.FromBase64String(ciphertext);
        byte[] plaintext = ProtectedData.Unprotect(protectedData, [], DataProtectionScope.LocalMachine);
        try
        {
            return Encoding.Unicode.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}
