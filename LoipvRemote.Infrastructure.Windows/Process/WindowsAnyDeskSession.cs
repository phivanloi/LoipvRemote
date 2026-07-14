using System.Diagnostics;
using LoipvRemote.Protocols.Abstractions;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace LoipvRemote.Infrastructure.Windows.Process;

public sealed class WindowsAnyDeskSession : IDisposable
{
    private DiagnosticsProcess? _process;

    public event EventHandler? Exited;

    public nint WindowHandle { get; private set; }

    public bool Start(string executablePath, string identifier, string password, TimeSpan windowTimeout)
    {
        IReadOnlyList<string> arguments = AnyDeskLaunch.BuildArguments(identifier, !string.IsNullOrEmpty(password));
        ProcessStartInfo startInfo = new(executablePath) { UseShellExecute = false };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);
        startInfo.RedirectStandardInput = !string.IsNullOrEmpty(password);

        DiagnosticsProcess process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Exited += ProcessOnExited;
        if (!process.Start())
        {
            process.Exited -= ProcessOnExited;
            process.Dispose();
            return false;
        }

        _process = process;
        if (!string.IsNullOrEmpty(password))
        {
            process.StandardInput.WriteLine(password);
            process.StandardInput.Close();
        }

        WindowHandle = WaitForWindow(windowTimeout);
        if (WindowHandle != 0)
            return true;

        Stop();
        return false;
    }

    public void Stop()
    {
        DiagnosticsProcess? process = Interlocked.Exchange(ref _process, null);
        if (process is null)
            return;

        process.Exited -= ProcessOnExited;
        try
        {
            if (!process.HasExited)
                process.Kill();
        }
        finally
        {
            process.Dispose();
            WindowHandle = 0;
        }
    }

    public void Dispose() => Stop();

    private nint WaitForWindow(TimeSpan timeout)
    {
        long deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            DiagnosticsProcess[] candidates = DiagnosticsProcess.GetProcessesByName("AnyDesk");
            try
            {
                foreach (DiagnosticsProcess candidate in candidates)
                {
                    candidate.Refresh();
                    if (candidate.MainWindowHandle == 0)
                        continue;

                    Adopt(candidate);
                    return candidate.MainWindowHandle;
                }
            }
            finally
            {
                foreach (DiagnosticsProcess candidate in candidates)
                {
                    if (!ReferenceEquals(candidate, _process))
                        candidate.Dispose();
                }
            }

            Thread.Sleep(100);
        }

        return 0;
    }

    private void Adopt(DiagnosticsProcess process)
    {
        if (ReferenceEquals(_process, process))
            return;

        DiagnosticsProcess? launcher = _process;
        if (launcher is not null)
        {
            launcher.Exited -= ProcessOnExited;
            launcher.Dispose();
        }

        _process = process;
        process.EnableRaisingEvents = true;
        process.Exited += ProcessOnExited;
    }

    private void ProcessOnExited(object? sender, EventArgs e) => Exited?.Invoke(this, EventArgs.Empty);
}
