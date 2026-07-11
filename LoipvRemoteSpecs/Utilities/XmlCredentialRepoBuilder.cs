using System.Security;
using LoipvRemote.Config;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers.CredentialSerializer;
using LoipvRemote.Credential;
using LoipvRemote.Credential.Repositories;
using LoipvRemote.Security;
using LoipvRemote.Security.SymmetricEncryption;

namespace LoipvRemoteSpecs.Utilities
{
    public class XmlCredentialRepoBuilder
    {
        public SecureString EncryptionKey { get; set; } = "someKey1".ConvertToSecureString();
        public ICryptographyProvider CryptographyProvider { get; set; } = new AeadCryptographyProvider();

        public ICredentialRepository BuildXmlCredentialRepo()
        {
            var xmlFileBuilder = new CredRepoXmlFileBuilder();
            var xmlFileContent = xmlFileBuilder.Build(CryptographyProvider.Encrypt("someheaderdata", EncryptionKey));
            var dataProvider = new InMemoryStringDataProvider(xmlFileContent);
            var encryptor = new XmlCredentialPasswordEncryptorDecorator(
                CryptographyProvider,
                new XmlCredentialRecordSerializer()
            );
            var decryptor = new XmlCredentialPasswordDecryptorDecorator(
                new XmlCredentialRecordDeserializer()
            );

            return new XmlCredentialRepository(
                new CredentialRepositoryConfig(),
                new CredentialRecordSaver(
                    dataProvider,
                    encryptor
                ), new CredentialRecordLoader(
                    dataProvider,
                    decryptor
                )
            );
        }
    }
}