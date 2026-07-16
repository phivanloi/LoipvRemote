using System.IO;
using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace LoipvRemote.Protocols.Putty.Transfers;

public enum PuttyFileTransferProtocol
{
    Scp,
    Sftp
}

public sealed record PuttyFileTransferRequest(
    string Hostname,
    int Port,
    string Username,
    string Password,
    string SourcePath,
    string DestinationPath,
    PuttyFileTransferProtocol Protocol);

/// <summary>Protocol-owned SCP/SFTP transfer; UI receives progress only.</summary>
public sealed class PuttyFileTransfer(PuttyFileTransferRequest request) : IDisposable
{
    private readonly PuttyFileTransferRequest _request = request ?? throw new ArgumentNullException(nameof(request));
    private bool _disposed;

    public event Action<long, long>? ProgressChanged;

    public async Task UploadAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        FileInfo source = new(_request.SourcePath);
        if (!source.Exists)
            throw new FileNotFoundException("The source file does not exist.", source.FullName);

        await Task.Run(() => UploadCore(source, cancellationToken), cancellationToken).ConfigureAwait(false);
    }

    private void UploadCore(FileInfo source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (_request.Protocol)
        {
            case PuttyFileTransferProtocol.Scp:
                using (ScpClient scp = new(_request.Hostname, _request.Port, _request.Username, _request.Password))
                {
                    scp.Uploading += (_, args) => ProgressChanged?.Invoke(ToProgressValue(args.Uploaded), ToProgressValue(args.Size));
                    scp.Connect();
                    try
                    {
                        scp.Upload(source, _request.DestinationPath);
                    }
                    finally
                    {
                        if (scp.IsConnected) scp.Disconnect();
                    }
                }
                break;

            case PuttyFileTransferProtocol.Sftp:
                using (SftpClient sftp = new(_request.Hostname, _request.Port, _request.Username, _request.Password))
                using (FileStream stream = source.OpenRead())
                {
                    sftp.Connect();
                    try
                    {
                        sftp.UploadFile(stream, _request.DestinationPath, uploaded =>
                            ProgressChanged?.Invoke(ToProgressValue(uploaded), source.Length));
                    }
                    finally
                    {
                        if (sftp.IsConnected) sftp.Disconnect();
                    }
                }
                break;

            default:
                throw new InvalidOperationException($"Unsupported transfer protocol: {_request.Protocol}.");
        }
    }

    private static long ToProgressValue(long value) => Math.Max(value, 0);
    private static long ToProgressValue(ulong value) => value > long.MaxValue ? long.MaxValue : (long)value;

    public void Dispose() => _disposed = true;
}
