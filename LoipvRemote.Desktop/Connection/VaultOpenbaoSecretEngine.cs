using LoipvRemote.Resources.Language;
using LoipvRemote.Tools;

namespace LoipvRemote.Connection
{
    public enum VaultOpenbaoSecretEngine
    {
        [LocalizedAttributes.LocalizedDescription(nameof(Language.VaultOpenbaoSecretEngineKeyValue))]
        Kv = 0,

        [LocalizedAttributes.LocalizedDescription(nameof(Language.VaultOpenbaoSecretEngineLDAPDynamic))]
        LdapDynamic = 1,

        [LocalizedAttributes.LocalizedDescription(nameof(Language.VaultOpenbaoSecretEngineLDAPStatic))]
        LdapStatic = 2,

        [LocalizedAttributes.LocalizedDescription(nameof(Language.VaultOpenbaoSecretEngineSSHOTP))]
        SSHOTP = 3,
    }
}
