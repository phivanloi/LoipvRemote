namespace LoipvRemote.WinUI;

public enum SftpPaneSide
{
    Local,
    Remote
}

public enum SftpEntryAction
{
    Open,
    Upload,
    Download,
    Rename,
    Delete
}

public static class SftpContextMenuPolicy
{
    public static IReadOnlyList<SftpEntryAction> For(SftpPaneSide side, bool isDirectory) =>
        isDirectory
            ? [SftpEntryAction.Open, SftpEntryAction.Rename, SftpEntryAction.Delete]
            : side == SftpPaneSide.Local
                ? [SftpEntryAction.Upload, SftpEntryAction.Rename, SftpEntryAction.Delete]
                : [SftpEntryAction.Download, SftpEntryAction.Rename, SftpEntryAction.Delete];
}

public readonly record struct SftpDialogSize(double Width, double Height);

public static class SftpDialogSizing
{
    public const double DesiredWidth = 1555;
    public const double DesiredHeight = 878;
    public const double ScreenMargin = 80;

    public static SftpDialogSize Fit(double availableWidth, double availableHeight) => new(
        FitDimension(availableWidth, DesiredWidth),
        FitDimension(availableHeight, DesiredHeight));

    private static double FitDimension(double available, double desired)
    {
        if (!double.IsFinite(available))
            return desired;

        return Math.Max(0, Math.Min(desired, available - ScreenMargin));
    }
}

public static class SftpWindowActivationPolicy
{
    public static bool ShouldStayTopmost(
        bool foregroundUsesAppProcess,
        bool foregroundIsInsideOwner) =>
        foregroundUsesAppProcess || foregroundIsInsideOwner;
}
