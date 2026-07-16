using Renci.SshNet;

namespace LoipvRemote.Protocols.Putty.Monitoring;

/// <summary>Protocol-owned SSH metrics channel, isolated from terminal input and WinForms.</summary>
public sealed class PuttyResourceMonitor : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(3);
    private readonly PuttyMonitoringConnection _connection;
    private readonly IPuttyHostKeyTrustStore _hostKeyTrustStore;
    private readonly object _sync = new();
    private CancellationTokenSource? _cancellation;
    private SshClient? _client;
    private LinuxResourceSample? _previousSample;
    private DateTimeOffset _previousSampleAt;
    private bool _isActive;

    public event Action<RemoteResourceSnapshot>? SnapshotUpdated;
    public event Action<PuttyResourceMonitorStatus>? StatusChanged;

    public PuttyResourceMonitor(PuttyMonitoringConnection connection, IPuttyHostKeyTrustStore hostKeyTrustStore)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _hostKeyTrustStore = hostKeyTrustStore ?? throw new ArgumentNullException(nameof(hostKeyTrustStore));
    }

    public void Start()
    {
        lock (_sync)
        {
            if (_cancellation is not null) return;
            _isActive = true;
            _cancellation = new CancellationTokenSource();
            _ = Task.Run(() => RunAsync(_cancellation.Token));
        }
    }

    public void SetIsActive(bool isActive)
    {
        _isActive = isActive;
        if (!isActive)
            PublishStatus(new(PuttyResourceMonitorState.WaitingForActiveTab, "SSH resource monitoring paused"));
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
                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
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
                PublishStatus(new(PuttyResourceMonitorState.Monitoring, "Monitoring remote resources"));
                SnapshotUpdated?.Invoke(snapshot);
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (HostKeyNotTrustedException)
            {
                PublishStatus(new(PuttyResourceMonitorState.Unavailable, "PuTTYNG has no trusted host key for SSH monitoring"));
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (AuthenticationUnavailableException ex)
            {
                PublishStatus(new(PuttyResourceMonitorState.AuthenticationUnavailable, ex.Message));
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                DisconnectClient();
                PublishStatus(new(PuttyResourceMonitorState.Unavailable, "Unable to retrieve SSH metrics"));
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task<LinuxResourceSample> CollectAsync(CancellationToken cancellationToken)
    {
        EnsurePasswordAuthenticationAvailable();
        SshClient client = await GetOrConnectClientAsync(cancellationToken).ConfigureAwait(false);
        using SshCommand command = client.CreateCommand(LinuxResourceProbe.Command);
        command.CommandTimeout = CommandTimeout;
        string output = await Task.Run(command.Execute, cancellationToken).ConfigureAwait(false);
        if (command.ExitStatus != 0)
            throw new InvalidOperationException("The Linux resource probe failed.");
        return LinuxResourceSampleParser.Parse(output);
    }

    private async Task<SshClient> GetOrConnectClientAsync(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            if (_client?.IsConnected == true) return _client;
        }

        DisconnectClient();
        PublishStatus(new(PuttyResourceMonitorState.Connecting, "Connecting SSH monitoring channel"));
        Renci.SshNet.ConnectionInfo connectionInfo = new(
            _connection.Hostname,
            _connection.Port,
            _connection.Username,
            new PasswordAuthenticationMethod(_connection.Username, _connection.Password));
        _hostKeyTrustStore.PreferCachedHostKeyAlgorithms(connectionInfo, _connection.Hostname, _connection.Port);
        SshClient client = new(connectionInfo);
        bool hostKeyRejected = false;
        client.HostKeyReceived += (_, eventArgs) =>
        {
            eventArgs.CanTrust = _hostKeyTrustStore.IsTrusted(
                _connection.Hostname, _connection.Port, eventArgs.HostKeyName, eventArgs.HostKey);
            hostKeyRejected = !eventArgs.CanTrust;
        };

        try
        {
            await Task.Run(client.Connect, cancellationToken).ConfigureAwait(false);
            lock (_sync)
            {
                _client = client;
                return client;
            }
        }
        catch
        {
            client.Dispose();
            if (hostKeyRejected) throw new HostKeyNotTrustedException();
            throw;
        }
    }

    private void EnsurePasswordAuthenticationAvailable()
    {
        if (string.IsNullOrWhiteSpace(_connection.Username) || string.IsNullOrEmpty(_connection.Password))
            throw new AuthenticationUnavailableException("SSH monitoring requires a direct connection username and password");
    }

    private void PublishStatus(PuttyResourceMonitorStatus status) => StatusChanged?.Invoke(status);

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
