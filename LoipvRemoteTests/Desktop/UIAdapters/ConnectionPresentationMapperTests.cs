using System;
using LoipvRemote.Desktop.UIAdapters;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Domain.Protocols;
using NUnit.Framework;

namespace LoipvRemoteTests.Desktop.UIAdapters;

public class ConnectionPresentationMapperTests
{
    [Test]
    public void MapsDomainConnectionToUiOnlyPresentationModel()
    {
        var id = Guid.NewGuid();
        var definition = new ConnectionDefinition(
            id,
            "production ssh",
            "host.example",
            22,
            ProtocolKind.Ssh2,
            CredentialReference.None);

        var presentation = ConnectionPresentationMapper.ToPresentation(definition, ProtocolSessionState.Connected);

        Assert.Multiple(() =>
        {
            Assert.That(presentation.Id, Is.EqualTo(id));
            Assert.That(presentation.Title, Is.EqualTo("production ssh"));
            Assert.That(presentation.Endpoint, Is.EqualTo("host.example:22"));
            Assert.That(presentation.SessionState, Is.EqualTo(ProtocolSessionState.Connected));
        });
    }
}
