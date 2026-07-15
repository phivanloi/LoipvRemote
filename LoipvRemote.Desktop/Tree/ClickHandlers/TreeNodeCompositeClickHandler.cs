using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using LoipvRemote.Connection;

namespace LoipvRemote.Tree.ClickHandlers
{
    [SupportedOSPlatform("windows")]
    public class TreeNodeCompositeClickHandler : ITreeNodeClickHandler<ConnectionInfo>
    {
        public IEnumerable<ITreeNodeClickHandler<ConnectionInfo>> ClickHandlers { get; set; } =
            new ITreeNodeClickHandler<ConnectionInfo>[0];

        public void Execute(ConnectionInfo clickedNode)
        {
            ArgumentNullException.ThrowIfNull(clickedNode);
            foreach (ITreeNodeClickHandler<ConnectionInfo> handler in ClickHandlers)
            {
                handler.Execute(clickedNode);
            }
        }
    }
}