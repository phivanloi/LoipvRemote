using LoipvRemote.Protocols.Putty;
using Microsoft.Win32;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Protocols;

[Platform("Win")]
public sealed class PuttyConnectionDefaultsTests
{
    [TestCase("", null, null, 0, 30)]
    [TestCase("", 0, 0, 0, 30)]
    [TestCase("", 0, 45, 0, 45)]
    [TestCase("", 2, 15, 2, 15)]
    [TestCase("Default Settings", 0, 0, 0, 30)]
    [TestCase("default settings", 0, 0, 0, 30)]
    [TestCase("Custom saved session", 0, 0, 0, 0)]
    public void DefaultKeepalivePreventsIdleSessionsWithoutReplacingAnExistingInterval(
        string sourceName, int? minutes, int? seconds, int expectedMinutes, int expectedSeconds)
    {
        var path = $@"Software\LoipvRemote\Tests\Keepalive-{Guid.NewGuid():N}";
        try
        {
            using var settings = Registry.CurrentUser.CreateSubKey(path, writable: true);
            if (minutes.HasValue)
                settings.SetValue("PingInterval", minutes.Value, RegistryValueKind.DWord);
            if (seconds.HasValue)
                settings.SetValue("PingIntervalSecs", seconds.Value, RegistryValueKind.DWord);
            settings.SetValue("PublicKeyFile", "test-key.ppk");
            settings.SetValue("TCPKeepalives", 0, RegistryValueKind.DWord);

            PuttyDpiSettingsSession.ApplyDefaultKeepalive(settings, sourceName);

            Assert.Multiple(() =>
            {
                Assert.That(settings.GetValue("PingInterval"), Is.EqualTo(expectedMinutes));
                Assert.That(settings.GetValue("PingIntervalSecs"), Is.EqualTo(expectedSeconds));
                Assert.That(settings.GetValue("PublicKeyFile"), Is.EqualTo("test-key.ppk"));
                Assert.That(settings.GetValue("TCPKeepalives"), Is.EqualTo(0));
            });
        }
        finally
        {
            Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        }
    }

    [TestCase(0, 0)]
    [TestCase(1, 15)]
    public void TemporarySessionPreservesNamedSettingsAndWorksWithoutAHostWindow(int minutes, int seconds)
    {
        var sourceName = $"LoipvRemote Test {Guid.NewGuid():N}";
        var sourcePath = $@"Software\SimonTatham\PuTTY\Sessions\{Uri.EscapeDataString(sourceName)}";
        string? temporaryPath = null;
        try
        {
            using var source = Registry.CurrentUser.CreateSubKey(sourcePath, writable: true);
            source.SetValue("PingInterval", minutes, RegistryValueKind.DWord);
            source.SetValue("PingIntervalSecs", seconds, RegistryValueKind.DWord);
            source.SetValue("PublicKeyFile", "test-key.ppk");
            source.SetValue("FontHeight", 12, RegistryValueKind.DWord);

            using (var session = PuttyDpiSettingsSession.TryCreate(sourceName, IntPtr.Zero))
            {
                Assert.That(session, Is.Not.Null);
                temporaryPath = $@"Software\SimonTatham\PuTTY\Sessions\{Uri.EscapeDataString(session!.SessionName)}";
                using var temporary = Registry.CurrentUser.OpenSubKey(temporaryPath);
                Assert.Multiple(() =>
                {
                    Assert.That(session.Dpi, Is.EqualTo(96));
                    Assert.That(temporary?.GetValue("PingInterval"), Is.EqualTo(minutes));
                    Assert.That(temporary?.GetValue("PingIntervalSecs"), Is.EqualTo(seconds));
                    Assert.That(temporary?.GetValue("PublicKeyFile"), Is.EqualTo("test-key.ppk"));
                    Assert.That(temporary?.GetValue("FontHeight"), Is.EqualTo(12));
                });
            }

            using var deleted = Registry.CurrentUser.OpenSubKey(temporaryPath);
            Assert.Multiple(() =>
            {
                Assert.That(deleted, Is.Null);
                Assert.That(source.GetValue("PingInterval"), Is.EqualTo(minutes));
                Assert.That(source.GetValue("PingIntervalSecs"), Is.EqualTo(seconds));
                Assert.That(source.GetValue("PublicKeyFile"), Is.EqualTo("test-key.ppk"));
            });
        }
        finally
        {
            if (temporaryPath is not null)
                Registry.CurrentUser.DeleteSubKeyTree(temporaryPath, throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree(sourcePath, throwOnMissingSubKey: false);
        }
    }
}
