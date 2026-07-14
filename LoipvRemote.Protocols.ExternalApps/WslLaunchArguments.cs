namespace LoipvRemote.Protocols.ExternalApps;

public static class WslLaunchArguments
{
    public static IReadOnlyList<string> Build(string? distribution, string? username)
    {
        List<string> arguments = [];
        string normalizedDistribution = distribution?.Trim() ?? string.Empty;
        if (normalizedDistribution.Length > 0 &&
            !normalizedDistribution.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            arguments.Add("-d");
            arguments.Add(normalizedDistribution);
        }

        string normalizedUsername = username?.Trim() ?? string.Empty;
        if (normalizedUsername.Length > 0)
        {
            arguments.Add("-u");
            arguments.Add(normalizedUsername);
        }

        return arguments;
    }
}
