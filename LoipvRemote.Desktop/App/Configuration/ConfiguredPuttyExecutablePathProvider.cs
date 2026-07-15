using LoipvRemote.Properties;
using LoipvRemote.UseCases.Configuration;

namespace LoipvRemote.App.Configuration;

/// <summary>Bridges persisted desktop settings into the protocol-neutral path contract.</summary>
public sealed class ConfiguredPuttyExecutablePathProvider : IPuttyExecutablePathProvider
{
    public string? Resolve() => OptionsAdvancedPage.Default.UseCustomPuttyPath
        ? OptionsAdvancedPage.Default.CustomPuttyPath
        : App.Info.GeneralAppInfo.PuttyPath;
}
