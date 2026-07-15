using System;
using System.Runtime.Versioning;
using LoipvRemote.Connection;

namespace LoipvRemote.Tree.ClickHandlers
{
    [SupportedOSPlatform("windows")]
    public class SwitchToConnectionClickHandler : ITreeNodeClickHandler<ConnectionInfo>
    {
        private readonly IConnectionInitiator _connectionInitiator;

        public SwitchToConnectionClickHandler(IConnectionInitiator connectionInitiator)
        {
            ArgumentNullException.ThrowIfNull(connectionInitiator);
            _connectionInitiator = connectionInitiator;
        }

        public void Execute(ConnectionInfo clickedNode)
        {
            ArgumentNullException.ThrowIfNull(clickedNode);
            if (clickedNode.GetTreeNodeType() != TreeNodeType.Connection &&
                clickedNode.GetTreeNodeType() != TreeNodeType.PuttySession) return;
            _connectionInitiator.SwitchToOpenConnection(clickedNode);
        }
    }
}