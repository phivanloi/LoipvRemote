namespace LoipvRemote.Protocols.Abstractions;

public static class AnyDeskLaunch
{
    public static bool IsValidIdentifier(string? identifier) =>
        !string.IsNullOrWhiteSpace(identifier) &&
        identifier.All(character => char.IsLetterOrDigit(character) || character is '@' or '-' or '_' or '.');

    public static IReadOnlyList<string> BuildArguments(string identifier, bool hasPassword)
    {
        if (!IsValidIdentifier(identifier))
            throw new ArgumentException("Invalid AnyDesk identifier.", nameof(identifier));

        List<string> arguments = [identifier.Trim()];
        if (hasPassword)
            arguments.Add("--with-password");
        arguments.Add("--plain");
        return arguments;
    }
}
