using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Rdp;

public sealed record RdpResourceMonitorConnection(string Host, string Username, string? Password);

public interface IWindowsResourceCollector : IDisposable
{
    Task<RemoteResourceSnapshot> CollectAsync(CancellationToken cancellationToken);
}

public static class WindowsResourceSampleParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static RemoteResourceSnapshot Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            throw new FormatException("The Windows resource probe returned no data.");

        string? json = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault(line => line.StartsWith('{') && line.EndsWith('}'));
        if (json is null)
            throw new FormatException("The Windows resource probe did not return JSON data.");

        WindowsResourceSample? sample;
        try
        {
            sample = JsonSerializer.Deserialize<WindowsResourceSample>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new FormatException("The Windows resource probe returned invalid JSON data.", exception);
        }

        if (sample is null ||
            !double.IsFinite(sample.CpuPercent) || sample.CpuPercent is < 0 or > 100 ||
            sample.MemoryUsedBytes < 0 || sample.MemoryTotalBytes < 0 || sample.MemoryUsedBytes > sample.MemoryTotalBytes ||
            sample.DiskUsedBytes < 0 || sample.DiskTotalBytes < 0 || sample.DiskUsedBytes > sample.DiskTotalBytes ||
            sample.ReceiveBytesPerSecond < 0 || sample.TransmitBytesPerSecond < 0 || sample.UptimeSeconds < 0)
        {
            throw new FormatException("The Windows resource probe returned inconsistent counters.");
        }

        double diskPercent = sample.DiskTotalBytes == 0
            ? 0
            : Math.Clamp(sample.DiskUsedBytes * 100d / sample.DiskTotalBytes, 0d, 100d);

        return new RemoteResourceSnapshot(
            sample.CpuPercent,
            sample.MemoryUsedBytes,
            sample.MemoryTotalBytes,
            diskPercent,
            sample.DiskUsedBytes,
            sample.DiskTotalBytes,
            sample.ReceiveBytesPerSecond,
            sample.TransmitBytesPerSecond,
            TimeSpan.FromSeconds(sample.UptimeSeconds));
    }

    private sealed record WindowsResourceSample(
        double CpuPercent,
        long MemoryUsedBytes,
        long MemoryTotalBytes,
        long DiskUsedBytes,
        long DiskTotalBytes,
        long ReceiveBytesPerSecond,
        long TransmitBytesPerSecond,
        long UptimeSeconds);
}

