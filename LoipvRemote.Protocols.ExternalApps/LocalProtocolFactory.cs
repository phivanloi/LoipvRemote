using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.ExternalApps;

/// <summary>
/// Owns local-console protocols that are process based rather than remote
/// protocol implementations.  The factory deliberately consumes only Domain
/// values and a Windows process host; it does not create shell lifecycle
/// adapters or reach into the WinForms shell.
/// </summary>
public sealed class LocalProtocolFactory(
    IExternalApplicationHostFactory hostFactory,
    Func<string?>? anyDeskExecutableLocator = null) : IProtocolFactory
{
    private readonly IExternalApplicationHostFactory _hostFactory =
        hostFactory ?? throw new ArgumentNullException(nameof(hostFactory));
    private readonly Func<string?> _anyDeskExecutableLocator =
        anyDeskExecutableLocator ?? LocateAnyDeskExecutable;

    public IProtocolSession Create(ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        ExternalApplicationDefinition application = definition.Protocol switch
        {
            ProtocolKind.PowerShell => BuildPowerShell(definition),
            ProtocolKind.Terminal => BuildTerminal(definition),
            ProtocolKind.Wsl => BuildWsl(definition),
            ProtocolKind.AnyDesk => BuildAnyDesk(definition),
            _ => throw new NotSupportedException(
                $"Protocol '{definition.Protocol}' is not handled by {nameof(LocalProtocolFactory)}.")
        };

        return new ExternalApplicationSession(application, _hostFactory.Create());
    }

    private static ExternalApplicationDefinition BuildPowerShell(ConnectionDefinition definition)
    {
        string executable = GetOption(definition, "ExecutablePath") ?? FindPowerShellExecutable();
        string arguments = string.IsNullOrWhiteSpace(definition.Host)
            ? "-NoExit"
            : $"-NoExit -Command \"Enter-PSSession -ComputerName {ProcessArgumentEscaper.Quote(definition.Host)}\"";
        return CreateDefinition(definition, executable, arguments);
    }

    private static ExternalApplicationDefinition BuildTerminal(ConnectionDefinition definition)
    {
        string commandProcessor = GetOption(definition, "ExecutablePath")
            ?? Environment.GetEnvironmentVariable("COMSPEC")
            ?? @"C:\Windows\System32\cmd.exe";
        TerminalProcessStartInfo startInfo = TerminalProcessStartInfoBuilder.Build(
            definition.Host,
            GetOption(definition, "Username") ?? string.Empty,
            definition.Port,
            commandProcessor);
        return CreateDefinition(definition, startInfo.FileName, startInfo.Arguments);
    }

    private static ExternalApplicationDefinition BuildWsl(ConnectionDefinition definition)
    {
        string executable = GetOption(definition, "ExecutablePath")
            ?? Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows", "System32", "wsl.exe");
        string arguments = string.Join(
            ' ',
            WslLaunchArguments.Build(definition.Host, GetOption(definition, "Username"))
                .Select(ProcessArgumentEscaper.Quote));
        return CreateDefinition(definition, executable, arguments);
    }

    private ExternalApplicationDefinition BuildAnyDesk(ConnectionDefinition definition)
    {
        string executable = GetOption(definition, "ExecutablePath") ?? _anyDeskExecutableLocator()
            ?? throw new InvalidOperationException("AnyDesk is not installed and no ExecutablePath option was supplied.");
        string identifier = definition.Host.Trim();
        if (!AnyDeskLaunch.IsValidIdentifier(identifier))
            throw new ArgumentException("AnyDesk identifier contains unsupported characters.", nameof(definition));
        return CreateDefinition(definition, executable, ProcessArgumentEscaper.Quote(identifier));
    }

    private static ExternalApplicationDefinition CreateDefinition(
        ConnectionDefinition definition,
        string executable,
        string arguments) =>
        new(
            definition.Name,
            executable,
            arguments,
            string.Empty,
            RunElevated: false,
            EmbedWindow: true,
            WaitForExit: false);

    private static string? GetOption(ConnectionDefinition definition, string name) =>
        definition.Options?.Values.TryGetValue(name, out string? value) == true &&
        !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static string FindPowerShellExecutable()
    {
        string? programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
        string pwsh = string.IsNullOrWhiteSpace(programFiles)
            ? "pwsh.exe"
            : Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe");
        return File.Exists(pwsh) ? pwsh : "powershell.exe";
    }

    private static string? LocateAnyDeskExecutable()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory.Trim(), "AnyDesk.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
