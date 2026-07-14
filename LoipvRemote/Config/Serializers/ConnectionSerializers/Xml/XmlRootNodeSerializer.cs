using System;
using System.Runtime.Versioning;
using System.Xml.Linq;
using LoipvRemote.Security;
using LoipvRemote.Tree.Root;

namespace LoipvRemote.Config.Serializers.ConnectionSerializers.Xml
{
    [SupportedOSPlatform("windows")]
    public class XmlRootNodeSerializer
    {
        public XElement SerializeRootNodeInfo(RootNodeInfo rootNodeInfo, ICryptographyProvider cryptographyProvider, Version version, bool fullFileEncryption = false)
        {
            XNamespace xmlNamespace = "http://mremoteng.org";
            XElement element = new(xmlNamespace + "Connections");
            element.Add(new XAttribute(XNamespace.Xmlns + "mrng", xmlNamespace));
            element.Add(new XAttribute(XName.Get("Name"), rootNodeInfo.Name));
            element.Add(new XAttribute(XName.Get("Export"), "false"));
            element.Add(new XAttribute(XName.Get("EncryptionEngine"), cryptographyProvider.CipherEngine));
            element.Add(new XAttribute(XName.Get("BlockCipherMode"), cryptographyProvider.CipherMode));
            element.Add(new XAttribute(XName.Get("KdfIterations"), cryptographyProvider.KeyDerivationIterations));
            element.Add(new XAttribute(XName.Get("FullFileEncryption"), fullFileEncryption.ToString().ToLowerInvariant()));
            element.Add(CreateProtectedAttribute(rootNodeInfo, cryptographyProvider));
            element.Add(new XAttribute(XName.Get("ConfVersion"), version.ToString(2)));
            return element;
        }

        private XAttribute CreateProtectedAttribute(RootNodeInfo rootNodeInfo, ICryptographyProvider cryptographyProvider)
        {
            XAttribute attribute = new(XName.Get("Protected"), "");
            if (string.IsNullOrEmpty(rootNodeInfo.PasswordString))
                return attribute;

            attribute.Value = cryptographyProvider.Encrypt("ThisIsProtected", rootNodeInfo.PasswordString.ConvertToSecureString());
            return attribute;
        }
    }
}
