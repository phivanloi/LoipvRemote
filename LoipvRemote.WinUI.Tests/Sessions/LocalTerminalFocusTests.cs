using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Sessions;

public sealed class LocalTerminalFocusTests
{
    [Test]
    public void TerminalFocusIsReappliedWhenTheWebViewIsAttachedToTheVisualTree()
    {
        string sourcePath = Path.GetFullPath(Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..", "..", "..", "..", "..", "LoipvRemote.WinUI", "Sessions", "LocalTerminalSession.cs"));
        string source = File.ReadAllText(sourcePath).ReplaceLineEndings("\n");

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("private int _focusRequested;"));
            Assert.That(source, Does.Contain("_webView.Loaded += WebViewOnLoaded;"));
            Assert.That(source, Does.Contain("private void WebViewOnLoaded(object sender, RoutedEventArgs args)"));
            Assert.That(source, Does.Contain("_webView.Focus(FocusState.Programmatic);"));
            Assert.That(source, Does.Contain("PostMessageAsync(new { kind = \"focus\" })"));
        });
    }
}