/// <summary>Collects Windows metrics over a separate CIM channel without touching the RDP session.</summary>
public sealed class WindowsCimResourceCollector : IWindowsResourceCollector
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(45);
    private static readonly string EncodedProbe = Convert.ToBase64String(Encoding.Unicode.GetBytes(ProbeScript));
    private readonly RdpResourceMonitorConnection _connection;
    private readonly TimeSpan _timeout;

    public WindowsCimResourceCollector(RdpResourceMonitorConnection connection, TimeSpan? timeout = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _timeout = timeout ?? DefaultTimeout;
        if (_timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    public async Task<RemoteResourceSnapshot> CollectAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows resource monitoring requires Windows.");
        if (!string.IsNullOrWhiteSpace(_connection.Username) && _connection.Password is null)
            throw new RemoteResourceMonitorAuthenticationException("The RDP password is unavailable for Windows resource monitoring.");

        string powerShell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = powerShell,
            Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {EncodedProbe}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        // A parent PowerShell 7 process can contribute an incompatible
        // PSModulePath. Let Windows PowerShell rebuild its own module path so
        // inbox modules such as CimCmdlets load reliably.
        startInfo.Environment.Remove("PSModulePath");
        startInfo.Environment["LOIPVREMOTE_MONITOR_HOST"] = _connection.Host;
        startInfo.Environment["LOIPVREMOTE_MONITOR_USERNAME"] = _connection.Username;

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("Unable to start the Windows resource probe.");

        await process.StandardInput.WriteLineAsync(_connection.Password ?? string.Empty).ConfigureAwait(false);
        process.StandardInput.Close();
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(_timeout);
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(timeoutCancellation.Token);
        Task<string> standardError = process.StandardError.ReadToEndAsync(timeoutCancellation.Token);
        try
        {
            await process.WaitForExitAsync(timeoutCancellation.Token).ConfigureAwait(false);
            string output = await standardOutput.ConfigureAwait(false);
            string error = await standardError.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                if (IsAuthenticationFailure(error))
                    throw new RemoteResourceMonitorAuthenticationException("Windows resource monitoring rejected the RDP credentials.");
                throw new InvalidOperationException("Windows resource monitoring is unavailable for this host.");
            }

            return WindowsResourceSampleParser.Parse(output);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException("The Windows resource probe timed out.");
        }
        finally
        {
            if (!process.HasExited)
                TryKill(process);
        }
    }

    public void Dispose()
    {
    }

    private static bool IsAuthenticationFailure(string error) =>
        error.Contains("Access is denied", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("Logon failure", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("credentials", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("0x80070005", StringComparison.OrdinalIgnoreCase);

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private const string ProbeScript = """
        $ErrorActionPreference = 'Stop'
        $ProgressPreference = 'SilentlyContinue'
        $computer = $env:LOIPVREMOTE_MONITOR_HOST
        $username = $env:LOIPVREMOTE_MONITOR_USERNAME
        $password = [Console]::In.ReadLine()
        $session = $null
        try {
            $credential = $null
            if (-not [string]::IsNullOrWhiteSpace($username)) {
                $securePassword = [System.Security.SecureString]::new()
                foreach ($character in $password.ToCharArray()) {
                    $securePassword.AppendChar($character)
                }
                $securePassword.MakeReadOnly()
                $credential = New-Object System.Management.Automation.PSCredential($username, $securePassword)
                $password = $null
            }

            function New-WsManMonitorSession {
                if ($null -eq $credential) {
                    return New-CimSession -ComputerName $computer -Authentication Negotiate
                } else {
                    return New-CimSession -ComputerName $computer -Credential $credential -Authentication Negotiate
                }
            }

            function New-DcomMonitorSession {
                $dcomOption = New-CimSessionOption -Protocol Dcom
                if ($null -eq $credential) {
                    return New-CimSession -ComputerName $computer -SessionOption $dcomOption
                } else {
                    return New-CimSession -ComputerName $computer -Credential $credential -SessionOption $dcomOption
                }
            }

            $parsedAddress = $null
            $preferDcom = [System.Net.IPAddress]::TryParse($computer, [ref]$parsedAddress)
            if ($preferDcom) {
                # WinRM Negotiate rejects IP targets unless TrustedHosts is
                # configured. Prefer DCOM so monitoring needs no client-side
                # security-setting change, while retaining WinRM as fallback.
                try {
                    $session = New-DcomMonitorSession
                } catch {
                    $session = New-WsManMonitorSession
                }
            } else {
                try {
                    $session = New-WsManMonitorSession
                } catch {
                    $session = New-DcomMonitorSession
                }
            }
            $os = Get-CimInstance -CimSession $session -ClassName Win32_OperatingSystem
            $cpu = Get-CimInstance -CimSession $session -ClassName Win32_PerfFormattedData_PerfOS_Processor -Filter "Name='_Total'" | Select-Object -First 1
            $diskFilter = "DeviceID='$($os.SystemDrive)'"
            $disk = Get-CimInstance -CimSession $session -ClassName Win32_LogicalDisk -Filter $diskFilter | Select-Object -First 1
            $network = @(Get-CimInstance -CimSession $session -ClassName Win32_PerfFormattedData_Tcpip_NetworkInterface -ErrorAction SilentlyContinue)
            $receive = ($network | Measure-Object -Property BytesReceivedPersec -Sum).Sum
            $transmit = ($network | Measure-Object -Property BytesSentPersec -Sum).Sum
            if ($null -eq $receive) { $receive = 0 }
            if ($null -eq $transmit) { $transmit = 0 }
            $memoryTotal = [int64]$os.TotalVisibleMemorySize * 1024
            $memoryFree = [int64]$os.FreePhysicalMemory * 1024
            $diskTotal = if ($null -eq $disk.Size) { 0 } else { [int64]$disk.Size }
            $diskFree = if ($null -eq $disk.FreeSpace) { 0 } else { [int64]$disk.FreeSpace }
            $uptime = [math]::Max(0, [int64]((Get-Date) - [datetime]$os.LastBootUpTime).TotalSeconds)
            [ordered]@{
                cpuPercent = [double]$cpu.PercentProcessorTime
                memoryUsedBytes = $memoryTotal - $memoryFree
                memoryTotalBytes = $memoryTotal
                diskUsedBytes = $diskTotal - $diskFree
                diskTotalBytes = $diskTotal
                receiveBytesPerSecond = [int64]$receive
                transmitBytesPerSecond = [int64]$transmit
                uptimeSeconds = $uptime
            } | ConvertTo-Json -Compress
        } finally {
            if ($null -ne $session) { Remove-CimSession -CimSession $session -ErrorAction SilentlyContinue }
        }
        """;
}

public sealed class RdpResourceMonitor : IRemoteResourceMonitor
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(15);
    private readonly IWindowsResourceCollector _collector;
    private readonly TimeSpan _pollInterval;
    private readonly object _sync = new();
    private CancellationTokenSource? _cancellation;
    private bool _isActive;
    private bool _disposed;
    private RemoteResourceSnapshot? _lastSnapshot;
    private RemoteResourceMonitorStatus _lastStatus = new(RemoteResourceMonitorState.WaitingForActiveTab, "RDP resource monitoring paused");

    public RdpResourceMonitor(IWindowsResourceCollector collector, TimeSpan? pollInterval = null)
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
        get { lock (_sync) return _lastSnapshot; }
    }

    public RemoteResourceMonitorStatus LastStatus
    {
        get { lock (_sync) return _lastStatus; }
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
            PublishStatus(new(RemoteResourceMonitorState.WaitingForActiveTab, "RDP resource monitoring paused"));
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
                if (LastSnapshot is null)
                    PublishStatus(new(RemoteResourceMonitorState.Connecting, "Connecting Windows resource monitoring channel"));
                RemoteResourceSnapshot snapshot = await _collector.CollectAsync(cancellationToken).ConfigureAwait(false);
                PublishSnapshot(snapshot);
                PublishStatus(new(RemoteResourceMonitorState.Monitoring, "Monitoring Windows resources over CIM (WinRM/DCOM)"));
                await DelayAsync(_pollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (RemoteResourceMonitorAuthenticationException)
            {
                ClearSnapshot();
                PublishStatus(new(RemoteResourceMonitorState.AuthenticationUnavailable, "RDP credentials are unavailable or rejected by Windows resource monitoring"));
                await DelayAsync(RetryInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                ClearSnapshot();
                PublishStatus(new(RemoteResourceMonitorState.Unavailable, "Windows resource monitoring is unavailable"));
                await DelayAsync(RetryInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private bool IsActive()
    {
        lock (_sync) return _isActive;
    }

    private void PublishSnapshot(RemoteResourceSnapshot snapshot)
    {
        lock (_sync) _lastSnapshot = snapshot;
        SnapshotUpdated?.Invoke(snapshot);
    }

    private void ClearSnapshot()
    {
        lock (_sync) _lastSnapshot = null;
    }

    private void PublishStatus(RemoteResourceMonitorStatus status)
    {
        lock (_sync)
        {
            if (_lastStatus == status)
                return;
            _lastStatus = status;
        }
        StatusChanged?.Invoke(status);
    }

    private static async Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

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
    }
}

public sealed class RdpResourceMonitorFactory
{
    private readonly Func<ConnectionDefinition, string?> _passwordResolver;
    private readonly Func<RdpResourceMonitorConnection, IWindowsResourceCollector> _collectorFactory;

    public RdpResourceMonitorFactory(
        Func<ConnectionDefinition, string?> passwordResolver,
        Func<RdpResourceMonitorConnection, IWindowsResourceCollector>? collectorFactory = null)
    {
        _passwordResolver = passwordResolver ?? throw new ArgumentNullException(nameof(passwordResolver));
        _collectorFactory = collectorFactory ?? (connection => new WindowsCimResourceCollector(connection));
    }

    public IRemoteResourceMonitor? Create(ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Protocol != ProtocolKind.Rdp)
            return null;

        string username = Option(definition.Options, "Username");
        string domain = Option(definition.Options, "Domain");
        if (!string.IsNullOrWhiteSpace(username) &&
            !string.IsNullOrWhiteSpace(domain) &&
            !username.Contains('\\', StringComparison.Ordinal) &&
            !username.Contains('@', StringComparison.Ordinal))
        {
            username = $"{domain}\\{username}";
        }

        var connection = new RdpResourceMonitorConnection(
            definition.Host,
            username,
            _passwordResolver(definition));
        return new RdpResourceMonitor(_collectorFactory(connection));
    }

    private static string Option(ConnectionNodeOptions? options, string name) =>
        options?.Values.TryGetValue(name, out string? value) == true ? value : string.Empty;
}

internal sealed class RemoteResourceMonitorAuthenticationException(string message) : Exception(message);
