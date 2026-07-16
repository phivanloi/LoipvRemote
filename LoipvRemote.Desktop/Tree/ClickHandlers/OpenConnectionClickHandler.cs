using System;
using System.Runtime.Versioning;
using LoipvRemote.Connection;

namespace LoipvRemote.Tree.ClickHandlers
{
    [SupportedOSPlatform("windows")]
    public class OpenConnectionClickHandler : ITreeNodeClickHandler<ConnectionInfo>
    {
        private readonly IConnectionInitiator _connectionInitiator;

        public OpenConnectionClickHandler(IConnectionInitiator connectionInitiator)
        {
            ArgumentNullException.ThrowIfNull(connectionInitiator);
            _connectionInitiator = connectionInitiator;
        }

        public void Execute(ConnectionInfo clickedNode)
        {
            ArgumentNullException.ThrowIfNull(clickedNode);
            if (clickedNode.GetTreeNodeType() != TreeNodeType.Connection &&
                clickedNode.GetTreeNodeType() != TreeNodeType.PuttySession) return;
            _ = _connectionInitiator.OpenConnectionAsync(clickedNode);
        }
    }
}
