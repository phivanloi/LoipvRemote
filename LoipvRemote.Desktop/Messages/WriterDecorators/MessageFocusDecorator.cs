using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows.Forms;
using LoipvRemote.Messages.MessageFilteringOptions;
using LoipvRemote.Messages.MessageWriters;
using LoipvRemote.App.Composition;
using LoipvRemote.UI.Forms;
using LoipvRemote.UI.Window;
using WeifenLuo.WinFormsUI.Docking;

namespace LoipvRemote.Messages.WriterDecorators
{
    [SupportedOSPlatform("windows")]
    public class MessageFocusDecorator(
        ErrorAndInfoWindow messageWindow,
        IMessageTypeFilteringOptions filter,
        IMessageWriter decoratedWriter,
        MainWindowContext mainWindowContext) : IMessageWriter
    {
        private readonly IMessageTypeFilteringOptions _filter = filter ?? throw new ArgumentNullException(nameof(filter));
        private readonly IMessageWriter _decoratedWriter = decoratedWriter ?? throw new ArgumentNullException(nameof(decoratedWriter));
        private readonly ErrorAndInfoWindow _messageWindow = messageWindow ?? throw new ArgumentNullException(nameof(messageWindow));
        private readonly MainWindowContext _mainWindowContext = mainWindowContext ?? throw new ArgumentNullException(nameof(mainWindowContext));

        public void Write(IMessage message)
        {
            _decoratedWriter.Write(message);

            if (WeShouldFocusNotificationPanel(message))
                _ = SwitchToMessageAsync();
        }

        private bool WeShouldFocusNotificationPanel(IMessage message)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (message.MessageClass)
            {
                case MessageClass.InformationMsg:
                    if (_filter.AllowInfoMessages)
                        return true;
                    break;
                case MessageClass.WarningMsg:
                    if (_filter.AllowWarningMessages) return true;
                    break;
                case MessageClass.ErrorMsg:
                    if (_filter.AllowErrorMessages) return true;
                    break;
            }

            return false;
        }

        private async Task SwitchToMessageAsync()
        {
            await Task.Delay(TimeSpan.FromMilliseconds(300)).ConfigureAwait(true);
            SwitchToMessage();
        }

        private void SwitchToMessage()
        {
            // do not attempt to focus the notification panel if the application is closing
            FrmMain? mainWindow = _mainWindowContext.Current;
            if (mainWindow is null || mainWindow.IsClosing || !mainWindow.IsAccessible || mainWindow.IsDisposed)
            {
                return;
            }

            if (_messageWindow.InvokeRequired)
            {
                mainWindow.Invoke((MethodInvoker)SwitchToMessage);
                return;
            }

            // do not attempt to focus the notification panel if it is in an inconsistent state
            if (_messageWindow.DockState == DockState.Unknown)
                return;

            _messageWindow.PreviousActiveForm = (DockContent)mainWindow.pnlDock.ActiveContent;

            // Show the notifications panel solution:
            // https://stackoverflow.com/questions/13843604/calling-up-dockpanel-suites-autohidden-dockcontent-programmatically
            if (AutoHideEnabled(_messageWindow))
                mainWindow.pnlDock.ActiveAutoHideContent = _messageWindow;
            else
                _messageWindow.Show(mainWindow.pnlDock);

            _messageWindow.lvErrorCollector.Focus();
            _messageWindow.lvErrorCollector.SelectedItems.Clear();
            _messageWindow.lvErrorCollector.Items[0].Selected = true;
            _messageWindow.lvErrorCollector.FocusedItem = _messageWindow.lvErrorCollector.Items[0];
        }

        private static bool AutoHideEnabled(DockContent content)
        {
            return content.DockState == DockState.DockBottomAutoHide ||
                   content.DockState == DockState.DockTopAutoHide ||
                   content.DockState == DockState.DockLeftAutoHide ||
                   content.DockState == DockState.DockRightAutoHide;
        }
    }
}
