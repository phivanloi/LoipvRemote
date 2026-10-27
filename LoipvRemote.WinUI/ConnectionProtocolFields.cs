using LoipvRemote.Domain.Connections;
using LoipvRemote.WinUI.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace LoipvRemote.WinUI;

/// <summary>Nhóm input gọn cho thiết lập chính của SSH, RDP và VNC.</summary>
internal sealed class ConnectionProtocolFields
{
    private readonly TextBox _domain;
    private readonly ToggleSwitch _fitToWindow;
    private readonly ConnectionProtocolSettings _initial;
    private readonly ConnectionNodeOptions? _options;
    private readonly TextBox _privateKey;
    private readonly StackPanel _rdp;
    private readonly ToggleSwitch _shareClipboard;
    private readonly ToggleSwitch _shareLocalDrives;
    private readonly StackPanel _ssh;
    private readonly ToggleSwitch _viewOnly;
    private readonly StackPanel _vnc;

    internal ConnectionProtocolFields(Window owner, ConnectionNodeOptions? options = null, ConnectionNodeOptions? effectiveOptions = null)
    {
        _initial = ConnectionProtocolSettings.Read(effectiveOptions ?? options);
        _options = options;
        _domain = new TextBox { Header = "Domain (optional)", Text = _initial.Domain };
        _privateKey = new TextBox { Header = "SSH private key (optional)", Text = _initial.PrivateKeyPath, PlaceholderText = "Path to a .ppk or OpenSSH key" };
        _shareClipboard = new ToggleSwitch { Header = "Share clipboard", IsOn = _initial.ShareClipboard };
        _shareLocalDrives = new ToggleSwitch { Header = "Share local drives", IsOn = _initial.ShareLocalDrives };
        _viewOnly = new ToggleSwitch { Header = "View only", IsOn = _initial.ViewOnly };
        _fitToWindow = new ToggleSwitch { Header = "Fit remote desktop to window", IsOn = _initial.FitToWindow };
        var browse = new Button { Content = "Browse key file" };
        browse.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(owner));
            var selected = await picker.PickSingleFileAsync();
            if (selected is not null)
                _privateKey.Text = selected.Path;
        };
        _ssh = new StackPanel { Spacing = 8, Children = { _privateKey, browse } };
        _rdp = new StackPanel { Spacing = 12, Children = { _domain, _shareClipboard, _shareLocalDrives } };
        _vnc = new StackPanel { Spacing = 12, Children = { _viewOnly, _fitToWindow } };
        View = new StackPanel { Children = { _ssh, _rdp, _vnc } };
    }

    /// <summary>Phần nội dung đặt sau các input đăng nhập.</summary>
    internal FrameworkElement View { get; }

    /// <summary>Đồng bộ input hiển thị và port khi đổi giao thức.</summary>
    internal void Bind(ComboBox protocolBox, NumberBox portBox)
    {
        var previous = (ProtocolKind)protocolBox.SelectedItem;
        Show(previous);
        protocolBox.SelectionChanged += (_, _) =>
        {
            if (protocolBox.SelectedItem is not ProtocolKind selected)
                return;
            portBox.Value = ConnectionProtocolSettings.ChangePort(portBox.Value, previous, selected);
            previous = selected;
            Show(selected);
        };

        void Show(ProtocolKind protocol)
        {
            _ssh.Visibility = protocol == ProtocolKind.Ssh2 ? Visibility.Visible : Visibility.Collapsed;
            _rdp.Visibility = protocol == ProtocolKind.Rdp ? Visibility.Visible : Visibility.Collapsed;
            _vnc.Visibility = protocol == ProtocolKind.Vnc ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>Lấy thiết lập đã sửa, bảo toàn các giá trị không có input.</summary>
    internal ConnectionNodeOptions? ReadOptions(ProtocolKind protocol) => (_initial with
    {
        PrivateKeyPath = _privateKey.Text,
        Domain = _domain.Text,
        ShareClipboard = _shareClipboard.IsOn,
        ShareLocalDrives = _shareLocalDrives.IsOn,
        ViewOnly = _viewOnly.IsOn,
        FitToWindow = _fitToWindow.IsOn
    }).Apply(_options, protocol, _initial);
}
