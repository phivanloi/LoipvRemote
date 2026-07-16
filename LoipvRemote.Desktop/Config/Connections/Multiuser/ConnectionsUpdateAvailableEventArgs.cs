using System;

namespace LoipvRemote.Config.Connections.Multiuser
{
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
