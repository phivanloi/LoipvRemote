using System;
using System.Linq;
using LoipvRemote.Connection.Protocol;

namespace LoipvRemote.Tools.Attributes
{
    public class AttributeUsedInAllProtocolsExcept(params ProtocolType[] exceptions) : AttributeUsedInProtocol(Enum
                .GetValues(typeof(ProtocolType))
                .Cast<ProtocolType>()
                .Except(exceptions)
                .ToArray())
    {
    }
}
