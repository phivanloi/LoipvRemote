using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;
using Renci.SshNet;

namespace LoipvRemote.Protocols.Putty;

/// <summary>Collects a Linux sample without exposing transport details to the UI.</summary>
public interface ILinuxResourceCollector : IDisposable
{
    Task<LinuxResourceSample> CollectAsync(CancellationToken cancellationToken);
}

public sealed class SshResourceMonitor : IRemoteResourceMonitor
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);
    private readonly ILinuxResourceCollector _collector;
    private readonly TimeSpan _pollInterval;
    private readonly object _sync = new();
    private CancellationTokenSource? _cancellation;
    private LinuxResourceSample? _previousSample;
    private DateTimeOffset _previousSampleAt;
    private bool _isActive;
    private bool _disposed;
    private RemoteResourceSnapshot? _lastSnapshot;
    private RemoteResourceMonitorStatus _lastStatus = new(RemoteResourceMonitorState.WaitingForActiveTab, "SSH resource monitoring paused");

    public SshResourceMonitor(ILinuxResourceCollector collector, TimeSpan? pollInterval = null)
    {
        _collector = collector ?? throw new ArgumentNullException(nameof(collector));
        _pollInterval = pollInterval ?? DefaultPollInterval;
        if (_pollInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(pollInterval));
    }

    public event Action<RemoteResourceSnapshot>? SnapshotUpdated;
    public event Action<RemoteResourceMonitorStatus>? StatusChanged;

    public RemoteResourceSnapshot? LastSnapshot
    {
        get
        {
            lock (_sync)
                return _lastSnapshot;
        }
    }

    public RemoteResourceMonitorStatus LastStatus
    {
        get
        {
            lock (_sync)
                return _lastStatus;
        }
    }

    public void Start()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_cancellation is not null)
                return;

            _isActive = true;
            _cancellation = new CancellationTokenSource();
            _ = Task.Run(() => RunAsync(_cancellation.Token));
        }
    }

    public void SetIsActive(bool isActive)
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _isActive = isActive;
        }

        if (!isActive)
            PublishStatus(new(RemoteResourceMonitorState.WaitingForActiveTab, "SSH resource monitoring paused"));
    }

    public void StopMonitoring()
    {
        CancellationTokenSource? cancellation;
        lock (_sync)
        {
            cancellation = _cancellation;
            _cancellation = null;
            _isActive = false;
        }

        cancellation?.Cancel();
        cancellation?.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!IsActive())
            {
                await DelayAsync(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                if (IsFirstSample())
                    PublishStatus(new(RemoteResourceMonitorState.Connecting, "Connecting SSH monitoring channel"));
                LinuxResourceSample sample = await _collector.CollectAsync(cancellationToken).ConfigureAwait(false);
                DateTimeOffset now = DateTimeOffset.UtcNow;
                RemoteResourceSnapshot snapshot;
                lock (_sync)
                {
                    TimeSpan elapsed = _previousSample is null ? TimeSpan.Zero : now - _previousSampleAt;
                    snapshot = RemoteResourceSnapshotCalculator.Calculate(sample, _previousSample, elapsed);
                    _previousSample = sample;
                    _previousSampleAt = now;
                    _lastSnapshot = snapshot;
                }

                PublishStatus(new(RemoteResourceMonitorState.Monitoring, "Monitoring remote resources"));
                SnapshotUpdated?.Invoke(snapshot);
                await DelayAsync(_pollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (SshResourceMonitorAuthenticationException)
            {
                PublishStatus(new(RemoteResourceMonitorState.AuthenticationUnavailable,
                    "SSH monitoring requires a direct username and password"));
                await DelayAsync(_pollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (SshResourceMonitorHostKeyException)
            {
                PublishStatus(new(RemoteResourceMonitorState.Unavailable,
                    "SSH host key has not been trusted in PuTTY"));
                await DelayAsync(_pollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                string message = exception is FormatException or InvalidOperationException
                    ? $"Unable to retrieve SSH metrics: {exception.Message}"
                    : "Unable to retrieve SSH metrics";
                PublishStatus(new(RemoteResourceMonitorState.Unavailable, message));
                await DelayAsync(_pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private bool IsActive()
    {
        lock (_sync)
            return !_disposed && _isActive;
    }

    private bool IsFirstSample()
    {
        lock (_sync)
            return _previousSample is null;
    }

    private void PublishStatus(RemoteResourceMonitorStatus status)
    {
        lock (_sync)
            _lastStatus = status;
        StatusChanged?.Invoke(status);
    }

    private static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        StopMonitoring();
        _collector.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed record SshResourceMonitorConnection(string Host, int Port, string Username, string? Password)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new ArgumentException("An SSH monitor host is required.", nameof(Host));
        if (Port is <= 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(Port));
    }
}

/// <summary>Creates the non-interactive SSH collector for a saved SSH connection.</summary>
public sealed class SshResourceMonitorFactory(Func<ConnectionDefinition, string?> passwordResolver)
{
    private readonly Func<ConnectionDefinition, string?> _passwordResolver = passwordResolver ?? throw new ArgumentNullException(nameof(passwordResolver));

    public IRemoteResourceMonitor? Create(ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Protocol != ProtocolKind.Ssh2)
            return null;

        string username = definition.Options?.Values.TryGetValue("Username", out string? value) == true ? value : string.Empty;
        var connection = new SshResourceMonitorConnection(
            definition.Host,
            definition.Port,
            username,
            _passwordResolver(definition));
        return new SshResourceMonitor(new SshNetLinuxResourceCollector(connection));
    }
}

internal sealed class SshResourceMonitorAuthenticationException : Exception;
internal sealed class SshResourceMonitorHostKeyException : Exception;

internal sealed class SshNetLinuxResourceCollector : ILinuxResourceCollector
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(3);
    private readonly SshResourceMonitorConnection _connection;
    private readonly object _sync = new();
    private SshClient? _client;
    private bool _disposed;

    public SshNetLinuxResourceCollector(SshResourceMonitorConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _connection.Validate();
    }

    public async Task<LinuxResourceSample> CollectAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connection.Username) || string.IsNullOrEmpty(_connection.Password))
            throw new SshResourceMonitorAuthenticationException();

        SshClient client = GetOrConnectClient(cancellationToken);
        using SshCommand command = client.CreateCommand(LinuxResourceProbe.Command);
        command.CommandTimeout = CommandTimeout;
        string output = await Task.Run(command.Execute, cancellationToken).ConfigureAwait(false);
        if (command.ExitStatus != 0)
        {
            string error = string.Concat(command.Error
                    .Where(character => !char.IsControl(character) || character == ' '))
                .Trim();
            if (error.Length > 160)
                error = error[..160];
            throw new InvalidOperationException(
                error.Length == 0
                    ? "The Linux resource probe failed."
                    : $"The Linux resource probe failed: {error}");
        }

        return LinuxResourceSampleParser.Parse(output);
    }

    private SshClient GetOrConnectClient(CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_client?.IsConnected == true)
                return _client;

            DisconnectClient();
            var connectionInfo = new Renci.SshNet.ConnectionInfo(
                _connection.Host,
                _connection.Port,
                _connection.Username,
                new PasswordAuthenticationMethod(_connection.Username, _connection.Password));
            PuttyHostKeyTrustStore.PreferCachedHostKeyAlgorithms(connectionInfo, _connection.Host, _connection.Port);
            var client = new SshClient(connectionInfo);
            bool hostKeyRejected = false;
            client.HostKeyReceived += (_, eventArgs) =>
            {
                eventArgs.CanTrust = PuttyHostKeyTrustStore.IsTrusted(
                    _connection.Host,
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
                client.Dispose();
                if (hostKeyRejected)
                    throw new SshResourceMonitorHostKeyException();
                throw;
            }

            _client = client;
            return client;
        }
    }

    private void DisconnectClient()
    {
        if (_client is null)
            return;

        try
        {
            if (_client.IsConnected)
                _client.Disconnect();
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
            if (_disposed)
                return;
            _disposed = true;
            DisconnectClient();
        }
    }
}

