using LoipvRemote.Resources.Language;
using LoipvRemote.Tools;

namespace LoipvRemote.Connection
{
    public enum ExternalCredentialProvider
    {
        [LocalizedAttributes.LocalizedDescription(nameof(Language.ECPNone))]
        None = 0,

        [LocalizedAttributes.LocalizedDescription(nameof(Language.ECPDelineaSecretServer))]
        DelineaSecretServer = 1,

        [LocalizedAttributes.LocalizedDescription(nameof(Language.ECPClickstudiosPasswordstate))]
        ClickstudiosPasswordState = 2,

        [LocalizedAttributes.LocalizedDescription(nameof(Language.ECPOnePassword))]
        OnePassword = 3,

        [LocalizedAttributes.LocalizedDescription(nameof(Language.VaultOpenbao))]
        VaultOpenbao = 4,
    }
}
