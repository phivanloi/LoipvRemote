using LoipvRemote.UseCases.Sessions;
using LoipvRemote.Protocols.Abstractions;
using NSubstitute;
using NUnit.Framework;

namespace LoipvRemoteTests.UseCases.Sessions;

public class SessionLifecycleCoordinatorTests
{
    [Test]
    public async Task StopAllAsyncDisconnectsClosesAndDisposesEveryTrackedSession()
    {
        var coordinator = new SessionLifecycleCoordinator();
        IProtocolSession session = Substitute.For<IProtocolSession>();
        session.Initialize().Returns(true);
        session.Connect().Returns(true);

        Assert.That(coordinator.Start(session), Is.EqualTo(SessionStartResult.Started));

        await coordinator.StopAllAsync(CancellationToken.None);

        session.Received(1).Disconnect();
        session.Received(1).Close();
        session.Received(1).Dispose();
        Assert.That(coordinator.ActiveSessionCount, Is.Zero);
    }

    [Test]
    public void StartClosesSessionWhenInitializationFails()
    {
        var session = Substitute.For<IProtocolSession>();
        session.Initialize().Returns(false);

        var result = new SessionLifecycleCoordinator().Start(session);

        Assert.That(result, Is.EqualTo(SessionStartResult.InitializationFailed));
        session.Received(1).Close();
        session.DidNotReceive().Connect();
    }

    [Test]
    public void StartClosesSessionWhenConnectFails()
    {
        var session = Substitute.For<IProtocolSession>();
        session.Initialize().Returns(true);
        session.Connect().Returns(false);

        var result = new SessionLifecycleCoordinator().Start(session);

        Assert.That(result, Is.EqualTo(SessionStartResult.ConnectionFailed));
        session.Received(1).Close();
    }

    [Test]
    public void StartConnectsInitializedSession()
    {
        var session = Substitute.For<IProtocolSession>();
        session.Initialize().Returns(true);
        session.Connect().Returns(true);

        var result = new SessionLifecycleCoordinator().Start(session);

        Assert.That(result, Is.EqualTo(SessionStartResult.Started));
        session.DidNotReceive().Close();
    }
}
