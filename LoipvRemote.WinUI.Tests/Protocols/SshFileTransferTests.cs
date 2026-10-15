using LoipvRemote.Protocols.Putty;
using NUnit.Framework;

namespace LoipvRemote.WinUI.Tests.Protocols;

public sealed class SshFileTransferTests
{
    [TestCase("ubuntu@app-01: /srv/apps/api", "/srv/apps/api")]
    [TestCase("ubuntu@app-01: ~", "~")]
    [TestCase("ubuntu@app-01: ~/deploy", "~/deploy")]
    [TestCase("LoipvRemote:CWD:%2Fvar%2Flib%2Fdocker", "/var/lib/docker")]
    [TestCase("LoipvRemote:CWD:%2Fhome%2Fuser%2Ffolder%20name", "/home/user/folder name")]
    public void WindowTitleParserExtractsSafeShellWorkingDirectory(string title, string expected)
    {
        Assert.That(SshWorkingDirectoryTitleParser.Parse(title), Is.EqualTo(expected));
    }

    [TestCase("root@app-01: ~", "", "/root")]
    [TestCase("root@app-01: ~/deploy", "", "/root/deploy")]
    [TestCase("ubuntu@app-01: ~", "", "/home/ubuntu")]
    [TestCase("server: ~", "operator", "/home/operator")]
    [TestCase("ubuntu@app-01: /srv/apps", "", "/srv/apps")]
    public void WindowTitleParserResolvesHomePathsToAnAbsoluteWorkingDirectory(
        string title,
        string fallbackUsername,
        string expected)
    {
        Assert.That(
            SshWorkingDirectoryTitleParser.ParseAbsolute(title, fallbackUsername),
            Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("PuTTY")]
    [TestCase("app-01 - PuTTY")]
    [TestCase("ubuntu@app-01: relative/path")]
    [TestCase("ubuntu@app-01: /tmp\nunsafe")]
    public void WindowTitleParserRejectsTitlesWithoutAnAbsoluteOrHomePath(string title)
    {
        Assert.That(SshWorkingDirectoryTitleParser.Parse(title), Is.Null);
    }

    [TestCase("root@ubuntu18:/home# ", "root", "/home")]
    [TestCase("ubuntu@app-01:~/deploy$ ", "ubuntu", "/home/ubuntu/deploy")]
    [TestCase("\u001b[01;32mroot@ubuntu18\u001b[00m:\u001b[01;34m/var/lib\u001b[00m# ", "root", "/var/lib")]
    [TestCase("[root@server /srv/apps]# ", "root", "/srv/apps")]
    public void SessionLogParserExtractsWorkingDirectoryFromCommonShellPrompts(
        string sessionOutput,
        string username,
        string expected)
    {
        Assert.That(
            SshWorkingDirectorySessionLogParser.Parse(sessionOutput, username),
            Is.EqualTo(expected));
    }

    [Test]
    public void SessionLogParserUsesTheLatestCompletePrompt()
    {
        const string sessionOutput = "root@ubuntu18:~# cd /home\r\nroot@ubuntu18:/home# ";

        Assert.That(
            SshWorkingDirectorySessionLogParser.Parse(sessionOutput, "root"),
            Is.EqualTo("/home"));
    }

    [TestCase("/home/ubuntu", "report.txt", "/home/ubuntu/report.txt")]
    [TestCase("/", "report.txt", "/report.txt")]
    public void RemotePathCombinerKeepsFileNamesInsideTheCurrentDirectory(
        string directory,
        string fileName,
        string expected)
    {
        Assert.That(SshRemotePath.Combine(directory, fileName), Is.EqualTo(expected));
    }

    [TestCase("../secret")]
    [TestCase("folder/file")]
    [TestCase("folder\\file")]
    public void RemotePathCombinerRejectsTraversalAndNestedNames(string fileName)
    {
        Assert.Throws<ArgumentException>(() => SshRemotePath.Combine("/home/ubuntu", fileName));
    }

    [TestCase("~", "/home/ubuntu", "/home/ubuntu")]
    [TestCase("~/deploy", "/home/ubuntu", "/home/ubuntu/deploy")]
    [TestCase("/srv/apps", "/home/ubuntu", "/srv/apps")]
    public void InitialPathResolverExpandsHomePaths(string requested, string home, string expected)
    {
        Assert.That(SshRemotePath.ResolveInitial(requested, home), Is.EqualTo(expected));
    }

    [TestCase("report.txt", 0, "report (1).txt")]
    [TestCase("archive.tar.gz", 2, "archive.tar (3).gz")]
    [TestCase("README", 1, "README (2)")]
    public void TransferNameGeneratorCreatesPredictableCollisionNames(
        string fileName,
        int existingSuffix,
        string expected)
    {
        Assert.That(FileTransferName.CreateCollisionName(fileName, existingSuffix + 1), Is.EqualTo(expected));
    }

    [TestCase(0, 100, 0)]
    [TestCase(1, 100, 1)]
    [TestCase(50, 100, 50)]
    [TestCase(150, 100, 100)]
    [TestCase(0, 0, 100)]
    public void TransferProgressClampsAStablePercentage(long transferred, long total, int expected)
    {
        Assert.That(new FileTransferProgress(transferred, total).Percentage, Is.EqualTo(expected));
    }

    [Test]
    public void TransferRowStatusShowsDirectionAndPercentageWithoutAWindowWideBusyState()
    {
        var status = new FileTransferRowStatus();

        status.Begin(FileTransferDirection.Upload);
        Assert.Multiple(() =>
        {
            Assert.That(status.Glyph, Is.EqualTo("\uE898"));
            Assert.That(status.Text, Is.EqualTo("0%"));
            Assert.That(status.IsActive, Is.True);
        });

        status.Report(new FileTransferProgress(42, 100));
        Assert.That(status.Text, Is.EqualTo("42%"));

        status.Report(new FileTransferProgress(100, 100));
        Assert.Multiple(() =>
        {
            Assert.That(status.Text, Is.EqualTo("100%"));
            Assert.That(status.IsActive, Is.False);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task TransferProgressStreamReportsRealReadAndWriteProgress(bool trackReads)
    {
        byte[] payload = new byte[100];
        var updates = new List<FileTransferProgress>();
        var progress = new InlineProgress<FileTransferProgress>(updates.Add);

        if (trackReads)
        {
            await using var source = new MemoryStream(payload);
            await using var tracked = new TransferProgressStream(source, payload.Length, progress, trackReads: true);
            var buffer = new byte[17];
            while (await tracked.ReadAsync(buffer) > 0)
            {
            }
        }
        else
        {
            await using var destination = new MemoryStream();
            await using var tracked = new TransferProgressStream(destination, payload.Length, progress, trackReads: false);
            await tracked.WriteAsync(payload);
        }

        Assert.Multiple(() =>
        {
            Assert.That(updates, Is.Not.Empty);
            Assert.That(updates[^1].TransferredBytes, Is.EqualTo(payload.Length));
            Assert.That(updates[^1].Percentage, Is.EqualTo(100));
        });
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
