using System;
using System.Runtime.Versioning;
using LoipvRemote.Connection.Protocol.VNC;

namespace LoipvRemote.Connection.Protocol.ARD
{
    [SupportedOSPlatform("windows")]
    public class ProtocolARD : ProtocolVNC
    {
        public ProtocolARD()
        {
        }

        public new enum Defaults
        {
            Port = 5900
        }
    }
}
