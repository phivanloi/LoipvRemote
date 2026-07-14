using System.Diagnostics;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Protocols.Abstractions;

namespace LoipvRemote.Infrastructure.Windows.ProcessManagement;

/// <summary>Builds a Windows process launch request without invoking a shell for normal launches.</summary>
public static class ExternalApplicationProcessStartInfoFactory
{
    public static ProcessStartInfo Create(ExternalApplicationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!definition.IsValid)
            throw new ArgumentException("External application definition is invalid.", nameof(definition));
        if (ContainsLineBreak(definition.ExecutablePath) || ContainsLineBreak(definition.WorkingDirectory))
            throw new ArgumentException("External application paths cannot contain line breaks.", nameof(definition));

        ProcessStartInfo startInfo = new()
        {
            FileName = definition.ExecutablePath,
            UseShellExecute = definition.RunElevated
        };

        if (definition.RunElevated)
        {
            startInfo.Verb = "runas";
            startInfo.Arguments = definition.Arguments;
        }
        else
        {
            foreach (string argument in ExternalApplicationCommandLine.SplitArguments(definition.Arguments))
                startInfo.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrWhiteSpace(definition.WorkingDirectory))
            startInfo.WorkingDirectory = definition.WorkingDirectory;

        return startInfo;
    }

    private static bool ContainsLineBreak(string value) => value.Contains('\r') || value.Contains('\n');
}
