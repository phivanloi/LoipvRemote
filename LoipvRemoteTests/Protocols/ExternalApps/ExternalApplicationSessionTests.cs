using System;
using LoipvRemote.Domain.Protocols;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;
using LoipvRemote.Protocols.ExternalApps;
using NUnit.Framework;

namespace LoipvRemoteTests.Protocols.ExternalApps;

[TestFixture]
public class ExternalApplicationSessionTests
{
    [Test]
    public void EmbeddedSession_TransitionsThroughLifecycleAndFocusesHost()
    {
        FakeExternalApplicationHost host = new();
        using ExternalApplicationSession session = new(CreateEmbeddedPlan(), host);

        Assert.That(session.Initialize(), Is.True);
        Assert.That(session.Connect(), Is.True);
        session.Focus();
        session.Close();

        Assert.Multiple(() =>
        {
            Assert.That(session.State, Is.EqualTo(ProtocolSessionState.Closed));
            Assert.That(session.Capabilities, Is.EqualTo(ProtocolCapabilities.EmbeddedWindow | ProtocolCapabilities.Resize));
            Assert.That(host.StartCount, Is.EqualTo(1));
            Assert.That(host.FocusCount, Is.EqualTo(1));
            Assert.That(host.CloseCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void InvalidElevatedEmbeddedPlanIsRejectedBeforeStart()
    {
        FakeExternalApplicationHost host = new();
        using ExternalApplicationSession session = new(
            CreateEmbeddedPlan() with { RunElevated = true },
            host);

        Assert.That(session.Initialize(), Is.False);
        Assert.Multiple(() =>
        {
            Assert.That(session.State, Is.EqualTo(ProtocolSessionState.Faulted));
            Assert.That(host.StartCount, Is.Zero);
        });
    }

    [Test]
    public void FailedStartMarksTheSessionFaulted()
    {
        FakeExternalApplicationHost host = new() { StartResult = false };
        using ExternalApplicationSession session = new(CreateEmbeddedPlan(), host);

        Assert.That(session.Initialize(), Is.True);
        Assert.That(session.Connect(), Is.False);
        Assert.That(session.State, Is.EqualTo(ProtocolSessionState.Faulted));
    }

    [Test]
    public void EmbeddedSession_AttachesAndResizesThroughHost()
    {
        FakeExternalApplicationHost host = new();
        using ExternalApplicationSession session = new(CreateEmbeddedPlan(), host);
        EmbeddedWindowBounds bounds = new(1, 2, 640, 480);

        Assert.That(session.Initialize(), Is.True);
        Assert.That(session.Connect(), Is.True);
        Assert.That(session.AttachTo(new IntPtr(123), TimeSpan.FromSeconds(1)), Is.True);
        session.Resize(bounds);

        Assert.Multiple(() =>
        {
            Assert.That(host.WaitForMainWindowCount, Is.EqualTo(1));
            Assert.That(host.AttachedParentHandle, Is.EqualTo(new IntPtr(123)));
            Assert.That(host.LastBounds, Is.EqualTo(bounds));
        });
    }

    [Test]
    public void HostExit_ClosesSessionAndRaisesExitNotification()
    {
        FakeExternalApplicationHost host = new();
        using ExternalApplicationSession session = new(CreateEmbeddedPlan(), host);
        int exitNotifications = 0;
        session.Exited += (_, _) => exitNotifications++;

        Assert.That(session.Initialize(), Is.True);
        Assert.That(session.Connect(), Is.True);
        host.RaiseExited();

        Assert.Multiple(() =>
        {
            Assert.That(session.State, Is.EqualTo(ProtocolSessionState.Closed));
            Assert.That(exitNotifications, Is.EqualTo(1));
        });
    }

    private static ExternalApplicationDefinition CreateEmbeddedPlan() => new(
        "Tool",
        "tool.exe",
        "--host example",
        string.Empty,
        RunElevated: false,
        EmbedWindow: true,
        WaitForExit: false);

    private sealed class FakeExternalApplicationHost : IExternalApplicationHost
    {
        public bool StartResult { get; set; } = true;
        public bool IsRunning { get; private set; }
        public IntPtr WindowHandle => IsRunning ? new IntPtr(456) : IntPtr.Zero;
        public string WindowTitle => "Tool";
        public int StartCount { get; private set; }
        public int FocusCount { get; private set; }
        public int CloseCount { get; private set; }
        public int WaitForMainWindowCount { get; private set; }
        public IntPtr AttachedParentHandle { get; private set; }
        public EmbeddedWindowBounds? LastBounds { get; private set; }

        public event EventHandler? Exited;

        public bool Start(ExternalApplicationDefinition definition)
        {
            StartCount++;
            IsRunning = StartResult;
            return StartResult;
        }

        public void Focus() => FocusCount++;

        public bool WaitForMainWindow(TimeSpan timeout)
        {
            WaitForMainWindowCount++;
            return IsRunning;
        }

        public bool AttachTo(IntPtr parentWindowHandle)
        {
            AttachedParentHandle = parentWindowHandle;
            return IsRunning;
        }

        public void Resize(EmbeddedWindowBounds bounds) => LastBounds = bounds;

        public void RaiseExited()
        {
            IsRunning = false;
            Exited?.Invoke(this, EventArgs.Empty);
        }

        public void Close()
        {
            CloseCount++;
            IsRunning = false;
        }

        public void Dispose()
        {
        }
    }
}
