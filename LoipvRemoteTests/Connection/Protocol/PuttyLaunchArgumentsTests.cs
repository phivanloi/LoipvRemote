using LoipvRemote.Protocols.Putty;
using NUnit.Framework;

namespace LoipvRemoteTests.Connection.Protocol;

[TestFixture]
public sealed class PuttyLaunchArgumentsTests
{
    [Test]
    public void BuildsSshLaunchWithoutPuttingPasswordOnCommandLine()
    {
        string arguments = PuttyLaunchArguments.Build(new PuttyLaunchOptions
        {
            SavedSession = "Default Settings",
            Protocol = PuttyProtocolKind.Ssh,
            SshVersion = PuttySshVersion.Ssh2,
            Username = "user name",
            PasswordPipeName = "LoipvRemoteSecretPipe12345678",
            PrivateKeyPath = @"C:\Temp Folder\key.ppk",
            OpeningCommandPath = @"C:\Temp Folder\open.txt",
            Port = 2222,
            Hostname = "server.example",
            AdditionalOptions = "-N -L 8080:localhost:80"
        });

        Assert.That(arguments, Is.EqualTo(
            "-load \"Default Settings\" -ssh -2 -l \"user name\" " +
            "-pwfile \\\\.\\PIPE\\LoipvRemoteSecretPipe12345678 " +
            "-i \"C:\\Temp Folder\\key.ppk\" -m \"C:\\Temp Folder\\open.txt\" " +
            "-P 2222 server.example -N -L 8080:localhost:80"));
    }

    [Test]
    public void SavedSessionDoesNotAppendConnectionOverrides()
    {
        string arguments = PuttyLaunchArguments.Build(new PuttyLaunchOptions
        {
            SavedSession = "Production",
            UseSavedSessionOnly = true,
            Protocol = PuttyProtocolKind.Ssh,
            Username = "ignored",
            Port = 22,
            Hostname = "ignored"
        });

        Assert.That(arguments, Is.EqualTo("-load Production"));
    }

    [Test]
    public void SuppressedCredentialsNeverAddsUsernameOrPasswordPipe()
    {
        string arguments = PuttyLaunchArguments.Build(new PuttyLaunchOptions
        {
            Protocol = PuttyProtocolKind.Ssh,
            SshVersion = PuttySshVersion.Ssh2,
            Username = "secret-user",
            PasswordPipeName = "secret-pipe",
            SuppressCredentials = true,
            Port = 22,
            Hostname = "host"
        });

        Assert.That(arguments, Does.Not.Contain("secret-user"));
        Assert.That(arguments, Does.Not.Contain("secret-pipe"));
    }
}
