using System;
using System.DirectoryServices;
using System.Text.RegularExpressions;
using LoipvRemote.Config.Import;
using LoipvRemote.Connection;
using LoipvRemote.Connection.Protocol;
using LoipvRemote.Container;
using LoipvRemote.Tools;
using LoipvRemote.Tree;
using LoipvRemote.Tree.Root;
using LoipvRemote.Resources.Language;
using LoipvRemote.Security;
using LoipvRemote.Messages;
using System.Runtime.Versioning;

namespace LoipvRemote.Config.Serializers.MiscSerializers
{
    [SupportedOSPlatform("windows")]
    public class ActiveDirectoryDeserializer(string ldapPath, bool importSubOu, MessageCollector messageCollector)
    {
        private readonly string _ldapPath = SanitizeLdapPath(ldapPath.ThrowIfNullOrEmpty(nameof(ldapPath)));
        private readonly bool _importSubOu = importSubOu;
        private readonly MessageCollector _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));

        private static string SanitizeLdapPath(string ldapPath)
        {
            LdapPathSanitizer.ValidatePath(ldapPath);

            int schemeEndIndex = ldapPath.IndexOf("://", StringComparison.OrdinalIgnoreCase) + 3;
            int pathStartIndex = ldapPath.IndexOf('/', schemeEndIndex);
            if (pathStartIndex < 0)
                return ldapPath;

            string schemeAndServer = ldapPath[..(pathStartIndex + 1)];
            string distinguishedName = ldapPath[(pathStartIndex + 1)..];
            return schemeAndServer + LdapPathSanitizer.SanitizeDistinguishedName(distinguishedName);
        }

        public ConnectionTreeModel Deserialize()
        {
            ConnectionTreeModel connectionTreeModel = new();
            RootNodeInfo root = new(RootNodeType.Connection);
            connectionTreeModel.AddRootNode(root);

            ImportContainers(_ldapPath, root);

            return connectionTreeModel;
        }

        private void ImportContainers(string ldapPath, ContainerInfo parentContainer)
        {
            Match match = Regex.Match(ldapPath, "ou=([^,]*)", RegexOptions.IgnoreCase);
            string name = match.Success ? match.Groups[1].Captures[0].Value : Language.ActiveDirectory;

            ContainerInfo newContainer = new() { Name = name};
            parentContainer.AddChild(newContainer);

            ImportComputers(ldapPath, newContainer);
        }

        private void ImportComputers(string ldapPath, ContainerInfo parentContainer)
        {
            try
            {
                const string ldapFilter = "(|(objectClass=computer)(objectClass=organizationalUnit))";
                using (DirectorySearcher ldapSearcher = new())
                {
                    ldapSearcher.SearchRoot = new DirectoryEntry(ldapPath);
                    ldapSearcher.Filter = ldapFilter;
                    ldapSearcher.SearchScope = SearchScope.OneLevel;
                    ldapSearcher.PropertiesToLoad.AddRange(new[] {"securityEquals", "cn", "objectClass"});

                    SearchResultCollection ldapResults = ldapSearcher.FindAll();
                    foreach (SearchResult ldapResult in ldapResults)
                    {
                        using (DirectoryEntry directoryEntry = ldapResult.GetDirectoryEntry())
                        {
                            if (directoryEntry.Properties["objectClass"].Contains("organizationalUnit"))
                            {
                                // check/continue here so we don't create empty connection objects
                                if (!_importSubOu) continue;

                                new ActiveDirectoryImporter(_messageCollector).Import(ldapResult.Path, parentContainer, _importSubOu);
                                continue;
                            }

                            DeserializeConnection(directoryEntry, parentContainer);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _messageCollector.AddExceptionMessage("Config.Import.ActiveDirectory.ImportComputers() failed.", ex);
            }
        }

        private void DeserializeConnection(DirectoryEntry directoryEntry, ContainerInfo parentContainer)
        {
            string displayName = Convert.ToString(directoryEntry.Properties["cn"].Value);
            string description = Convert.ToString(directoryEntry.Properties["Description"].Value);
            string hostName = Convert.ToString(directoryEntry.Properties["dNSHostName"].Value);

            ConnectionInfo newConnectionInfo = new()
            {
                Name = displayName,
                Hostname = hostName,
                Description = description,
                Protocol = ProtocolType.RDP
            };
            newConnectionInfo.Inheritance.TurnOnInheritanceCompletely();
            newConnectionInfo.Inheritance.Description = false;

            parentContainer.AddChild(newConnectionInfo);
        }
    }
}
