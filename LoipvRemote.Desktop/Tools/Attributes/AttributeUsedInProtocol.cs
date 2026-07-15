using System;

namespace LoipvRemote.Tools.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class AttributeUsedInProtocol(params ProtocolKind[] supportedProtocolTypes) : Attribute
    {
        public ProtocolKind[] SupportedProtocolTypes { get; } = supportedProtocolTypes;
    }
}
