using System;
using LoipvRemote.Config.Serializers.ConnectionSerializers.Xml;
using NUnit.Framework;

namespace LoipvRemoteTests.Config.Serializers.ConnectionSerializers.Xml;

public class XmlConnectionsDeserializerTests
{
    [Test]
    public void RejectsLegacyConnectionFileFormats()
    {
        var deserializer = new XmlConnectionsDeserializer();

        NotSupportedException exception = Assert.Throws<NotSupportedException>(
            () => deserializer.Deserialize("<Connections ConfVersion=\"2.6\" />"));

        Assert.That(exception.Message, Does.Contain("2.8"));
    }
}
