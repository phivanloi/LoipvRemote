using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Protocols.Rdp;

namespace LoipvRemote.WinUI.Services;

/// <summary>Các thiết lập chính hiển thị bằng input riêng theo giao thức.</summary>
internal sealed record ConnectionProtocolSettings
{
    /// <summary>Đường dẫn private key SSH; để trống khi dùng mật khẩu hoặc agent.</summary>
    public string PrivateKeyPath { get; init; } = string.Empty;

    /// <summary>Domain của tài khoản RDP, nếu có.</summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>Cho phép chia sẻ clipboard trong phiên RDP.</summary>
    public bool ShareClipboard { get; init; } = true;

    /// <summary>Cho phép chia sẻ ổ đĩa cục bộ trong phiên RDP.</summary>
    public bool ShareLocalDrives { get; init; } = true;

    /// <summary>Chỉ xem màn hình VNC, không gửi phím hoặc chuột.</summary>
    public bool ViewOnly { get; init; }

    /// <summary>Co màn hình VNC vừa kích thước tab.</summary>
    public bool FitToWindow { get; init; } = true;

    /// <summary>Đọc thiết lập hiện có, dùng cùng mặc định với protocol factory.</summary>
    internal static ConnectionProtocolSettings Read(ConnectionNodeOptions? options)
    {
        var drives = ReadOption(options, "RedirectDiskDrives");
        return new()
        {
            PrivateKeyPath = ReadOption(options, "PrivateKeyPath"),
            Domain = ReadOption(options, "Domain"),
            ShareClipboard = ReadBoolean("RedirectClipboard", true),
            ShareLocalDrives = !Enum.TryParse<RDPDiskDrives>(drives, true, out var parsed) || parsed != RDPDiskDrives.None,
            ViewOnly = ReadBoolean("ViewOnly", false),
            FitToWindow = ReadBoolean("SmartSize", true)
        };

        bool ReadBoolean(string name, bool fallback) =>
            bool.TryParse(ReadOption(options, name), out var value) ? value : fallback;
    }

    /// <summary>Chỉ cập nhật input đã thay đổi; bảo toàn tùy chọn ẩn và cấu hình kế thừa.</summary>
    internal ConnectionNodeOptions? Apply(
        ConnectionNodeOptions? options,
        ProtocolKind protocol,
        ConnectionProtocolSettings? initial = null)
    {
        initial ??= Read(options);
        var values = options is null ? new Dictionary<string, string>(StringComparer.Ordinal) : new(options.Values, StringComparer.Ordinal);
        var inherited = new HashSet<string>(options?.InheritedProperties ?? [], StringComparer.Ordinal);
        switch (protocol)
        {
            case ProtocolKind.Ssh2:
                SetChanged("PrivateKeyPath", PrivateKeyPath.Trim(), initial.PrivateKeyPath);
                break;
            case ProtocolKind.Rdp:
                SetChanged("Domain", Domain.Trim(), initial.Domain);
                if (ShareClipboard != initial.ShareClipboard)
                    SetChanged("RedirectClipboard", ShareClipboard.ToString(), initial.ShareClipboard.ToString());
                if (ShareLocalDrives != initial.ShareLocalDrives)
                    SetChanged("RedirectDiskDrives", ShareLocalDrives ? "Local" : "None", string.Empty);
                break;
            case ProtocolKind.Vnc:
                SetChanged("ViewOnly", ViewOnly.ToString(), initial.ViewOnly.ToString());
                SetChanged("SmartSize", FitToWindow.ToString(), initial.FitToWindow.ToString());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(protocol));
        }

        return values.Count == 0 && inherited.Count == 0 ? null : new(values, inherited.ToArray());

        void SetChanged(string name, string value, string previous)
        {
            if (string.Equals(value, previous, StringComparison.Ordinal))
                return;
            values[name] = value;
            inherited.Remove(name);
        }
    }

    /// <summary>Điền mặc định còn thiếu, không ghi đè giá trị hiệu lực từ connection hoặc folder.</summary>
    internal static ConnectionNodeOptions AddMissingDefaults(
        ConnectionNodeOptions? options, ProtocolKind protocol, ConnectionNodeOptions? effectiveOptions = null)
    {
        var values = options is null ? new Dictionary<string, string>(StringComparer.Ordinal) : new(options.Values, StringComparer.Ordinal);
        effectiveOptions ??= options;
        switch (protocol)
        {
            case ProtocolKind.Ssh2:
                Add("PrivateKeyPath", string.Empty);
                break;
            case ProtocolKind.Rdp:
                Add("Domain", string.Empty);
                Add("RedirectClipboard", "True");
                Add("RedirectDiskDrives", "Local");
                break;
            case ProtocolKind.Vnc:
                Add("ViewOnly", "False");
                Add("SmartSize", "True");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(protocol));
        }
        return new(values, options?.InheritedProperties.ToArray() ?? []);

        void Add(string name, string value)
        {
            if (effectiveOptions?.Values.ContainsKey(name) != true)
                values.TryAdd(name, value);
        }
    }

    /// <summary>Đổi port mặc định theo giao thức nhưng giữ port tùy chỉnh.</summary>
    internal static double ChangePort(double currentPort, ProtocolKind previous, ProtocolKind selected) =>
        currentPort == DefaultPort(previous) ? DefaultPort(selected) : currentPort;

    private static int DefaultPort(ProtocolKind protocol) => protocol switch
    {
        ProtocolKind.Ssh2 => 22,
        ProtocolKind.Rdp => 3389,
        ProtocolKind.Vnc => 5900,
        _ => throw new ArgumentOutOfRangeException(nameof(protocol))
    };

    private static string ReadOption(ConnectionNodeOptions? options, string name) =>
        options?.Values.GetValueOrDefault(name) ?? string.Empty;
}