internal static class LinuxResourceProbe
{
    // The command is fixed application code. Connection fields are never interpolated into it.
    internal const string Command = @"LC_ALL=C sh -c '
cpu=""$(awk ""NR==1 {idle=\$5+\$6; total=0; for(i=2;i<=NF;i++) total+=\$i; print total, idle}"" /proc/stat)""
set -- $cpu
cpu_total=$1
cpu_idle=$2
mem_total=""$(awk ""/^MemTotal:/ {print \$2*1024}"" /proc/meminfo)""
mem_available=""$(awk ""/^MemAvailable:/ {print \$2*1024; found=1} END {if(!found) print 0}"" /proc/meminfo)""
disk=""$(df -Pk / | awk ""NR==2 {print \$2*1024, \$3*1024}"" )""
set -- $disk
disk_total=$1
disk_used=$2
disk_table=""$(df -Pk -x tmpfs -x devtmpfs -x squashfs -x overlay 2>/dev/null || df -Pk)""
network=""$(awk ""NR>2 {iface=\$1; sub(/:$/, x, iface); if(iface !~ /^lo$/) {rx += \$2; tx += \$10}} END {print rx+0, tx+0}"" /proc/net/dev)""
set -- $network
net_rx=$1
net_tx=$2
uptime_seconds=""$(awk ""{print int(\$1)}"" /proc/uptime)""
printf ""cpu_total=%s\n"" ""$cpu_total""
printf ""cpu_idle=%s\n"" ""$cpu_idle""
printf ""mem_total=%s\n"" ""$mem_total""
printf ""mem_available=%s\n"" ""$mem_available""
printf ""disk_total=%s\n"" ""$disk_total""
printf ""disk_used=%s\n"" ""$disk_used""
printf ""disk_table_begin\n%s\ndisk_table_end\n"" ""$disk_table""
printf ""net_rx=%s\n"" ""$net_rx""
printf ""net_tx=%s\n"" ""$net_tx""
printf ""uptime_seconds=%s\n"" ""$uptime_seconds""
';";
}
