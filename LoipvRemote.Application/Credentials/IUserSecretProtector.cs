namespace LoipvRemote.Application.Credentials;

/// <summary>Application port for protecting local secret bytes without exposing storage or platform details.</summary>
public interface IUserSecretProtector
{
    byte[] Protect(byte[] plaintext, string purpose);
    byte[] Unprotect(byte[] protectedData, string purpose);
}
