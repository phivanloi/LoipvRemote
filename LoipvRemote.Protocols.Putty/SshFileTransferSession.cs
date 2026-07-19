using LoipvRemote.Domain.Connections;
using Renci.SshNet;

namespace LoipvRemote.Protocols.Putty;

public sealed record SshFileTransferEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    long Length,
    DateTime LastWriteTimeUtc)
{
    public string IconGlyph => IsDirectory ? "\uE8B7" : "\uE8A5";
    public string SizeText => IsDirectory ? string.Empty : FileTransferDisplay.FormatSize(Length);
    public string ModifiedText => LastWriteTimeUtc.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);
    public string DisplayText => IsDirectory
        ? $"[Folder] {Name}"
        : $"{Name}    {SizeText}";
}

public static class FileTransferDisplay
{
    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.0} {units[unit]}";
    }
}

public static class FileTransferName
{
    public static string CreateCollisionName(string fileName, int suffix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentOutOfRangeException.ThrowIfLessThan(suffix, 1);
        string extension = Path.GetExtension(fileName);
        string stem = Path.GetFileNameWithoutExtension(fileName);
        return $"{stem} ({suffix}){extension}";
    }
}

public interface ISshFileTransferSession : IAsyncDisposable
{
    string CurrentRemotePath { get; }
    Task<IReadOnlyList<SshFileTransferEntry>> ConnectAsync(string? initialPath, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SshFileTransferEntry>> ChangeDirectoryAsync(string path, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SshFileTransferEntry>> RefreshAsync(CancellationToken cancellationToken = default);
    Task<bool> FileExistsAsync(string remotePath, CancellationToken cancellationToken = default);
    Task CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken = default);
    Task RenameAsync(string oldRemotePath, string newRemotePath, CancellationToken cancellationToken = default);
    Task DeleteFileAsync(string remotePath, CancellationToken cancellationToken = default);
    Task DeleteDirectoryAsync(string remotePath, CancellationToken cancellationToken = default);
    Task UploadFileAsync(string localPath, string remotePath, CancellationToken cancellationToken = default);
    Task DownloadFileAsync(string remotePath, string localPath, CancellationToken cancellationToken = default);
}

public sealed class SshFileTransferSessionFactory(Func<ConnectionDefinition, string?> passwordResolver)
{
    private readonly Func<ConnectionDefinition, string?> _passwordResolver =
        passwordResolver ?? throw new ArgumentNullException(nameof(passwordResolver));

    public ISshFileTransferSession Create(ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Protocol != ProtocolKind.Ssh2)
            throw new NotSupportedException("SFTP is available only for SSH connections.");

        string username = definition.Options?.Values.TryGetValue("Username", out string? value) == true
            ? value
            : string.Empty;
        var connection = new SshResourceMonitorConnection(
            definition.Host,
            definition.Port,
            username,
            _passwordResolver(definition));
        return new SshNetFileTransferSession(connection);
    }
}

public static class SshRemotePath
{
    public static string ResolveInitial(string? requestedPath, string homePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homePath);
        if (string.IsNullOrWhiteSpace(requestedPath) || requestedPath == "~")
            return homePath;
        if (requestedPath.StartsWith("~/", StringComparison.Ordinal))
            return CombinePath(homePath, requestedPath[2..]);
        return requestedPath;
    }

    public static string Combine(string directory, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (fileName is "." or ".." || fileName.Contains('/') || fileName.Contains('\\'))
            throw new ArgumentException("A single file name without path traversal is required.", nameof(fileName));
        return CombinePath(directory, fileName);
    }

    public static string Parent(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path == "/")
            return path;
        int separator = path.TrimEnd('/').LastIndexOf('/');
        return separator <= 0 ? "/" : path[..separator];
    }

    private static string CombinePath(string directory, string child) =>
        directory == "/" ? $"/{child.TrimStart('/')}" : $"{directory.TrimEnd('/')}/{child.TrimStart('/')}";
}

internal sealed class SshNetFileTransferSession : ISshFileTransferSession
{
    private readonly SshResourceMonitorConnection _connection;
    private SftpClient? _client;
    private bool _disposed;

