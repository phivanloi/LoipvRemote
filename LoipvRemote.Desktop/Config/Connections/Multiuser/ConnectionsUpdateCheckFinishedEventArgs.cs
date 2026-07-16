using System;

namespace LoipvRemote.Config.Connections.Multiuser
{
    public class ConnectionsUpdateCheckFinishedEventArgs : EventArgs
    {
        public bool UpdateAvailable { get; set; }
    }
}
