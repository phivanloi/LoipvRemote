using System.Runtime.Versioning;

namespace LoipvRemote.Connection.Protocol.Rlogin
{
    [SupportedOSPlatform("windows")]
    public class ProtocolRlogin : PuttyBase
    {
        public ProtocolRlogin()
        {
            this.PuttyProtocol = Putty_Protocol.rlogin;
        }

        public enum Defaults
        {
            Port = 513
        }
    }
}