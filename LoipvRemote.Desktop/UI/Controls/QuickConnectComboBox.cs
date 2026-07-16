using System;
using System.Drawing;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Windows.Forms;
using LoipvRemote.Messages;
using LoipvRemote.Connection;
using LoipvRemote.Resources.Language;

namespace LoipvRemote.UI.Controls
{
    [SupportedOSPlatform("windows")]
    public class QuickConnectComboBox : ToolStripComboBox
    {
        private readonly ComboBox _comboBox = null!;
        private bool _ignoreEnter;
        private MessageCollector? _messageCollector;

        public QuickConnectComboBox()
        {
            _comboBox = ComboBox;
            if (_comboBox == null) return;
            _comboBox.PreviewKeyDown += ComboBox_PreviewKeyDown;
            _comboBox.SelectedIndexChanged += ComboBox_SelectedIndexChanged;
            _comboBox.DrawItem += ComboBox_DrawItem;
            _comboBox.DrawMode = DrawMode.OwnerDrawFixed;
            CausesValidation = false;

            // This makes it so that _ignoreEnter works correctly before any items are added to the combo box
            _comboBox.Items.Clear();
        }

        internal void AttachServices(MessageCollector messageCollector) =>
            _messageCollector = messageCollector ?? throw new ArgumentNullException(nameof(messageCollector));

        private void ComboBox_PreviewKeyDown(object? sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Enter & _comboBox.DroppedDown)
            {
                _ignoreEnter = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Enter)
            {
                // Only connect if Enter was not pressed while the combo box was dropped down
                if (!_ignoreEnter)
                {
                    OnConnectRequested(new ConnectRequestedEventArgs(_comboBox.Text));
                }

                _ignoreEnter = false;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete & _comboBox.DroppedDown)
            {
                if (_comboBox.SelectedIndex != -1)
                {
                    // Items can't be removed from the ComboBox while it is dropped down without possibly causing
                    // an exception so we must close it, delete the item, and then drop it down again. When we
                    // close it programmatically, the SelectedItem may revert to Nothing, so we must save it first.
                    object? item = _comboBox.SelectedItem;
                    if (item is null)
                        return;
                    _comboBox.DroppedDown = false;
                    _comboBox.Items.Remove(item);
                    _comboBox.SelectedIndex = -1;
                    if (_comboBox.Items.Count != 0)
                    {
                        _comboBox.DroppedDown = true;
                    }
                }

                e.Handled = true;
            }
        }

        private void ComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (!(_comboBox.SelectedItem is HistoryItem))
            {
                return;
            }

            HistoryItem historyItem = (HistoryItem)_comboBox.SelectedItem;
            OnProtocolChanged(new ProtocolChangedEventArgs(historyItem.ConnectionInfo.Protocol));
        }

        private static void ComboBox_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not ComboBox comboBox)
            {
                return;
            }

            object? drawItem = comboBox.Items[e.Index];

            string drawString;
            if (drawItem is HistoryItem)
            {
                HistoryItem historyItem = (HistoryItem)drawItem;
                drawString = historyItem.ToString(true);
            }
            else
            {
                drawString = drawItem?.ToString() ?? string.Empty;
            }

            e.DrawBackground();
            e.Graphics.DrawString(drawString, e.Font ?? SystemFonts.DefaultFont, new SolidBrush(e.ForeColor),
                                  new RectangleF(e.Bounds.X, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height));
            e.DrawFocusRectangle();
        }

        private struct HistoryItem : IEquatable<HistoryItem>
        {
            public ConnectionInfo ConnectionInfo { get; set; }

            public bool Equals(HistoryItem other)
            {
                if (ConnectionInfo.Hostname != other.ConnectionInfo.Hostname)
                {
                    return false;
                }

                if (ConnectionInfo.Port != other.ConnectionInfo.Port)
                {
                    return false;
                }

                return ConnectionInfo.Protocol == other.ConnectionInfo.Protocol;
            }

            public override bool Equals(object? obj) => obj is HistoryItem other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(ConnectionInfo.Hostname, ConnectionInfo.Port, ConnectionInfo.Protocol);

            public override string ToString()
            {
                return ToString(false);
            }

            public string ToString(bool includeProtocol)
            {
                string port = string.Empty;
                if (ConnectionInfo.Port != ConnectionInfo.GetDefaultPort())
                {
                    port = $":{ConnectionInfo.Port}";
                }

                return includeProtocol
                    ? $"{ConnectionInfo.Hostname}{port} ({ConnectionInfo.Protocol})"
                    : $"{ConnectionInfo.Hostname}{port}";
            }
        }

        private bool Exists(HistoryItem searchItem)
        {
            foreach (object item in _comboBox.Items)
            {
                if (!(item is HistoryItem))
                {
                    continue;
                }

                HistoryItem historyItem = (HistoryItem)item;
                if (historyItem.Equals(searchItem))
                {
                    return true;
                }
            }

            return false;
        }

        public void Add(ConnectionInfo connectionInfo)
        {
            try
            {
                HistoryItem historyItem = new() { ConnectionInfo = connectionInfo };
                if (!Exists(historyItem))
                {
                    _comboBox.Items.Insert(0, historyItem);
                }
            }
            catch (Exception ex)
            {
                if (_messageCollector is not null)
                    _messageCollector.AddExceptionMessage(Language.QuickConnectAddFailed, ex);
                else
                    Trace.TraceError($"{Language.QuickConnectAddFailed}{Environment.NewLine}{ex}");
            }
        }

        #region Events

        public class ConnectRequestedEventArgs(string connectionString) : EventArgs
        {
            public string ConnectionString { get; } = connectionString;
        }

        public event EventHandler<ConnectRequestedEventArgs>? ConnectRequested;


        private void OnConnectRequested(ConnectRequestedEventArgs e)
        {
            ConnectRequested?.Invoke(this, e);
        }

        public class ProtocolChangedEventArgs(ProtocolKind protocol) : EventArgs
        {
            public ProtocolKind Protocol { get; } = protocol;
        }

        public event EventHandler<ProtocolChangedEventArgs>? ProtocolChanged;


        private void OnProtocolChanged(ProtocolChangedEventArgs e)
        {
            ProtocolChanged?.Invoke(this, e);
        }

        #endregion
    }
}
