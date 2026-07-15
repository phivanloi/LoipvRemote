using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using LoipvRemote.Credential;


namespace LoipvRemote.Config.Serializers.CredentialProviderSerializer
{
    public class CredentialRepositoryListSerializer
    {
        public string Serialize(IEnumerable<ICredentialRepository> credentialProviderCatalog)
        {
            XDocument xmlDocument = new(new XDeclaration("1.0", "utf-8", null));
            XElement rootElement = new("CredentialRepositories",
                                           from provider in credentialProviderCatalog
                                           select new XElement("CredentialRepository",
                                                               new XAttribute("Id", provider.Config.Id),
                                                               new XAttribute("TypeName", provider.Config.TypeName),
                                                               new XAttribute("Title", provider.Config.Title),
                                                               new XAttribute("Source", provider.Config.Source)
                                                              )
                                          );
            xmlDocument.Add(rootElement);
            string declaration = xmlDocument.Declaration.ToString();
            string documentBody = xmlDocument.ToString();
            return string.Concat(declaration, Environment.NewLine, documentBody);
        }
    }
}