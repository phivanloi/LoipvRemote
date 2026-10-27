using System.Net.Sockets;
using System.Security.Cryptography;
using LoipvRemote.WinUI.Sessions;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Sessions;

public sealed class SshReconnectRetryTests
{
    [Test]
    public async Task TransientFailureRetriesWithBackoffAndStopsAfterSuccess()
    {
        var calls = 0;
        var delays = new List<TimeSpan>();
        await SshReconnectRetry.RunAsync(_ =>
        {
            if (++calls < 3)
                throw new SocketException((int)SocketError.ConnectionRefused);
            return Task.CompletedTask;
        }, CancellationToken.None, (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });
        Assert.That(calls, Is.EqualTo(3));
        Assert.That(delays, Is.EqualTo(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4) }));
    }

    [Test]
    public void PersistentNetworkFailureStopsAfterThreeAttempts()
    {
        var calls = 0;
        Assert.ThrowsAsync<TimeoutException>(() => SshReconnectRetry.RunAsync(_ =>
        {
            calls++;
            throw new TimeoutException();
        }, CancellationToken.None, (_, _) => Task.CompletedTask));
        Assert.That(calls, Is.EqualTo(3));
    }

    [Test]
    public void CredentialFailureIsNotRetried()
    {
        var calls = 0;
        Assert.ThrowsAsync<CryptographicException>(() => SshReconnectRetry.RunAsync(_ =>
        {
            calls++;
            throw new CryptographicException();
        }, CancellationToken.None));
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void ClosingDuringBackoffPreventsAnotherAttempt()
    {
        using var cancellation = new CancellationTokenSource();
        var calls = 0;
        Assert.CatchAsync<OperationCanceledException>(() => SshReconnectRetry.RunAsync(_ =>
        {
            calls++;
            throw new TimeoutException();
        }, cancellation.Token, (_, token) =>
        {
            cancellation.Cancel();
            return Task.FromCanceled(token);
        }));
        Assert.That(calls, Is.EqualTo(1));
    }
}
