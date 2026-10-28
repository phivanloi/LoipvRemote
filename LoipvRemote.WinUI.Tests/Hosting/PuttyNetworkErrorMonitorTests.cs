using System.Diagnostics;
using System.Runtime.InteropServices;
using LoipvRemote.Infrastructure.Windows.WindowEmbedding;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Hosting;

[Platform("Win")]
[Apartment(ApartmentState.STA)]
public sealed class PuttyNetworkErrorMonitorTests
{
    [Test]
    public void BackgroundDisposalDispatchesUnhookToTheOriginalThreadBeforeReleasingTheCallback()
    {
        Action? remove = null;
        using var monitor = WindowsPuttyNetworkErrorMonitor.TryCreate(Environment.ProcessId, () => { }, action =>
        {
            remove = action;
            return true;
        });
        Assert.That(monitor, Is.Not.Null);
        Task.Run(monitor!.Dispose).GetAwaiter().GetResult();
        Assert.That((object?)remove, Is.Not.Null);
        Assert.That(monitor.IsCallbackRooted, Is.True);
        remove!();
        Assert.That(monitor.IsCallbackRooted, Is.False);
        monitor.Dispose();
    }

    [TestCase("Network error: Software caused connection abort", true)]
    [TestCase("Network error: Connection reset by peer", true)]
    [TestCase("Network error: Connection timed out", true)]
    [TestCase("Configured password was not accepted", false)]
    [TestCase("Access denied", false)]
    [TestCase("WARNING - POTENTIAL SECURITY BREACH!", false)]
    public void OnlyTransientNetworkFailuresAreEligible(string text, bool expected) =>
        Assert.That(WindowsPuttyNetworkErrorMonitor.IsTransientNetworkError(text), Is.EqualTo(expected));

    [TestCase("Network error: Software caused connection abort", 1)]
    [TestCase("Configured password was not accepted", 0)]
    public void NativeDialogRequiresDismissalAndReportsItOnlyOnce(string text, int expected)
    {
        var acknowledgements = 0;
        using var monitor = WindowsPuttyNetworkErrorMonitor.TryCreate(Environment.ProcessId, () => acknowledgements++);
        Assert.That(monitor, Is.Not.Null);
        var dialog = CreateWindowEx(0, "#32770", "PuTTY Fatal Error", unchecked((int)0x80C00000), -32000, -32000, 300, 100, 0, 0, 0, 0);
        Assert.That(dialog, Is.Not.EqualTo(0));
        try
        {
            var label = CreateWindowEx(0, "Static", text, 0x50000000, 0, 0, 300, 50, dialog, 0, 0, 0);
            Assert.That(label, Is.Not.EqualTo(0));
            _ = ShowWindow(dialog, 4);
            PumpMessages(TimeSpan.FromMilliseconds(150));
            Assert.That(acknowledgements, Is.Zero, "A visible dialog must never trigger reconnect.");
            _ = ShowWindow(dialog, 0);
            PumpMessages(TimeSpan.FromMilliseconds(50));
            Assert.That(acknowledgements, Is.Zero, "Hiding/minimizing a dialog is not an acknowledgement.");
            _ = DestroyWindow(dialog);
            dialog = 0;
            PumpMessages(TimeSpan.FromMilliseconds(150));
            Assert.That(acknowledgements, Is.EqualTo(expected));
        }
        finally
        {
            if (dialog != 0)
                _ = DestroyWindow(dialog);
        }
    }

    private static void PumpMessages(TimeSpan duration)
    {
        var buffer = Marshal.AllocHGlobal(64);
        try
        {
            var elapsed = Stopwatch.StartNew();
            do
            {
                while (PeekMessage(buffer, 0, 0, 0, 1))
                {
                    _ = TranslateMessage(buffer);
                    _ = DispatchMessage(buffer);
                }
                Thread.Sleep(1);
            } while (elapsed.Elapsed < duration);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowEx(int extendedStyle, string className, string title, int style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);

    [DllImport("user32.dll")]
    private static extern int ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekMessage(nint message, nint window, uint minimum, uint maximum, uint remove);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage(nint message);

    [DllImport("user32.dll")]
    private static extern nint DispatchMessage(nint message);
}
