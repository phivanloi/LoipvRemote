using System.Security;

namespace LoipvRemote.Config.Serializers
{
    public interface ISecureSerializer<in TIn, out TOut>
    {
        TOut Serialize(TIn model, SecureString key);
    }
}