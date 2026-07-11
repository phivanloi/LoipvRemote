using System.Security;
using LoipvRemote.Tools;

namespace LoipvRemote.Security
{
    public interface IKeyProvider
    {
        Optional<SecureString> GetKey();
    }
}