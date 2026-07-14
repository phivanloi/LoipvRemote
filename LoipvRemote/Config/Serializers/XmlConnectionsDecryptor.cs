using System;
using System.Runtime.Versioning;
using System.Security;
using LoipvRemote.Security;
using LoipvRemote.Security.Authentication;
using LoipvRemote.Security.Factories;
using LoipvRemote.Security.SymmetricEncryption;
using LoipvRemote.Tools;
using LoipvRemote.Tree.Root;

namespace LoipvRemote.Config.Serializers
{
    [SupportedOSPlatform("windows")]
    public class XmlConnectionsDecryptor
    {
        private readonly ICryptographyProvider _cryptographyProvider;
        private readonly RootNodeInfo _rootNodeInfo;

        public Func<Optional<SecureString>> AuthenticationRequestor { get; set; }

        public int KeyDerivationIterations
        {
            get { return _cryptographyProvider.KeyDerivationIterations; }
            set { _cryptographyProvider.KeyDerivationIterations = value; }
        }


        public XmlConnectionsDecryptor(BlockCipherEngines blockCipherEngine, BlockCipherModes blockCipherMode, RootNodeInfo rootNodeInfo)
        {
            _cryptographyProvider = new CryptoProviderFactory(blockCipherEngine, blockCipherMode).Build();
            _rootNodeInfo = rootNodeInfo;
        }

        public string Decrypt(string plainText)
        {
            return plainText == ""
                ? ""
                : _cryptographyProvider.Decrypt(plainText, _rootNodeInfo.PasswordString.ConvertToSecureString());
        }

        public bool ConnectionsFileIsAuthentic(string protectedString, SecureString password)
        {
            if (string.IsNullOrEmpty(protectedString))
                return password is null || password.Length == 0;

            bool connectionsFileIsAuthentic = false;
            try
            {
                connectionsFileIsAuthentic = _cryptographyProvider.Decrypt(
                    protectedString,
                    _rootNodeInfo.PasswordString.ConvertToSecureString()) == "ThisIsProtected";
            }
            catch (Exception ex) when (ex is EncryptionException or ArgumentException)
            {
            }

            return connectionsFileIsAuthentic ||
                   (AuthenticationRequestor is not null && Authenticate(protectedString, _rootNodeInfo.PasswordString.ConvertToSecureString()));
        }

        private bool Authenticate(string cipherText, SecureString password)
        {
            PasswordAuthenticator authenticator = new(_cryptographyProvider, cipherText, AuthenticationRequestor);
            bool authenticated = authenticator.Authenticate(password);

            if (!authenticated)
                return false;

            // A successful Authenticate() guarantees LastAuthenticatedPassword is set.
            _rootNodeInfo.PasswordString = authenticator.LastAuthenticatedPassword!.ConvertToUnsecureString();
            return true;
        }
    }
}
