using System.Security;

namespace LoipvRemote.Security.Authentication
{
    public interface IAuthenticator
    {
        bool Authenticate(SecureString password);
    }
}