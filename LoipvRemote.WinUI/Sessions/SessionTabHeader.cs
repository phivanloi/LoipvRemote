using LoipvRemote.WinUI.ViewModels;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace LoipvRemote.WinUI.Sessions;

internal sealed class SessionTabHeader : StackPanel
{
    private readonly PathIcon _icon;

    internal SessionTabHeader(RemoteSessionTab sessionTab)
    {
        ArgumentNullException.ThrowIfNull(sessionTab);

        Orientation = Orientation.Horizontal;
        Spacing = 6;
        VerticalAlignment = VerticalAlignment.Center;

        _icon = new PathIcon
        {
            Data = ConnectionTreeItem.CreateProtocolIconGeometry(sessionTab.Connection.Protocol),
            Width = 14,
            Height = 14,
            VerticalAlignment = VerticalAlignment.Center
        };
        Children.Add(_icon);
        Children.Add(new TextBlock
        {
            Text = sessionTab.Connection.Name,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        });
        Children.Add(new TextBlock
        {
            Text = sessionTab.Connection.Host,
            Opacity = 0.72,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 180
        });

        UpdateSelection(isActive: false);
    }

    internal static bool UsesActiveForeground(bool isActive) => isActive;

    internal static Windows.UI.Color GetIconColor(bool isActive) =>
        isActive ? Microsoft.UI.Colors.ForestGreen : Microsoft.UI.Colors.Black;

    internal void UpdateSelection(bool isActive) =>
        _icon.Foreground = new SolidColorBrush(GetIconColor(UsesActiveForeground(isActive)));
}
