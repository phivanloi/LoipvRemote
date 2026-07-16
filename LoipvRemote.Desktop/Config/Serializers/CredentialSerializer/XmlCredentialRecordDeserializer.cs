using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using LoipvRemote.Credential;
using LoipvRemote.Security;

namespace LoipvRemote.Config.Serializers.CredentialSerializer
{
    public class XmlCredentialRecordDeserializer : IDeserializer<string, IEnumerable<ICredentialRecord>>
    {
        public string SchemaVersion { get; } = "1.0";

        public IEnumerable<ICredentialRecord> Deserialize(string xml)
        {
            if (string.IsNullOrEmpty(xml)) return Array.Empty<ICredentialRecord>();
            XDocument xdoc = XDocument.Parse(xml);
            XElement rootElement = xdoc.Root
                ?? throw new FormatException("Credential XML is missing its root element.");
            ValidateSchemaVersion(rootElement);

            IEnumerable<CredentialRecord> credentials = from element in xdoc.Descendants("Credential")
                                                        select new CredentialRecord(Guid.Parse(element.Attribute("Id")?.Value ??
                                                                                               Guid.NewGuid().ToString()))
                                                        {
                                                            Title = element.Attribute("Title")?.Value ?? "",
                                                            Username = element.Attribute("Username")?.Value ?? "",
                                                            Password = element.Attribute("Password")?.Value?.ConvertToSecureString()
                                                                       ?? new System.Security.SecureString(),
                                                            Domain = element.Attribute("Domain")?.Value ?? ""
                                                        };
            return credentials.ToArray();
        }

        private void ValidateSchemaVersion(XElement rootElement)
        {
            string? docSchemaVersion = rootElement.Attribute("SchemaVersion")?.Value;
            if (docSchemaVersion != SchemaVersion)
                throw new NotSupportedException($"The schema version of this document is not supported by this class. Document Version: {docSchemaVersion} Supported Version: {SchemaVersion}");
        }
    }
}
