using LoipvRemote.Domain.Connections;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace LoipvRemote.WinUI.ViewModels;

/// <summary>Presentation-only item for the WinUI TreeView.</summary>
public sealed record ConnectionTreeItem(
    Guid Id,
    string DisplayName,
    bool IsFolder,
    IReadOnlyList<ConnectionTreeItem> Children,
    bool IsConnected = false,
    ProtocolKind? Protocol = null)
{
    private static readonly Brush ConnectedForeground = new SolidColorBrush(Microsoft.UI.Colors.ForestGreen);
    private static readonly Brush DisconnectedForeground = new SolidColorBrush(Microsoft.UI.Colors.Black);
    private const string FolderPathData = "M1.5,4.25C1.5,3.56 2.06,3 2.75,3H5.75L7.25,4.75H13.25C13.94,4.75 14.5,5.31 14.5,6V12.75C14.5,13.44 13.94,14 13.25,14H2.75C2.06,14 1.5,13.44 1.5,12.75Z";
    private const string RemoteDesktopPathData = "M2,2.5H14C14.55,2.5 15,2.95 15,3.5V10C15,10.55 14.55,11 14,11H9.25V12.5H11.5V14H4.5V12.5H6.75V11H2C1.45,11 1,10.55 1,10V3.5C1,2.95 1.45,2.5 2,2.5ZM2.5,4V9.5H13.5V4Z";
    private const string SshPathData = "M2,2.25H14C14.69,2.25 15.25,2.81 15.25,3.5V12.5C15.25,13.19 14.69,13.75 14,13.75H2C1.31,13.75 0.75,13.19 0.75,12.5V3.5C0.75,2.81 1.31,2.25 2,2.25ZM4,4.5L7,7.5L4,10.5L5.1,11.6L9.2,7.5L5.1,3.4ZM9.75,10.5V12H12.75V10.5Z";
    private const string VncPathData = "M3,1.75H13C13.69,1.75 14.25,2.31 14.25,3V10C14.25,10.69 13.69,11.25 13,11.25H9.5V12.5H11.25V14H4.75V12.5H6.5V11.25H3C2.31,11.25 1.75,10.69 1.75,10V3C1.75,2.31 2.31,1.75 3,1.75ZM3.25,3.25V9.75H12.75V3.25ZM1.75,5.25H0.75V13C0.75,13.69 1.31,14.25 2,14.25H4V12.75H2.25C1.97,12.75 1.75,12.53 1.75,12.25Z";

    private static Geometry ParseGeometry(string pathData) =>
        (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), pathData);

    public static Geometry CreateProtocolIconGeometry(ProtocolKind protocol) =>
        ParseGeometry(protocol switch
        {
            ProtocolKind.Rdp => RemoteDesktopPathData,
            ProtocolKind.Ssh2 => SshPathData,
            ProtocolKind.Vnc => VncPathData,
            _ => SshPathData
        });

    public ConnectionTreeIconKind IconKind => IsFolder
        ? ConnectionTreeIconKind.Folder
        : Protocol switch
        {
            ProtocolKind.Rdp => ConnectionTreeIconKind.RemoteDesktop,
            ProtocolKind.Ssh2 => ConnectionTreeIconKind.SshTerminal,
            ProtocolKind.Vnc => ConnectionTreeIconKind.VncDesktop,
            _ => ConnectionTreeIconKind.SshTerminal
        };

    /// <summary>Each TreeView item receives its own geometry; WinUI visuals cannot share one instance.</summary>
    public Geometry IconGeometry => ParseGeometry(IconPathData);

    internal string IconPathData => IconKind switch
    {
        ConnectionTreeIconKind.Folder => FolderPathData,
        ConnectionTreeIconKind.RemoteDesktop => RemoteDesktopPathData,
        ConnectionTreeIconKind.SshTerminal => SshPathData,
        ConnectionTreeIconKind.VncDesktop => VncPathData,
        _ => SshPathData
    };

    public double IconSize => IconKind switch
    {
        ConnectionTreeIconKind.Folder => 14,
        ConnectionTreeIconKind.RemoteDesktop => 14,
        ConnectionTreeIconKind.SshTerminal => 14,
        ConnectionTreeIconKind.VncDesktop => 14,
        _ => 14
    };

    public double IconVerticalOffset => IconKind is ConnectionTreeIconKind.SshTerminal ? 3 : 0;

    internal static Windows.UI.Color GetIconColor(bool isConnected) =>
        isConnected ? Microsoft.UI.Colors.ForestGreen : Microsoft.UI.Colors.Black;

    internal static Brush GetIconForeground(bool isConnected) =>
        isConnected ? ConnectedForeground : DisconnectedForeground;

    public Brush IconForeground => GetIconForeground(IsConnected);
}

public enum ConnectionTreeIconKind
{
    Folder,
    RemoteDesktop,
    SshTerminal,
    VncDesktop
}
