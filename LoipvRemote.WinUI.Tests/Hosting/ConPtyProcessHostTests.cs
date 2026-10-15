using LoipvRemote.Infrastructure.Windows.Process;
using NUnit.Framework;
using System.Text;

namespace LoipvRemote.WinUI.Tests.Hosting;

public sealed class ConPtyProcessHostTests
{
    [Test]
    public async Task NativeProcessStartsAndConPtyEmitsOutput()
    {
        string executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ping.exe");
        if (!File.Exists(executable))
            Assert.Ignore("The Windows command processor is unavailable on this test machine.");

        var outputReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var processExited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var outputBuffer = new StringBuilder();
        await using var host = new ConPtyProcessHost();
        host.OutputReceived += output =>
        {
            outputBuffer.Append(output);
            outputReceived.TrySetResult();
        };
        host.Exited += () => processExited.TrySetResult();

        host.Start(executable, "127.0.0.1 -n 2", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 80, 24);
        try
        {
            await outputReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await processExited.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch (TimeoutException exception)
        {
            Assert.Fail($"ConPTY output timed out. Process={host.ProcessName}#{host.ProcessId}; Running={host.IsRunning}; ExitCode={host.ExitCode}; Output={outputBuffer}; Error={exception.Message}");
        }
        Assert.That(outputBuffer, Has.Length.GreaterThan(0));
    }
}
