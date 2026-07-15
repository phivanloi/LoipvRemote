using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Xml.Linq;
using LoipvRemote.Config;
using LoipvRemote.Config.DataProviders;
using LoipvRemote.Config.Serializers;

namespace LoipvRemote.Credential.Repositories
{
    [SupportedOSPlatform("windows")]
    public class XmlCredentialRepositoryFactory
    {
        private readonly ISecureSerializer<IEnumerable<ICredentialRecord>, string> _serializer;
        private readonly ISecureDeserializer<string, IEnumerable<ICredentialRecord>> _deserializer;

        public XmlCredentialRepositoryFactory(ISecureSerializer<IEnumerable<ICredentialRecord>, string> serializer,
                                              ISecureDeserializer<string, IEnumerable<ICredentialRecord>> deserializer)
        {
            ArgumentNullException.ThrowIfNull(serializer);
            ArgumentNullException.ThrowIfNull(deserializer);

            _serializer = serializer;
            _deserializer = deserializer;
        }

        public ICredentialRepository Build(ICredentialRepositoryConfig config)
        {
            return BuildXmlRepo(config);
        }

        public ICredentialRepository Build(XElement repositoryXElement)
        {
            string? stringId = repositoryXElement.Attribute("Id")?.Value;
            Guid.TryParse(stringId, out Guid id);
            if (id.Equals(Guid.Empty)) id = Guid.NewGuid();
            CredentialRepositoryConfig config = new(id)
            {
                TypeName = repositoryXElement.Attribute("TypeName")?.Value ?? string.Empty,
                Title = repositoryXElement.Attribute("Title")?.Value ?? string.Empty,
                Source = repositoryXElement.Attribute("Source")?.Value ?? string.Empty
            };
            return BuildXmlRepo(config);
        }

        private ICredentialRepository BuildXmlRepo(ICredentialRepositoryConfig config)
        {
            FileDataProvider dataProvider = new(config.Source);
            CredentialRecordSaver saver = new(dataProvider, _serializer);
            CredentialRecordLoader loader = new(dataProvider, _deserializer);
            return new XmlCredentialRepository(config, saver, loader);
        }
    }
}