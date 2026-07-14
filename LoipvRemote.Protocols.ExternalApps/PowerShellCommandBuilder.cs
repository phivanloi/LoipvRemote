using System.Text;

namespace LoipvRemote.Protocols.ExternalApps;

public static class PowerShellCommandBuilder
{
    public static string BuildEncodedArguments(string scriptBlock, string hostname, string username,
                                               string passwordPipeName, int loginAttempts)
    {
        string invocation = $@"
$RemoteScript = {{ {scriptBlock} }}
$Hostname = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{EncodeValue(hostname)}'))
$Username = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{EncodeValue(username)}'))
$PasswordPipeName = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{EncodeValue(passwordPipeName)}'))
$Password = ''
if (-not [string]::IsNullOrEmpty($PasswordPipeName)) {{
    $pipe = [IO.Pipes.NamedPipeClientStream]::new('.', $PasswordPipeName, [IO.Pipes.PipeDirection]::In)
    $pipe.Connect(5000)
    $reader = [IO.StreamReader]::new($pipe, [Text.Encoding]::UTF8)
    $Password = $reader.ReadToEnd()
    $reader.Dispose()
    $pipe.Dispose()
}}
& $RemoteScript -Hostname $Hostname -Username $Username -Password $Password -LoginAttempts {loginAttempts}
";
        return $"-NoExit -NoProfile -EncodedCommand {Convert.ToBase64String(Encoding.Unicode.GetBytes(invocation))}";
    }

    private static string EncodeValue(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
}
