using System;

namespace LoipvRemote.Config.Connections.Multiuser
{
    public delegate void
        ConnectionsUpdateAvailableEventHandler(object sender, ConnectionsUpdateAvailableEventArgs args);

    public class ConnectionsUpdateAvailableEventArgs : EventArgs
    {
        public string Revision { get; }
        public bool Handled { get; set; }

        public ConnectionsUpdateAvailableEventArgs(string revision)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(revision);
            Revision = revision;
        }
    }
}
