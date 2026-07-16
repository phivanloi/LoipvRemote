using LoipvRemote.Connection;
using LoipvRemote.Container;
using System;
using System.Linq;
using LoipvRemote.UI.Controls.ConnectionTree;
using System.Runtime.Versioning;

namespace LoipvRemote.Tree
{
    [SupportedOSPlatform("windows")]
    public class PreviousSessionOpener : IConnectionTreeAction
    {
        private readonly IConnectionInitiator _connectionInitiator;

        public PreviousSessionOpener(IConnectionInitiator connectionInitiator)
        {
            ArgumentNullException.ThrowIfNull(connectionInitiator);
            _connectionInitiator = connectionInitiator;
        }

        public void Execute(IConnectionTree connectionTree)
        {
            System.Collections.Generic.IEnumerable<ConnectionInfo> connectionInfoList = connectionTree.GetRootConnectionNode().GetRecursiveChildList()
                                                   .Where(node => !(node is ContainerInfo));
            System.Collections.Generic.IEnumerable<ConnectionInfo> previouslyOpenedConnections = connectionInfoList
                .Where(item =>
                           item.PleaseConnect &&
                           //ignore items that have already connected
                           !_connectionInitiator.ActiveConnections.Contains(item.ConstantID));

            foreach (ConnectionInfo connectionInfo in previouslyOpenedConnections)
            {
                _ = _connectionInitiator.OpenConnectionAsync(connectionInfo);
            }
        }
    }
}
