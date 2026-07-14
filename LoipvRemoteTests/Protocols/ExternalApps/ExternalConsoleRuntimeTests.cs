using System.Drawing;
using System.Windows.Forms;
using LoipvRemote.Protocols.ExternalApps;
using NUnit.Framework;

namespace LoipvRemoteTests.Protocols.ExternalApps;

[TestFixture]
[Apartment(ApartmentState.STA)]
public sealed class ExternalConsoleRuntimeTests
{
    [Test]
    public void StartProcess_CapturesOutputInManagedControl()
    {
        using ExternalConsoleRuntime runtime = new(Color.MidnightBlue);

        runtime.StartProcess(Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe", "/d /c echo managed-console-ready");

        RichTextBox control = runtime.Control as RichTextBox
            ?? throw new AssertionException("The terminal must be a managed text control.");
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!control.Text.Contains("managed-console-ready", StringComparison.Ordinal) && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }

        Assert.That(control.Text, Does.Contain("managed-console-ready"));
    }

    [Test]
    public void Constructor_UsesFocusableManagedWinFormsControl()
    {
        using ExternalConsoleRuntime runtime = new(Color.Black);

        Assert.Multiple(() =>
        {
            Assert.That(runtime.Control, Is.InstanceOf<RichTextBox>());
            Assert.That(runtime.Control.TabStop, Is.True);
            Assert.That(runtime.Control.AccessibleName, Is.EqualTo("Console session"));
        });
    }

    [Test]
    public void SendInput_WritesToInteractiveChildProcess()
    {
        using ExternalConsoleRuntime runtime = new(Color.Black);
        runtime.StartProcess(Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe", "/d /q /k");
        runtime.SendInput("echo managed-terminal-input\r\n");

        RichTextBox control = (RichTextBox)runtime.Control;
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (!control.Text.Contains("managed-terminal-input", StringComparison.Ordinal) && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }

        Assert.That(control.Text, Does.Contain("managed-terminal-input"));
    }
}
