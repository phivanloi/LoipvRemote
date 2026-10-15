using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
            (sample.CpuPercent is double cpuPercent && (!double.IsFinite(cpuPercent) || cpuPercent is < 0 or > 100)) ||
            sample.MemoryUsedBytes < 0 || sample.MemoryTotalBytes < 0 || sample.MemoryUsedBytes > sample.MemoryTotalBytes ||
            sample.DiskUsedBytes < 0 || sample.DiskTotalBytes < 0 || sample.DiskUsedBytes > sample.DiskTotalBytes ||
            sample.Disks?.Any(disk => string.IsNullOrWhiteSpace(disk.Name) ||
                                      disk.UsedBytes < 0 || disk.TotalBytes < 0 || disk.UsedBytes > disk.TotalBytes) == true ||
            sample.ReceiveBytesPerSecond is < 0 || sample.TransmitBytesPerSecond is < 0 || sample.UptimeSeconds < 0)
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
            TimeSpan.FromSeconds(sample.UptimeSeconds),
            sample.Disks is { Length: > 0 }
                ? sample.Disks
                    .Select(disk => new RemoteDiskSnapshot(disk.Name, disk.UsedBytes, disk.TotalBytes))
                    .ToArray()
                : null);
    }

    private sealed record WindowsResourceSample(
        double? CpuPercent,
        long MemoryUsedBytes,
        long MemoryTotalBytes,
        long DiskUsedBytes,
        long DiskTotalBytes,
        long? ReceiveBytesPerSecond,
        long? TransmitBytesPerSecond,
        long UptimeSeconds,
        WindowsDiskSample[]? Disks);

    private sealed record WindowsDiskSample(string Name, long UsedBytes, long TotalBytes);
}

