using System.Security;
using LoipvRemote.Tools;

namespace LoipvRemote.Security
{
    public interface IKeyProvider
    {
        OptionalValue<SecureString> GetKey();
    }
}
