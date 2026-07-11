using LoipvRemote.Resources.Language;
using LoipvRemote.Tools;

namespace LoipvRemote.Connection
{
    public enum ExternalAddressProvider
    {
        [LocalizedAttributes.LocalizedDescription(nameof(Language.EAPNone))]
        None = 0,

        [LocalizedAttributes.LocalizedDescription(nameof(Language.EAPAmazonWebServices))]
        AmazonWebServices = 1
    }
}
