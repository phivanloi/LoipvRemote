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
}
