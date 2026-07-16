using System;
using System.Collections.Generic;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Domain.Events;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.UseCases.Sessions;
using NSubstitute;
using NUnit.Framework;

namespace LoipvRemoteTests.UseCases.Sessions;

public class ConnectionSessionOrchestratorTests
{
    [Test]
    public async Task StartsValidatedDefinitionAndReturnsOwnedSession()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        IProtocolSession session = CreateSession(initializeResult: true, connectResult: true);
        factory.Create(Arg.Any<ConnectionDefinition>()).Returns(session);
        var definition = CreateDefinition();

        ConnectionSessionStartOutcome outcome = await CreateOrchestrator(factory).StartAsync(definition);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(ConnectionSessionStartStatus.Started));
            Assert.That(outcome.Session, Is.SameAs(session));
        });
        factory.Received(1).Create(definition);
        session.DidNotReceive().Dispose();
    }

    [Test]
    public async Task StartsExistingSessionAndPublishesItsConnectedState()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        IProtocolSession session = CreateSession(initializeResult: true, connectResult: true);
        var receivedEvents = new List<ConnectionSessionStateChanged>();
        var orchestrator = CreateOrchestrator(factory);
        ConnectionDefinition definition = CreateDefinition();
        orchestrator.StateChanged += receivedEvents.Add;

        ConnectionSessionStartOutcome outcome = await orchestrator.StartAsync(definition, session);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(ConnectionSessionStartStatus.Started));
            Assert.That(outcome.Session, Is.SameAs(session));
            Assert.That(receivedEvents, Has.Count.EqualTo(1));
            Assert.That(receivedEvents[0].ConnectionId, Is.EqualTo(definition.Id));
        });
        factory.DidNotReceive().Create(Arg.Any<ConnectionDefinition>());
    }

    [Test]
    public async Task RejectsInvalidDefinitionBeforeCreatingProtocol()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        var invalidDefinition = CreateDefinition() with { Name = string.Empty };

        ConnectionSessionStartOutcome outcome = await CreateOrchestrator(factory).StartAsync(invalidDefinition);

        Assert.That(outcome.Status, Is.EqualTo(ConnectionSessionStartStatus.InvalidDefinition));
        factory.DidNotReceive().Create(Arg.Any<ConnectionDefinition>());
    }

    [Test]
    public async Task DisposesSessionAfterConnectionFailure()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        IProtocolSession session = CreateSession(initializeResult: true, connectResult: false);
        factory.Create(Arg.Any<ConnectionDefinition>()).Returns(session);

        ConnectionSessionStartOutcome outcome = await CreateOrchestrator(factory).StartAsync(CreateDefinition());

        Assert.That(outcome.Status, Is.EqualTo(ConnectionSessionStartStatus.ConnectionFailed));
        _ = session.Received(1).CloseAsync(Arg.Any<CancellationToken>());
        _ = session.Received(1).DisposeAsync();
    }

    [Test]
    public async Task ReportsUnavailableProtocolWithoutLeakingFactoryException()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        factory.Create(Arg.Any<ConnectionDefinition>()).Returns(_ => throw new NotSupportedException());

        ConnectionSessionStartOutcome outcome = await CreateOrchestrator(factory).StartAsync(CreateDefinition());

        Assert.That(outcome.Status, Is.EqualTo(ConnectionSessionStartStatus.ProtocolUnavailable));
        Assert.That(outcome.Session, Is.Null);
    }

    [Test]
    public async Task StopAsyncDisconnectsAndDisposesSession()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        IProtocolSession session = Substitute.For<IProtocolSession>();

        await CreateOrchestrator(factory).StopAsync(session);

        _ = session.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
        _ = session.Received(1).DisposeAsync();
    }

    private static ConnectionSessionOrchestrator CreateOrchestrator(IProtocolFactory factory) =>
        new(factory, new SessionLifecycleCoordinator());

    private static IProtocolSession CreateSession(bool initializeResult, bool connectResult)
    {
        IProtocolSession session = Substitute.For<IProtocolSession>();
        session.InitializeAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(initializeResult));
        session.ConnectAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(connectResult));
        return session;
    }

    private static ConnectionDefinition CreateDefinition() => new(
        Guid.NewGuid(),
        "ssh",
        "host.example",
        22,
        ProtocolKind.Ssh2,
        CredentialReference.None);
}
