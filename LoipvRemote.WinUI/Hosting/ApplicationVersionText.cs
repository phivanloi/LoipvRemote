namespace LoipvRemote.WinUI.Hosting;

internal static class ApplicationVersionText
{
    public static string Current => Format(typeof(ApplicationVersionText).Assembly.GetName().Version);

    internal static string Format(Version? version) =>
        version is null ? "Version unavailable" : $"Version {version}";
}
