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
    public void StartsValidatedDefinitionAndReturnsOwnedSession()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        IProtocolSession session = CreateSession(initializeResult: true, connectResult: true);
        factory.Create(Arg.Any<ConnectionDefinition>()).Returns(session);
        var definition = CreateDefinition();

        ConnectionSessionStartOutcome outcome = CreateOrchestrator(factory).Start(definition);

        Assert.Multiple(() =>
        {
            Assert.That(outcome.Status, Is.EqualTo(ConnectionSessionStartStatus.Started));
            Assert.That(outcome.Session, Is.SameAs(session));
        });
        factory.Received(1).Create(definition);
        session.DidNotReceive().Dispose();
    }

    [Test]
    public void StartsExistingSessionAndPublishesItsConnectedState()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        IProtocolSession session = CreateSession(initializeResult: true, connectResult: true);
        var receivedEvents = new List<ConnectionSessionStateChanged>();
        var orchestrator = CreateOrchestrator(factory);
        ConnectionDefinition definition = CreateDefinition();
        orchestrator.StateChanged += receivedEvents.Add;

        ConnectionSessionStartOutcome outcome = orchestrator.Start(definition, session);

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
    public void RejectsInvalidDefinitionBeforeCreatingProtocol()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        var invalidDefinition = CreateDefinition() with { Name = string.Empty };

        ConnectionSessionStartOutcome outcome = CreateOrchestrator(factory).Start(invalidDefinition);

        Assert.That(outcome.Status, Is.EqualTo(ConnectionSessionStartStatus.InvalidDefinition));
        factory.DidNotReceive().Create(Arg.Any<ConnectionDefinition>());
    }

    [Test]
    public void DisposesSessionAfterConnectionFailure()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        IProtocolSession session = CreateSession(initializeResult: true, connectResult: false);
        factory.Create(Arg.Any<ConnectionDefinition>()).Returns(session);

        ConnectionSessionStartOutcome outcome = CreateOrchestrator(factory).Start(CreateDefinition());

        Assert.That(outcome.Status, Is.EqualTo(ConnectionSessionStartStatus.ConnectionFailed));
        session.Received(1).Close();
        session.Received(1).Dispose();
    }

    [Test]
    public void ReportsUnavailableProtocolWithoutLeakingFactoryException()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        factory.Create(Arg.Any<ConnectionDefinition>()).Returns(_ => throw new NotSupportedException());

        ConnectionSessionStartOutcome outcome = CreateOrchestrator(factory).Start(CreateDefinition());

        Assert.That(outcome.Status, Is.EqualTo(ConnectionSessionStartStatus.ProtocolUnavailable));
        Assert.That(outcome.Session, Is.Null);
    }

    [Test]
    public void StopDisconnectsAndDisposesSession()
    {
        IProtocolFactory factory = Substitute.For<IProtocolFactory>();
        IProtocolSession session = Substitute.For<IProtocolSession>();

        CreateOrchestrator(factory).Stop(session);

        session.Received(1).Disconnect();
        session.Received(1).Dispose();
    }

    private static ConnectionSessionOrchestrator CreateOrchestrator(IProtocolFactory factory) =>
        new(factory, new SessionLifecycleCoordinator());

    private static IProtocolSession CreateSession(bool initializeResult, bool connectResult)
    {
        IProtocolSession session = Substitute.For<IProtocolSession>();
        session.Initialize().Returns(initializeResult);
        session.Connect().Returns(connectResult);
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
