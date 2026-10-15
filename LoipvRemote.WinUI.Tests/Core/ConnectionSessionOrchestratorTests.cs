using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Application.Sessions;
using NSubstitute;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Core;

#pragma warning disable CA2012 // NSubstitute configures ValueTask-returning members without consuming their configured values.

public sealed class ConnectionSessionOrchestratorTests
{
    [Test]
    public async Task StartsValidatedDefinitionAndReturnsOwnedSession()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        IProtocolSession session = CreateSession(initialized: true, connected: true);
        factory.Create(Arg.Any<ConnectionDefinition>()).Returns(session);
        ConnectionDefinition definition = CreateDefinition();

        ConnectionSessionStartOutcome outcome = await CreateOrchestrator(factory).StartAsync(definition);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(ConnectionSessionStartStatus.Started));
            Assert.That(outcome.Session, Is.SameAs(session));
        });
        factory.Received(1).Create(definition);
    }

    [Test]
    public async Task RejectsInvalidDefinitionBeforeCreatingAProtocolSession()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        ConnectionDefinition invalidDefinition = CreateDefinition() with { Name = string.Empty };

        ConnectionSessionStartOutcome outcome = await CreateOrchestrator(factory).StartAsync(invalidDefinition);

        Assert.That(outcome.Status, Is.EqualTo(ConnectionSessionStartStatus.InvalidDefinition));
        factory.DidNotReceive().Create(Arg.Any<ConnectionDefinition>());
    }

    [Test]
    public async Task ClosesAndDisposesSessionAfterConnectionFailure()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        IProtocolSession session = CreateSession(initialized: true, connected: false);
        factory.Create(Arg.Any<ConnectionDefinition>()).Returns(session);

        ConnectionSessionStartOutcome outcome = await CreateOrchestrator(factory).StartAsync(CreateDefinition());

        Assert.That(outcome.Status, Is.EqualTo(ConnectionSessionStartStatus.ConnectionFailed));
        await session.Received(1).CloseAsync(Arg.Any<CancellationToken>());
        await session.Received(1).DisposeAsync();
    }

    private static ConnectionSessionOrchestrator CreateOrchestrator(IProtocolFactory factory) =>
        new(factory, new SessionLifecycleCoordinator());

    private static IProtocolSession CreateSession(bool initialized, bool connected)
    {
        IProtocolSession session = Substitute.For<IProtocolSession>();
        _ = session.InitializeAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(initialized));
        _ = session.ConnectAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(connected));
        return session;
    }

    private static ConnectionDefinition CreateDefinition() => new(
        Guid.NewGuid(), "ssh", "host.example", 22, ProtocolKind.Ssh2, CredentialReference.None);
}

#pragma warning restore CA2012