    public SshNetFileTransferSession(SshResourceMonitorConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _connection.Validate();
    }

    public string CurrentRemotePath => _client?.WorkingDirectory ?? string.Empty;

    public async Task<IReadOnlyList<SshFileTransferEntry>> ConnectAsync(
        string? initialPath,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(_connection.Username) || string.IsNullOrEmpty(_connection.Password))
            throw new InvalidOperationException("SFTP requires a direct SSH username and password.");

        if (_client?.IsConnected != true)
        {
            _client?.Dispose();
            var connectionInfo = new ConnectionInfo(
                _connection.Host,
                _connection.Port,
                _connection.Username,
                new PasswordAuthenticationMethod(_connection.Username, _connection.Password));
            PuttyHostKeyTrustStore.PreferCachedHostKeyAlgorithms(connectionInfo, _connection.Host, _connection.Port);
            var client = new SftpClient(connectionInfo);
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
                await Task.Run(client.Connect, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                client.Dispose();
                if (hostKeyRejected)
                    throw new InvalidOperationException("The SSH host key has not been trusted in PuTTY.");
                throw;
            }

            _client = client;
        }

        string homePath = _client.WorkingDirectory;
        string requestedPath = SshRemotePath.ResolveInitial(initialPath, homePath);
        try
        {
            await _client.ChangeDirectoryAsync(requestedPath, cancellationToken).ConfigureAwait(false);
        }
        catch when (!string.Equals(requestedPath, homePath, StringComparison.Ordinal))
        {
            await _client.ChangeDirectoryAsync(homePath, cancellationToken).ConfigureAwait(false);
        }

        return await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SshFileTransferEntry>> ChangeDirectoryAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        SftpClient client = GetConnectedClient();
        await client.ChangeDirectoryAsync(path, cancellationToken).ConfigureAwait(false);
        return await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SshFileTransferEntry>> RefreshAsync(CancellationToken cancellationToken = default)
    {
        SftpClient client = GetConnectedClient();
        var entries = new List<SshFileTransferEntry>();
        await foreach (Renci.SshNet.Sftp.ISftpFile file in client.ListDirectoryAsync(client.WorkingDirectory, cancellationToken))
        {
            if (file.Name is "." or "..")
                continue;
            entries.Add(new(file.Name, file.FullName, file.IsDirectory, file.Length, file.LastWriteTimeUtc));
        }

        return entries
            .OrderByDescending(entry => entry.IsDirectory)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<bool> FileExistsAsync(string remotePath, CancellationToken cancellationToken = default) =>
        GetConnectedClient().ExistsAsync(remotePath, cancellationToken);

    public Task CreateDirectoryAsync(string remotePath, CancellationToken cancellationToken = default) =>
        GetConnectedClient().CreateDirectoryAsync(remotePath, cancellationToken);

    public Task RenameAsync(
        string oldRemotePath,
        string newRemotePath,
        CancellationToken cancellationToken = default) =>
        GetConnectedClient().RenameFileAsync(oldRemotePath, newRemotePath, cancellationToken);

    public Task DeleteFileAsync(string remotePath, CancellationToken cancellationToken = default) =>
        GetConnectedClient().DeleteFileAsync(remotePath, cancellationToken);

    public Task DeleteDirectoryAsync(string remotePath, CancellationToken cancellationToken = default) =>
        GetConnectedClient().DeleteDirectoryAsync(remotePath, cancellationToken);

    public async Task UploadFileAsync(
        string localPath,
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        await using FileStream source = new(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
        await GetConnectedClient().UploadFileAsync(source, remotePath, cancellationToken).ConfigureAwait(false);
    }

    public async Task DownloadFileAsync(
        string remotePath,
        string localPath,
        CancellationToken cancellationToken = default)
    {
        await using FileStream destination = new(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, useAsync: true);
        await GetConnectedClient().DownloadFileAsync(remotePath, destination, cancellationToken).ConfigureAwait(false);
    }

    private SftpClient GetConnectedClient()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _client?.IsConnected == true
            ? _client
            : throw new InvalidOperationException("The SFTP session is not connected.");
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;
        if (_client is not null)
        {
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

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
