using System;
using System.Linq;

namespace LoipvRemote.Tools.Attributes
{
    public class AttributeUsedInAllProtocolsExcept(params ProtocolKind[] exceptions) : AttributeUsedInProtocol(Enum
                .GetValues(typeof(ProtocolKind))
                .Cast<ProtocolKind>()
                .Except(exceptions)
                .ToArray())
    {
    }
}
