using System;
using System.Text;

namespace mRemoteNG.Connection.Protocol.PowerShell
{
    public static class PowerShellCommandBuilder
    {
        public static string BuildEncodedArguments(string scriptBlock, string hostname, string username,
                                                   string password, int loginAttempts)
        {
            string invocation = $@"
$RemoteScript = {{ {scriptBlock} }}
$Hostname = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{EncodeValue(hostname)}'))
$Username = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{EncodeValue(username)}'))
$Password = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{EncodeValue(password)}'))
& $RemoteScript -Hostname $Hostname -Username $Username -Password $Password -LoginAttempts {loginAttempts}
";
            string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(invocation));
            return $"-NoExit -NoProfile -EncodedCommand {encodedCommand}";
        }

        private static string EncodeValue(string value) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
    }
}
