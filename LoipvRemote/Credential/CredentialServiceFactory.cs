using System.IO;
using System.Runtime.Versioning;
using LoipvRemote.App;
using LoipvRemote.App.Info;
using LoipvRemote.Config;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers.CredentialProviderSerializer;
using LoipvRemote.Config.Serializers.CredentialSerializer;
using LoipvRemote.Security.Factories;

namespace LoipvRemote.Credential
{
    [SupportedOSPlatform("windows")]
    public class CredentialServiceFactory
    {
        // When we get a true CompositionRoot we can move this to that class. We should only require 1 instance of this service at a time
        public CredentialServiceFacade Build()
        {
            CryptoProviderFactoryFromSettings cryptoFromSettings = new();
            XmlCredentialPasswordEncryptorDecorator credRepoSerializer = new(cryptoFromSettings.Build(), new XmlCredentialRecordSerializer());
            XmlCredentialPasswordDecryptorDecorator credRepoDeserializer = new(new XmlCredentialRecordDeserializer());

            string credentialRepoListPath = Path.Combine(SettingsFileInfo.SettingsPath, "credentialRepositories.xml");
            FileDataProvider repoListDataProvider = new(credentialRepoListPath);
            CredentialRepositoryListLoader repoListLoader = new(repoListDataProvider, new CredentialRepositoryListDeserializer(credRepoSerializer, credRepoDeserializer));
            CredentialRepositoryListSaver repoListSaver = new(repoListDataProvider);

            return new CredentialServiceFacade(Runtime.CredentialProviderCatalog, repoListLoader, repoListSaver);
        }
    }
}