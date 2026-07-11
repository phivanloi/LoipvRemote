using System;
using LoipvRemote.Connection.Protocol;

namespace LoipvRemote.Tools.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class AttributeUsedInProtocol(params ProtocolType[] supportedProtocolTypes) : Attribute
    {
        public ProtocolType[] SupportedProtocolTypes { get; } = supportedProtocolTypes;
    }
}
