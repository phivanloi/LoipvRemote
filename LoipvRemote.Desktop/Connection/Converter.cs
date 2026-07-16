using System;

namespace LoipvRemote.Connection
{
    public static class Converter
    {
        public static string ProtocolToString(ProtocolKind protocol)
        {
            return protocol.ToString();
        }

        public static ProtocolKind StringToProtocol(string protocol)
        {
            try
            {
                return Enum.Parse<ProtocolKind>(protocol, true);
            }
            catch (Exception)
            {
                return ProtocolKind.Rdp;
            }
        }
    }
}