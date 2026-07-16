using System;
using System.Runtime.Versioning;
using System.Xml.Linq;
using LoipvRemote.Security.SymmetricEncryption;

namespace LoipvRemote.Security.Factories
{
    [SupportedOSPlatform("windows")]
    public class CryptoProviderFactoryFromXml : ICryptoProviderFactory
    {
        private readonly XElement _element;

        public CryptoProviderFactoryFromXml(XElement element)
        {
            ArgumentNullException.ThrowIfNull(element);

            _element = element;
        }

        public ICryptographyProvider Build()
        {
            ICryptographyProvider cryptoProvider;
            try
            {
                if (!Enum.TryParse(_element.Attribute("EncryptionEngine")?.Value, ignoreCase: true, out BlockCipherEngines engine) ||
                    !Enum.TryParse(_element.Attribute("BlockCipherMode")?.Value, ignoreCase: true, out BlockCipherModes mode))
                    throw new FormatException("Invalid credential encryption settings.");
                cryptoProvider = new CryptoProviderFactory(engine, mode).Build();

                if (!int.TryParse(_element.Attribute("KdfIterations")?.Value, out int keyDerivationIterations))
                    throw new FormatException("Invalid credential KDF iteration count.");
                cryptoProvider.KeyDerivationIterations = keyDerivationIterations;
            }
            catch (Exception)
            {
                return new CryptoProviderFactoryFromSettings().Build();
            }

            return cryptoProvider;
        }
    }
}
