using LoipvRemote.App;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Versioning;
using System;

namespace LoipvRemote.Tools
{
    [SupportedOSPlatform("windows")]
    public class SshTunnelTypeConverter : StringConverter
    {
        private static Func<IEnumerable<ConnectionInfo>> s_rootSource = static () => [];

        public static void Configure(Func<IEnumerable<ConnectionInfo>> rootSource)
        {
            s_rootSource = rootSource ?? throw new ArgumentNullException(nameof(rootSource));
        }

        public static string[] SshTunnels
        {
            get
            {
                List<string> sshTunnelList = new() { string.Empty};

                // Add a blank entry to signify that no external tool is selected
                sshTunnelList.AddRange(GetSshConnectionNames(s_rootSource()));
                return sshTunnelList.ToArray();
            }
        }

        // recursively traverse the connection tree to find all ConnectionInfo s of type SSH
        private static IEnumerable<string> GetSshConnectionNames(IEnumerable<ConnectionInfo> rootnodes)
        {
            List<string> result = new();
            foreach (ConnectionInfo node in rootnodes)
                if (node is ContainerInfo container)
                {
                    result.AddRange(GetSshConnectionNames(container.Children));
                }
                else
                {
                    if (node is PuttySessionInfo) continue;
                    if (node.Protocol == ProtocolKind.Ssh1 || node.Protocol == ProtocolKind.Ssh2)
                        result.Add(node.Name);
                }

            return result;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
        {
            return new StandardValuesCollection(SshTunnels);
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context)
        {
            return true;
        }

        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context)
        {
            return true;
        }
    }
}
