using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;

namespace LoipvRemote.Infrastructure.Windows.Process;

public static class WindowsSecretPipeServer
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public static string StartPassword(string prefix, string password)
    {
        string pipeName = $"{prefix}{Guid.NewGuid():N}";
        _ = Task.Run(async () =>
        {
            try { await WritePasswordAsync(pipeName, password).ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        });
        return pipeName;
    }

    public static async Task ServeVaultOtpAsync(
        string pipeName,
        string expectedUsername,
        string expectedHostname,
        int expectedPort,
        string password,
        CancellationToken cancellationToken)
    {
        using NamedPipeServerStream server = CreatePipeServer(pipeName, PipeDirection.InOut);
        await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
        using StreamReader reader = new(server, Utf8NoBom, false, 1024, leaveOpen: true);
        using StreamWriter writer = new(server, Utf8NoBom, 1024, leaveOpen: true) { AutoFlush = true };

        string? pingMessage = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (pingMessage != "ping")
            throw new FormatException("Invalid ping from VaultOpenbao SSH OTP plugin");
        await writer.WriteLineAsync("pong").ConfigureAwait(false);

        string dataRequest = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new FormatException("Invalid data request from VaultOpenbao SSH OTP plugin");
        (string username, string hostname, int port) = DeserializeRequest(dataRequest);
        if (username != expectedUsername || hostname != expectedHostname || port != expectedPort)
            throw new FormatException("Mismatched data request from VaultOpenbao SSH OTP plugin");

        await writer.WriteLineAsync(password).ConfigureAwait(false);
    }

    public static async Task ServeVaultOtpWithTimeoutAsync(
        string pipeName,
        string expectedUsername,
        string expectedHostname,
        int expectedPort,
        string password,
        TimeSpan timeout)
    {
        using CancellationTokenSource cancellation = new(timeout);
        await ServeVaultOtpAsync(
            pipeName,
            expectedUsername,
            expectedHostname,
            expectedPort,
            password,
            cancellation.Token).ConfigureAwait(false);
    }

    private static async Task WritePasswordAsync(string pipeName, string password)
    {
        using NamedPipeServerStream server = CreatePipeServer(pipeName, PipeDirection.Out);
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
        await server.WaitForConnectionAsync(timeout.Token).ConfigureAwait(false);
        using StreamWriter writer = new(server, Utf8NoBom, 1024, leaveOpen: true);
        await writer.WriteAsync(password).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    private static NamedPipeServerStream CreatePipeServer(string pipeName, PipeDirection direction)
    {
        PipeSecurity pipeSecurity = new();
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        SecurityIdentifier sid = identity.Owner ?? identity.User
            ?? throw new InvalidOperationException("Unable to determine current user SID.");
        pipeSecurity.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        pipeSecurity.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.FullControl, AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(
            pipeName, direction, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, pipeSecurity);
    }

    private static (string Username, string Hostname, int Port) DeserializeRequest(string data)
    {
        string[] values = data.Split(':');
        if (values.Length != 3)
            throw new FormatException("Invalid data format");

        return (
            Encoding.UTF8.GetString(Convert.FromBase64String(values[0])),
            Encoding.UTF8.GetString(Convert.FromBase64String(values[1])),
            int.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(values[2])), System.Globalization.CultureInfo.InvariantCulture));
    }
}
