namespace LoipvRemote.Domain.Metadata;

/// <summary>Associates a protocol option with a presentation-layer display resource key.</summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class ProtocolDisplayKeyAttribute(string resourceKey) : Attribute
{
    public string ResourceKey { get; } = resourceKey ?? throw new ArgumentNullException(nameof(resourceKey));
}