/// <summary>Collects Windows metrics over a separate CIM channel without touching the RDP session.</summary>
public sealed class WindowsCimResourceCollector : IWindowsResourceCollector
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan OptionalCounterTimeout = TimeSpan.FromSeconds(20);
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

        TimeSpan optionalTimeout = _timeout < OptionalCounterTimeout ? _timeout : OptionalCounterTimeout;
        Task<string> operatingSystemProbe = RunProbeAsync("operating-system", _timeout, cancellationToken);
        Task<string?> diskProbe = TryRunOptionalProbeAsync("disks", _timeout, cancellationToken);
        Task<string?> cpuProbe = TryRunOptionalProbeAsync("cpu", optionalTimeout, cancellationToken);
        Task<string?> networkProbe = TryRunOptionalProbeAsync("network", optionalTimeout, cancellationToken);

        string operatingSystem = await operatingSystemProbe.ConfigureAwait(false);
        string? disks = await diskProbe.ConfigureAwait(false);
        string? cpu = await cpuProbe.ConfigureAwait(false);
        string? network = await networkProbe.ConfigureAwait(false);
        return WindowsResourceSampleParser.Parse(MergeProbeOutputs(operatingSystem, disks, cpu, network));
    }

    private async Task<string?> TryRunOptionalProbeAsync(
        string query,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            return await RunProbeAsync(query, timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException or FormatException)
        {
            return null;
        }
    }

    private async Task<string> RunProbeAsync(
        string query,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
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
        startInfo.Environment["LOIPVREMOTE_MONITOR_QUERY"] = query;

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("Unable to start the Windows resource probe.");

        await process.StandardInput.WriteLineAsync(_connection.Password ?? string.Empty).ConfigureAwait(false);
        process.StandardInput.Close();
        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(timeout);
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

            if (string.IsNullOrWhiteSpace(output))
                throw new FormatException($"The Windows {query} resource probe returned no data.");
            return output;
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

    private static string MergeProbeOutputs(params string?[] outputs)
    {
        var merged = new JsonObject();
        foreach (string output in outputs.OfType<string>())
        {
            string? json = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault(line => line.StartsWith('{') && line.EndsWith('}'));
            if (json is null || JsonNode.Parse(json) is not JsonObject probe)
                continue;

            foreach ((string propertyName, JsonNode? value) in probe)
                merged[propertyName] = value?.DeepClone();
        }

        return merged.ToJsonString();
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
        $query = $env:LOIPVREMOTE_MONITOR_QUERY
        $password = [Console]::In.ReadLine()
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
                return New-CimSession -ComputerName $computer -Authentication Negotiate -OperationTimeoutSec 8
            } else {
                return New-CimSession -ComputerName $computer -Credential $credential -Authentication Negotiate -OperationTimeoutSec 8
            }
        }

        function New-DcomMonitorSession {
            $dcomOption = New-CimSessionOption -Protocol Dcom
            if ($null -eq $credential) {
                return New-CimSession -ComputerName $computer -SessionOption $dcomOption -OperationTimeoutSec 8
            } else {
                return New-CimSession -ComputerName $computer -Credential $credential -SessionOption $dcomOption -OperationTimeoutSec 8
            }
        }

        function New-MonitorCimSession {
            $parsedAddress = $null
            $preferDcom = [System.Net.IPAddress]::TryParse($computer, [ref]$parsedAddress)
            if ($preferDcom) {
                # WinRM Negotiate rejects IP targets unless TrustedHosts is
                # configured. Prefer DCOM so monitoring needs no client-side
                # security-setting change, while retaining WinRM as fallback.
                try {
                    return New-DcomMonitorSession
                } catch {
                    return New-WsManMonitorSession
                }
            } else {
                try {
                    return New-WsManMonitorSession
                } catch {
                    return New-DcomMonitorSession
                }
            }
        }

        function New-ClassicWmiScope {
            $options = [System.Management.ConnectionOptions]::new()
            $options.Authentication = [System.Management.AuthenticationLevel]::PacketPrivacy
            $options.Impersonation = [System.Management.ImpersonationLevel]::Impersonate
            $options.Timeout = [TimeSpan]::FromSeconds(8)
            if ($null -ne $credential) {
                $options.Username = $credential.UserName
                $options.SecurePassword = $credential.Password
            }
            $scope = [System.Management.ManagementScope]::new("\\$computer\root\cimv2", $options)
            $scope.Connect()
            return $scope
        }

        function Invoke-ClassicWmiQuery([System.Management.ManagementScope]$scope, [string]$wql) {
            $queryObject = [System.Management.ObjectQuery]::new($wql)
            $enumeration = [System.Management.EnumerationOptions]::new()
            $enumeration.Timeout = [TimeSpan]::FromSeconds(8)
            $searcher = [System.Management.ManagementObjectSearcher]::new($scope, $queryObject, $enumeration)
            return @($searcher.Get())
        }

        switch ($query) {
            'operating-system' {
                $session = New-MonitorCimSession
                $os = Get-CimInstance -CimSession $session -ClassName Win32_OperatingSystem -OperationTimeoutSec 12
                $memoryTotal = [int64]$os.TotalVisibleMemorySize * 1024
                $memoryFree = [int64]$os.FreePhysicalMemory * 1024
                $uptime = [math]::Max(0, [int64]((Get-Date) - [datetime]$os.LastBootUpTime).TotalSeconds)
                [ordered]@{
                    memoryUsedBytes = $memoryTotal - $memoryFree
                    memoryTotalBytes = $memoryTotal
                    uptimeSeconds = $uptime
                } | ConvertTo-Json -Compress
            }
            'disks' {
                $session = New-MonitorCimSession
                $logicalDisks = @(Get-CimInstance -CimSession $session -ClassName Win32_LogicalDisk -Filter "DriveType=3" -OperationTimeoutSec 12)
                $diskTotal = [int64]0
                $diskUsed = [int64]0
                $diskDetails = @($logicalDisks | ForEach-Object {
                    $size = if ($null -eq $_.Size) { 0 } else { [int64]$_.Size }
                    $free = if ($null -eq $_.FreeSpace) { 0 } else { [int64]$_.FreeSpace }
                    $used = $size - $free
                    $diskTotal += $size
                    $diskUsed += $used
                    [pscustomobject][ordered]@{
                        name = [string]$_.DeviceID
                        usedBytes = $used
                        totalBytes = $size
                    }
                })
                [ordered]@{
                    diskUsedBytes = $diskUsed
                    diskTotalBytes = $diskTotal
                    disks = @($diskDetails)
                } | ConvertTo-Json -Depth 4 -Compress
            }
            'cpu' {
                try {
                    $scope = New-ClassicWmiScope
                    $cpu = @(Invoke-ClassicWmiQuery $scope "SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'") | Select-Object -First 1
                    if ($null -eq $cpu) { throw 'The classic WMI CPU counter returned no rows.' }
                } catch {
                    $session = New-MonitorCimSession
                    $cpu = Get-CimInstance -CimSession $session -ClassName Win32_PerfFormattedData_PerfOS_Processor -Filter "Name='_Total'" -OperationTimeoutSec 8 | Select-Object -First 1
                }
                [ordered]@{
                    cpuPercent = [double]$cpu.PercentProcessorTime
                } | ConvertTo-Json -Compress
            }
            'network' {
                try {
                    $scope = New-ClassicWmiScope
                    $network = @(Invoke-ClassicWmiQuery $scope 'SELECT BytesReceivedPersec,BytesSentPersec FROM Win32_PerfFormattedData_Tcpip_NetworkInterface')
                    if ($network.Count -eq 0) { throw 'The classic WMI network counter returned no rows.' }
                } catch {
                    $session = New-MonitorCimSession
                    $network = @(Get-CimInstance -CimSession $session -ClassName Win32_PerfFormattedData_Tcpip_NetworkInterface -OperationTimeoutSec 8)
                }
                $receive = ($network | Measure-Object -Property BytesReceivedPersec -Sum).Sum
                $transmit = ($network | Measure-Object -Property BytesSentPersec -Sum).Sum
                if ($null -eq $receive) { $receive = 0 }
                if ($null -eq $transmit) { $transmit = 0 }
                [ordered]@{
                    receiveBytesPerSecond = [int64]$receive
                    transmitBytesPerSecond = [int64]$transmit
                } | ConvertTo-Json -Compress
            }
            default {
                throw "Unsupported Windows resource query: $query"
            }
        }
        """;
}

public sealed class RdpResourceMonitor : IRemoteResourceMonitor
{
    private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(15);
    private const int MaxRetainedPerformanceCounterMisses = 1;
    private const int MaxRetainedCollectorFailures = 1;
    private readonly IWindowsResourceCollector _collector;
    private readonly TimeSpan _pollInterval;
    private readonly object _sync = new();
    private CancellationTokenSource? _cancellation;
    private bool _isActive;
    private bool _disposed;
    private int _collectorFailures;
    private int _cpuCounterMisses;
    private int _networkCounterMisses;
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
                RemoteResourceSnapshot collected = await _collector.CollectAsync(cancellationToken).ConfigureAwait(false);
                RemoteResourceSnapshot snapshot = MergeTransientPerformanceCounterMisses(collected, out bool retainedPerformanceSample);
                PublishSnapshot(snapshot);
                string statusMessage = retainedPerformanceSample
                    ? "Monitoring Windows resources over CIM/WMI; retaining the last performance sample during a transient retry"
                    : snapshot.CpuPercent is null ||
                                       snapshot.ReceiveBytesPerSecond is null ||
                                       snapshot.TransmitBytesPerSecond is null
                    ? "Monitoring available Windows resources over CIM/WMI; unavailable performance counters show --"
                    : "Monitoring Windows resources over CIM/WMI (WinRM/DCOM)";
                PublishStatus(new(RemoteResourceMonitorState.Monitoring, statusMessage));
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
                if (TryRetainSnapshotAfterTransientCollectorFailure())
                {
                    PublishStatus(new(
                        RemoteResourceMonitorState.Monitoring,
                        "Monitoring Windows resources over CIM/WMI; retaining the last resource snapshot during a transient retry"));
                }
                else
                {
                    ClearSnapshot();
                    PublishStatus(new(RemoteResourceMonitorState.Unavailable, "Windows resource monitoring is unavailable"));
                }
                await DelayAsync(RetryInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private bool TryRetainSnapshotAfterTransientCollectorFailure()
    {
        lock (_sync)
        {
            if (_lastSnapshot is null || _collectorFailures >= MaxRetainedCollectorFailures)
                return false;

            _collectorFailures++;
            return true;
        }
    }

    private RemoteResourceSnapshot MergeTransientPerformanceCounterMisses(
        RemoteResourceSnapshot collected,
        out bool retainedPerformanceSample)
    {
        double? cpu = collected.CpuPercent;
        long? receive = collected.ReceiveBytesPerSecond;
        long? transmit = collected.TransmitBytesPerSecond;
        retainedPerformanceSample = false;

        lock (_sync)
        {
            RemoteResourceSnapshot? previous = _lastSnapshot;
            if (cpu is not null)
            {
                _cpuCounterMisses = 0;
            }
            else if (previous?.CpuPercent is not null && _cpuCounterMisses < MaxRetainedPerformanceCounterMisses)
            {
                cpu = previous.CpuPercent;
                _cpuCounterMisses++;
                retainedPerformanceSample = true;
            }
            else
            {
                _cpuCounterMisses++;
            }

            if (receive is not null && transmit is not null)
            {
                _networkCounterMisses = 0;
            }
            else if (previous?.ReceiveBytesPerSecond is not null &&
                     previous.TransmitBytesPerSecond is not null &&
                     _networkCounterMisses < MaxRetainedPerformanceCounterMisses)
            {
                receive ??= previous.ReceiveBytesPerSecond;
                transmit ??= previous.TransmitBytesPerSecond;
                _networkCounterMisses++;
                retainedPerformanceSample = true;
            }
            else
            {
                _networkCounterMisses++;
            }
        }

        if (cpu == collected.CpuPercent &&
            receive == collected.ReceiveBytesPerSecond &&
            transmit == collected.TransmitBytesPerSecond)
        {
            return collected;
        }

        return new RemoteResourceSnapshot(
            cpu,
            collected.MemoryUsedBytes,
            collected.MemoryTotalBytes,
            collected.DiskPercent,
            collected.DiskUsedBytes,
            collected.DiskTotalBytes,
            receive,
            transmit,
            collected.Uptime,
            collected.Disks);
    }

    private bool IsActive()
    {
        lock (_sync) return _isActive;
    }

    private void PublishSnapshot(RemoteResourceSnapshot snapshot)
    {
        lock (_sync)
        {
            _lastSnapshot = snapshot;
            _collectorFailures = 0;
        }
        SnapshotUpdated?.Invoke(snapshot);
    }

    private void ClearSnapshot()
    {
        lock (_sync)
        {
            _lastSnapshot = null;
            _collectorFailures = 0;
            _cpuCounterMisses = 0;
            _networkCounterMisses = 0;
        }
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
