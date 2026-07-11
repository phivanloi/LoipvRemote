using LoipvRemote.Security.SymmetricEncryption;
using System.Runtime.Versioning;

namespace LoipvRemote.Security.Factories
{
    [SupportedOSPlatform("windows")]
    public class LegacyInsecureCryptoProviderFactory : ICryptoProviderFactory
    {
        public ICryptographyProvider Build()
        {
            return new LegacyRijndaelCryptographyProvider();
        }
    }
}