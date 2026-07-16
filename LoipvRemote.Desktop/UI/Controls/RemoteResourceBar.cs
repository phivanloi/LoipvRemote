using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using LoipvRemote.Connection;
using LoipvRemote.Connection.Monitoring;
using LoipvRemote.Protocols.Putty.Monitoring;
using System.Runtime.Versioning;

namespace LoipvRemote.UI.Controls
{
    [SupportedOSPlatform("windows")]
    public sealed class RemoteResourceBar : UserControl
    {
        private readonly SshResourceMonitor _monitor;
        private readonly Label _status = CreateStatusLabel(150);
        private readonly ResourceMetricLabel _cpu = CreateMetricLabel(80);
        private readonly ResourceMetricLabel _memory = CreateMetricLabel(145);
        private readonly ResourceMetricLabel _disk = CreateMetricLabel(180);
        private readonly ResourceMetricLabel _receive = CreateMetricLabel(105);
        private readonly ResourceMetricLabel _transmit = CreateMetricLabel(105);
        private readonly ResourceMetricLabel _uptime = CreateMetricLabel(110);
        private readonly ToolTip _toolTip = new();
        private readonly FlowLayoutPanel _items;
        private RemoteResourceMonitorState _monitorState = RemoteResourceMonitorState.WaitingForActiveTab;
        private readonly ResourceMetricLabel[] _metrics;

        public RemoteResourceBar(ConnectionInfo connection, PuttyResourceMonitorFactory monitorFactory)
        {
            Dock = DockStyle.Bottom;
            Height = 28;
            MinimumSize = new Size(0, 28);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.FromArgb(247, 248, 250);
            ForeColor = Color.FromArgb(31, 41, 55);
            Padding = new Padding(5, 2, 5, 2);

            _items = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                WrapContents = false,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = BackColor,
                Padding = Padding,
                Margin = Padding.Empty
            };

            _status.Text = RemoteResourceText.Format("RemoteResourceStatusConnection", "SSH: {0}", connection.Name);
            _cpu.SetMetric("CPU:", "--");
            _memory.SetMetric("RAM:", "--");
            _disk.SetMetric("Disk:", "--");
            _receive.SetMetric("In:", "--");
            _transmit.SetMetric("Out:", "--");
            _uptime.SetMetric("Uptime:", "--");
            _toolTip.SetToolTip(_cpu, RemoteResourceText.Get("RemoteResourceTooltipCpu", "Remote CPU usage"));
            _toolTip.SetToolTip(_memory, RemoteResourceText.Get("RemoteResourceTooltipMemory", "Used / total RAM"));
            _toolTip.SetToolTip(_disk, RemoteResourceText.Get("RemoteResourceTooltipDisk", "Used disk space"));
            _toolTip.SetToolTip(_receive, RemoteResourceText.Get("RemoteResourceTooltipReceive", "Remote receive traffic"));
            _toolTip.SetToolTip(_transmit, RemoteResourceText.Get("RemoteResourceTooltipTransmit", "Remote transmit traffic"));
            _toolTip.SetToolTip(_uptime, RemoteResourceText.Get("RemoteResourceTooltipUptime", "Remote uptime"));

            _items.Controls.AddRange([_status, _cpu, _memory, _disk, _receive, _transmit, _uptime]);
            _metrics = [_cpu, _memory, _disk, _receive, _transmit, _uptime];
            Controls.Add(_items);
            Paint += DrawTopBorder;
            Resize += (_, _) => ApplyResponsiveLayout();

            _monitor = monitorFactory?.Create(connection)
                ?? throw new ArgumentNullException(nameof(monitorFactory));
            _monitor.SnapshotUpdated += OnSnapshotUpdated;
            _monitor.StatusChanged += OnStatusChanged;
            ApplyResponsiveLayout();
        }

        public void Start() => _monitor.Start();

        public void Stop() => _monitor.Stop();

        public void SetIsActive(bool isActive) => _monitor.SetIsActive(isActive);

        internal bool IsStatusVisible => _status.Visible;

