using System;

namespace LoipvRemote.Connection.Protocol.Terminal
{
    public readonly record struct TerminalProcessStartInfo(string FileName, string Arguments);

    public static class TerminalProcessStartInfoBuilder
    {
        public static TerminalProcessStartInfo Build(string hostname, string username, int port, string commandProcessor)
        {
            string normalizedHostname = hostname?.Trim() ?? string.Empty;
            if (normalizedHostname.Length == 0 || normalizedHostname.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                return new TerminalProcessStartInfo(commandProcessor, "/K");

            string destination = string.IsNullOrEmpty(username)
                ? normalizedHostname
                : $"{username}@{normalizedHostname}";
            string arguments = port > 0 && port != (int)ProtocolTerminal.Defaults.Port
                ? $"-p {port} {ProcessArgumentEscaper.Quote(destination)}"
                : ProcessArgumentEscaper.Quote(destination);

            return new TerminalProcessStartInfo("ssh.exe", arguments);
        }
    }
}
