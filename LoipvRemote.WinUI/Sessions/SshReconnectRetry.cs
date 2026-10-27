using System.Net.Sockets;

namespace LoipvRemote.WinUI.Sessions;

/// <summary>Giới hạn việc thử lại lỗi mạng; không lặp lại lỗi xác thực hay cấu hình.</summary>
internal static class SshReconnectRetry
{
    internal static async Task RunAsync(
        Func<CancellationToken, Task> connect,
        CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        delay ??= Task.Delay;
        for (var attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await connect(cancellationToken);
                return;
            }
            catch (Exception exception) when (attempt < 3 && exception is SocketException or TimeoutException)
            {
                await delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
            }
        }
    }
}