        private static Label CreateStatusLabel(int width) => new()
        {
            AutoSize = false,
            Width = width,
            Height = 22,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 5, 0),
            Padding = new Padding(5, 0, 5, 0),
            BorderStyle = BorderStyle.FixedSingle
        };

        private static ResourceMetricLabel CreateMetricLabel(int width) => new()
        {
            AutoSize = false,
            Width = width,
            Height = 22,
            Margin = new Padding(0, 0, 5, 0),
            Padding = new Padding(5, 0, 5, 0),
            BorderStyle = BorderStyle.FixedSingle
        };

        private void OnSnapshotUpdated(RemoteResourceSnapshot snapshot)
        {
            InvokeIfAlive(() =>
            {
                _cpu.SetMetric("CPU:", snapshot.CpuPercent is null ? "--" : $"{snapshot.CpuPercent:0} %");
                _memory.SetMetric("RAM:", $"{FormatBytes(snapshot.MemoryUsedBytes)}/{FormatBytes(snapshot.MemoryTotalBytes)}");
                _disk.SetMetric("Disk:", $"{FormatBytes(snapshot.DiskUsedBytes)}/{FormatBytes(snapshot.DiskTotalBytes)} ({snapshot.DiskPercent:0} %)");
                _receive.SetMetric("In:", snapshot.ReceiveBytesPerSecond is null ? "--" : $"{FormatBytes(snapshot.ReceiveBytesPerSecond.Value)}/s");
                _transmit.SetMetric("Out:", snapshot.TransmitBytesPerSecond is null ? "--" : $"{FormatBytes(snapshot.TransmitBytesPerSecond.Value)}/s");
                _uptime.SetMetric("Uptime:", FormatUptime(snapshot.Uptime));
            });
        }

        private void OnStatusChanged(RemoteResourceMonitorStatus status)
        {
            InvokeIfAlive(() =>
            {
                _monitorState = status.State;
                _status.Text = status.Message;
                ApplyResponsiveLayout();
            });
        }

        private void ApplyResponsiveLayout()
        {
            int availableWidth = ClientSize.Width - Padding.Horizontal;
            if (availableWidth <= 0) return;

            // Keep the monitoring state visible while metrics are being collected.
            // Hiding the status item in the healthy state made the whole SSH
            // monitoring strip look as if it had disappeared, especially when
            // the terminal filled most of the available width.
            _status.Visible = true;
            int minimumWidth = _metrics.Sum(GetBaseWidth) + _metrics.Sum(label => label.Margin.Horizontal);
            if (_status.Visible)
                minimumWidth += GetBaseWidth(_status) + _status.Margin.Horizontal;

            int extraWidthPerMetric = Math.Max(0, availableWidth - minimumWidth) / _metrics.Length;
            foreach (Label metric in _metrics)
                metric.Width = GetBaseWidth(metric) + extraWidthPerMetric;

            if (_status.Visible)
                _status.Width = GetBaseWidth(_status);
            _items.PerformLayout();
        }

        private int GetBaseWidth(Label label) => label switch
        {
            var value when ReferenceEquals(value, _cpu) => 80,
            var value when ReferenceEquals(value, _memory) => 145,
            var value when ReferenceEquals(value, _disk) => 180,
            var value when ReferenceEquals(value, _receive) => 105,
            var value when ReferenceEquals(value, _transmit) => 105,
            var value when ReferenceEquals(value, _uptime) => 110,
            _ => 150
        };

        private void InvokeIfAlive(Action callback)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            if (InvokeRequired)
                BeginInvoke(callback);
            else
                callback();
        }

        private void DrawTopBorder(object? sender, PaintEventArgs e)
        {
            using Pen pen = new(Color.FromArgb(203, 213, 225));
            e.Graphics.DrawLine(pen, 0, 0, Width, 0);
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB"];
            double value = Math.Max(bytes, 0);
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.0} {units[unit]}";
        }

        private static string FormatUptime(TimeSpan uptime)
        {
            return uptime.TotalDays >= 1
                ? $"{uptime.Days}d {uptime.Hours}h"
                : $"{uptime.Hours}h {uptime.Minutes}m";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _monitor.Dispose();
                _toolTip.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    [SupportedOSPlatform("windows")]
    internal sealed class ResourceMetricLabel : Label
    {
        private Font? _valueFont;

        internal string Caption { get; private set; } = string.Empty;
        internal string ValueText { get; private set; } = string.Empty;
        internal FontStyle ValueFontStyle => ValueFont.Style;

        internal void SetMetric(string caption, string value)
        {
            Caption = caption ?? string.Empty;
            ValueText = value ?? string.Empty;
            AccessibleName = $"{Caption} {ValueText}";
            Invalidate();
        }

        protected override void OnFontChanged(EventArgs e)
        {
            _valueFont?.Dispose();
            _valueFont = null;
            base.OnFontChanged(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Rectangle textBounds = ClientRectangle;
            textBounds.Inflate(-Padding.Left, -Padding.Top);
            Size captionSize = TextRenderer.MeasureText(e.Graphics, Caption, Font, Size.Empty, TextFormatFlags.NoPadding);
            int y = textBounds.Top + Math.Max(0, (textBounds.Height - Font.Height) / 2);
            TextRenderer.DrawText(e.Graphics, Caption, Font, new Point(textBounds.Left, y), ForeColor, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(e.Graphics, ValueText, ValueFont, new Point(textBounds.Left + captionSize.Width + 4, y), ForeColor,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _valueFont?.Dispose();

            base.Dispose(disposing);
        }

        private Font ValueFont => _valueFont ??= new Font(Font, Font.Style | FontStyle.Bold);
    }
}
