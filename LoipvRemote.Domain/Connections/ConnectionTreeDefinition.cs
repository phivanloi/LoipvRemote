using LoipvRemote.Domain.Validation;

namespace LoipvRemote.Domain.Connections;

/// <summary>Complete, ordered connection tree independent of UI controls and persistence technology.</summary>
public sealed record ConnectionTreeDefinition(
    IReadOnlyCollection<ConnectionFolderDefinition> Folders,
    IReadOnlyCollection<ConnectionDefinition> Connections)
{
    public static ConnectionTreeDefinition Empty { get; } = new([], []);

    public void Validate() => ConnectionTreeDefinitionValidator.Validate(this);
}
