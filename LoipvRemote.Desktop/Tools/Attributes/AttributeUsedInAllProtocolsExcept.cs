using System;
using System.Linq;

namespace LoipvRemote.Tools.Attributes
{
    public class AttributeUsedInAllProtocolsExcept(params ProtocolKind[] exceptions) : UsedInProtocolAttribute(Enum
                .GetValues<ProtocolKind>()
                .Cast<ProtocolKind>()
                .Except(exceptions)
                .ToArray())
    {
    }
}
