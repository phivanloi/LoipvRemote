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

    [Test]
    public async Task StartAsyncClosesSessionWhenInitializationThrows()
    {
        var session = Substitute.For<IProtocolSession>();
        session.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns<ValueTask<bool>>(_ => throw new InvalidOperationException("init failed"));

        var result = await new SessionLifecycleCoordinator().StartAsync(session);

        Assert.That(result, Is.EqualTo(SessionStartResult.InitializationFailed));
        await session.Received(1).CloseAsync(CancellationToken.None);
        await session.DidNotReceive().ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StartAsyncClosesSessionWhenConnectThrows()
    {
        var session = Substitute.For<IProtocolSession>();
        session.InitializeAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(true));
        session.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns<ValueTask<bool>>(_ => throw new InvalidOperationException("connect failed"));

        var result = await new SessionLifecycleCoordinator().StartAsync(session);

        Assert.That(result, Is.EqualTo(SessionStartResult.ConnectionFailed));
        await session.Received(1).CloseAsync(CancellationToken.None);
    }

    [Test]
    public async Task StartAsyncClosesAndPropagatesCancellation()
    {
        using CancellationTokenSource cancellation = new();
        var session = Substitute.For<IProtocolSession>();
        session.InitializeAsync(Arg.Any<CancellationToken>())
            .Returns<ValueTask<bool>>(_ => throw new OperationCanceledException(cancellation.Token));

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new SessionLifecycleCoordinator().StartAsync(session, cancellation.Token).AsTask());

        await session.Received(1).CloseAsync(CancellationToken.None);
    }

    [Test]
    public async Task StopAllAsyncContinuesAfterOneSessionFails()
    {
        var coordinator = new SessionLifecycleCoordinator();
        IProtocolSession failed = CreateConnectedSession();
        IProtocolSession healthy = CreateConnectedSession();
        failed.DisconnectAsync(Arg.Any<CancellationToken>())
            .Returns<ValueTask>(_ => throw new InvalidOperationException("disconnect failed"));

        Assert.That(await coordinator.StartAsync(failed), Is.EqualTo(SessionStartResult.Started));
        Assert.That(await coordinator.StartAsync(healthy), Is.EqualTo(SessionStartResult.Started));

        Assert.ThrowsAsync<AggregateException>(async () =>
            await coordinator.StopAllAsync(CancellationToken.None));

        await failed.Received(1).CloseAsync(CancellationToken.None);
        await failed.Received(1).DisposeAsync();
        await healthy.Received(1).DisconnectAsync(Arg.Any<CancellationToken>());
        await healthy.Received(1).CloseAsync(CancellationToken.None);
        await healthy.Received(1).DisposeAsync();
        Assert.That(coordinator.ActiveSessionCount, Is.Zero);
    }

    private static IProtocolSession CreateConnectedSession()
    {
        IProtocolSession session = Substitute.For<IProtocolSession>();
        session.InitializeAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(true));
        session.ConnectAsync(Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(true));
        return session;
    }
}
