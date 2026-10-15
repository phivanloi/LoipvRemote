using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Protocols.Putty;

/// <summary>Creates PuTTY sessions from Domain connection definitions.</summary>
public sealed class PuttyProtocolFactory(
    Func<IPuttyProcessHost> processFactory,
    Func<IEmbeddedWindowOperations> windowOperationsFactory,
    Func<string?>? executableLocator = null,
    Func<ConnectionDefinition, string?>? passwordResolver = null,
    Func<string, string, string>? passwordPipeFactory = null,
    Func<IPuttyEndpointProbe>? endpointProbeFactory = null) : IProtocolFactory
{
    private readonly Func<IPuttyProcessHost> _processFactory = processFactory ?? throw new ArgumentNullException(nameof(processFactory));
    private readonly Func<IEmbeddedWindowOperations> _windowOperationsFactory = windowOperationsFactory ?? throw new ArgumentNullException(nameof(windowOperationsFactory));
    private readonly Func<string?> _executableLocator = executableLocator ?? LocateExecutable;
    private readonly Func<ConnectionDefinition, string?>? _passwordResolver = passwordResolver;
    private readonly Func<string, string, string>? _passwordPipeFactory = passwordPipeFactory;
    private readonly Func<IPuttyEndpointProbe> _endpointProbeFactory = endpointProbeFactory ?? (() => new PuttyEndpointProbe());

    public IProtocolSession Create(ConnectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Protocol != ProtocolKind.Ssh2)
            throw new NotSupportedException($"Protocol '{definition.Protocol}' is not handled by {nameof(PuttyProtocolFactory)}.");

        string? password = _passwordResolver?.Invoke(definition);
        string passwordPipeName = string.IsNullOrEmpty(password)
            ? string.Empty
            : _passwordPipeFactory?.Invoke("LoipvRemote-PuTTY-", password)
                ?? throw new InvalidOperationException("A password pipe factory is required when PuTTY credentials are configured.");

        var options = new PuttyConnectionOptions(
            ResolveExecutable(definition.Options),
            new PuttyLaunchOptions
            {
                Hostname = definition.Host,
                Port = definition.Port,
                Protocol = PuttyProtocolKind.Ssh,
                SshVersion = PuttySshVersion.Ssh2,
                SavedSession = Option(definition.Options, "PuttySession"),
                OpeningCommandPath = Option(definition.Options, "OpeningCommandPath"),
                PrivateKeyPath = Option(definition.Options, "PrivateKeyPath"),
                AuthenticationPluginCommand = Option(definition.Options, "AuthenticationPluginCommand"),
                SessionLogPath = SshWorkingDirectorySessionLog.CreatePath(),
                AdditionalOptions = Option(definition.Options, "SSHOptions"),
                Username = Option(definition.Options, "Username")
                ,PasswordPipeName = passwordPipeName
            },
            // Starting minimized requires SW_RESTORE after reparenting. PuTTY
            // uses that restore transition to restore its top-level styles,
            // which turns the embedded session back into an owned overlay.
            StartMinimized: false);

        options.Validate();
        return new PuttyProtocolSession(_processFactory(), _windowOperationsFactory(), options, _endpointProbeFactory());
    }

    private string ResolveExecutable(ConnectionNodeOptions? options)
    {
        string value = Option(options, "ExecutablePath");
        if (string.IsNullOrWhiteSpace(value))
            value = _executableLocator() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("PuTTY executable was not configured and putty.exe was not found.", nameof(options));
        return value;
    }

    private static string Option(ConnectionNodeOptions? options, string name) =>
        options?.Values.TryGetValue(name, out string? value) == true ? value : string.Empty;

    private static string? LocateExecutable()
    {
        return new SystemPuttyExecutableLocator().Locate();
    }
}
