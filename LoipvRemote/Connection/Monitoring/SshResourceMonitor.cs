using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;

namespace LoipvRemote.Connection.Monitoring
{
    public enum RemoteResourceMonitorState
    {
        WaitingForActiveTab,
        Connecting,
        Monitoring,
        AuthenticationUnavailable,
        Unavailable
    }

    public sealed record RemoteResourceMonitorStatus(RemoteResourceMonitorState State, string Message, string? Fingerprint = null);

    /// <summary>
    /// Collects Linux resource counters through a separate, session-scoped SSH channel.
    /// The monitor never reuses terminal input and never persists secrets or host keys.
    /// </summary>
    public sealed class SshResourceMonitor : IDisposable
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(3);
        private readonly ConnectionInfo _connection;
        private readonly object _sync = new();
        private CancellationTokenSource? _cancellation;
        private SshClient? _client;
        private LinuxResourceSample? _previousSample;
        private DateTimeOffset _previousSampleAt;
        private bool _isActive;

        public event Action<RemoteResourceSnapshot>? SnapshotUpdated;
        public event Action<RemoteResourceMonitorStatus>? StatusChanged;

        public SshResourceMonitor(ConnectionInfo connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public void Start()
        {
            lock (_sync)
            {
                if (_cancellation is not null) return;
                // A newly opened connection tab is focused by ConnectionWindow before
                // its protocol raises Connected. Start its first sample immediately;
                // ConnectionWindow pauses it again when another tab becomes active.
                _isActive = true;
                _cancellation = new CancellationTokenSource();
                _ = Task.Run(() => RunAsync(_cancellation.Token));
            }
        }

        public void SetIsActive(bool isActive)
        {
            _isActive = isActive;
            if (!isActive)
                PublishStatus(new(RemoteResourceMonitorState.WaitingForActiveTab,
                    RemoteResourceText.Get("RemoteResourceStatusPaused", "SSH resource monitoring paused")));
        }

        public void Stop()
        {
            lock (_sync)
            {
                _cancellation?.Cancel();
                DisconnectClient();
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!_isActive)
                {
                    await DelayAsync(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    LinuxResourceSample sample = await CollectAsync(cancellationToken).ConfigureAwait(false);
                    DateTimeOffset now = DateTimeOffset.UtcNow;
                    TimeSpan elapsed = _previousSample is null ? TimeSpan.Zero : now - _previousSampleAt;
                    RemoteResourceSnapshot snapshot = RemoteResourceSnapshotCalculator.Calculate(sample, _previousSample, elapsed);
                    _previousSample = sample;
                    _previousSampleAt = now;
                    PublishStatus(new(RemoteResourceMonitorState.Monitoring,
                        RemoteResourceText.Get("RemoteResourceStatusMonitoring", "Monitoring remote resources")));
                    SnapshotUpdated?.Invoke(snapshot);
                    await DelayAsync(PollInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (HostKeyNotTrustedException)
                {
                    PublishStatus(new(RemoteResourceMonitorState.Unavailable,
                        RemoteResourceText.Get("RemoteResourceStatusHostKeyUnavailable",
                            "PuTTYNG has no trusted host key for SSH monitoring")));
                    await DelayAsync(PollInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (AuthenticationUnavailableException ex)
                {
                    PublishStatus(new(RemoteResourceMonitorState.AuthenticationUnavailable, ex.Message));
                    await DelayAsync(PollInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch
                {
                    // Do not expose host details, credentials, or remote command output in the UI/logs.
                    DisconnectClient();
                    PublishStatus(new(RemoteResourceMonitorState.Unavailable,
                        RemoteResourceText.Get("RemoteResourceStatusUnavailable", "Unable to retrieve SSH metrics")));
                    await DelayAsync(PollInterval, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private async Task<LinuxResourceSample> CollectAsync(CancellationToken cancellationToken)
        {
            EnsurePasswordAuthenticationAvailable();
            SshClient client = GetOrConnectClient(cancellationToken);
            using SshCommand command = client.CreateCommand(LinuxResourceProbe.Command);
            command.CommandTimeout = CommandTimeout;
            string output = await Task.Run(command.Execute, cancellationToken).ConfigureAwait(false);
            if (command.ExitStatus != 0)
                throw new InvalidOperationException("The Linux resource probe failed.");

            return LinuxResourceSampleParser.Parse(output);
        }

        private SshClient GetOrConnectClient(CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                if (_client?.IsConnected == true) return _client;

                DisconnectClient();
                PublishStatus(new(RemoteResourceMonitorState.Connecting,
                    RemoteResourceText.Get("RemoteResourceStatusConnecting", "Connecting SSH monitoring channel")));
                Renci.SshNet.ConnectionInfo connectionInfo = new(
                    _connection.Hostname,
                    _connection.Port,
                    _connection.Username,
                    new PasswordAuthenticationMethod(_connection.Username, _connection.Password));
                PuttyHostKeyTrustStore.PreferCachedHostKeyAlgorithms(connectionInfo, _connection.Hostname, _connection.Port);
                SshClient client = new(connectionInfo);
                bool hostKeyRejected = false;
                client.HostKeyReceived += (_, eventArgs) =>
                {
                    eventArgs.CanTrust = PuttyHostKeyTrustStore.IsTrusted(
                        _connection.Hostname,
                        _connection.Port,
                        eventArgs.HostKeyName,
                        eventArgs.HostKey);
                    hostKeyRejected = !eventArgs.CanTrust;
                };

                try
                {
                    Task.Run(client.Connect, cancellationToken).GetAwaiter().GetResult();
                }
                catch
                {
                    if (hostKeyRejected)
                    {
                        client.Dispose();
                        throw new HostKeyNotTrustedException();
                    }

                    client.Dispose();
                    throw;
                }

                _client = client;
                return client;
            }
        }

        private void EnsurePasswordAuthenticationAvailable()
        {
            if (string.IsNullOrWhiteSpace(_connection.Username) || string.IsNullOrEmpty(_connection.Password))
            {
                throw new AuthenticationUnavailableException(
                    RemoteResourceText.Get("RemoteResourceStatusAuthenticationUnavailable",
                        "SSH monitoring requires a direct connection username and password"));
            }
        }

        private void PublishStatus(RemoteResourceMonitorStatus status) => StatusChanged?.Invoke(status);

        private static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
            Task.Delay(delay, cancellationToken);

        private void DisconnectClient()
        {
            if (_client is null) return;
            try
            {
                if (_client.IsConnected) _client.Disconnect();
            }
            finally
            {
                _client.Dispose();
                _client = null;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                Stop();
                _cancellation?.Dispose();
                _cancellation = null;
            }
        }

        private sealed class HostKeyNotTrustedException : Exception;

        private sealed class AuthenticationUnavailableException(string message) : Exception(message);
    }
}
