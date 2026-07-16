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
        session.InitializeAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(true));
        session.ConnectAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(true));

        Assert.That(await coordinator.StartAsync(session), Is.EqualTo(SessionStartResult.Started));

        await coordinator.StopAllAsync(CancellationToken.None);

        _ = session.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
        await session.Received(1).CloseAsync(Arg.Any<CancellationToken>());
        _ = session.Received(1).DisposeAsync();
        Assert.That(coordinator.ActiveSessionCount, Is.Zero);
    }

    [Test]
    public async Task StartAsyncClosesSessionWhenInitializationFails()
    {
        var session = Substitute.For<IProtocolSession>();
        session.InitializeAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(false));

        var result = await new SessionLifecycleCoordinator().StartAsync(session);

        Assert.That(result, Is.EqualTo(SessionStartResult.InitializationFailed));
        await session.Received(1).CloseAsync(Arg.Any<CancellationToken>());
        await session.DidNotReceive().ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StartAsyncClosesSessionWhenConnectFails()
    {
        var session = Substitute.For<IProtocolSession>();
        session.InitializeAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(true));
        session.ConnectAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(false));

        var result = await new SessionLifecycleCoordinator().StartAsync(session);

        Assert.That(result, Is.EqualTo(SessionStartResult.ConnectionFailed));
        await session.Received(1).CloseAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StartAsyncConnectsInitializedSession()
    {
        var session = Substitute.For<IProtocolSession>();
        session.InitializeAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(true));
        session.ConnectAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(true));

        var result = await new SessionLifecycleCoordinator().StartAsync(session);

        Assert.That(result, Is.EqualTo(SessionStartResult.Started));
        await session.DidNotReceive().CloseAsync(Arg.Any<CancellationToken>());
    }
}
