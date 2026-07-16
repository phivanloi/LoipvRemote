using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Windows.Forms;
using LoipvRemote.App;
using LoipvRemote.App.Info;
using LoipvRemote.Resources.Language;
using ApplicationEdition = LoipvRemote.UseCases.Hosting.ApplicationEdition;

namespace LoipvRemote.UI.Forms
{
    [SupportedOSPlatform("windows")]
    public partial class UnhandledExceptionForm : Form
    {
        private readonly bool _isFatal;

        public UnhandledExceptionForm()
            : this(null, false)
        {
        }

        public UnhandledExceptionForm(Exception? exception, bool isFatal)
        {
            _isFatal = isFatal;
            InitializeComponent();
            SetLanguage();

            if (exception == null)
                return;

            textBoxExceptionMessage.Text = exception.Message;
            textBoxStackTrace.Text = exception.StackTrace;
            SetEnvironmentText();
        }

        private void SetEnvironmentText()
        {
            textBoxEnvironment.Text = new StringBuilder()
                .AppendLine(CultureInfo.CurrentCulture, $"OS: {Environment.OSVersion}")
                .AppendLine(CultureInfo.CurrentCulture, $"{GeneralAppInfo.ProductName} Version: {GeneralAppInfo.ApplicationVersion}")
                .AppendLine("Edition: " + (ApplicationEdition.IsPortable ? "Portable" : "MSI"))
                .AppendLine("Cmd line args: " + string.Join(" ", Environment.GetCommandLineArgs().Skip(1)))
                .ToString();
        }

        private void SetLanguage()
        {
            Text = Language.LoipvRemoteUnhandledException;
            labelExceptionCaught.Text = Language.UnhandledExceptionOccured;

            labelExceptionIsFatalHeader.Text = _isFatal
                ? Language.ExceptionForcesLoipvRemoteToClose
                : string.Empty;

            labelExceptionMessageHeader.Text = Language.ExceptionMessage;
            labelStackTraceHeader.Text = Language.StackTrace;
            labelEnvironment.Text = Language.Environment;
            buttonCreateBug.Text = Language.MenuItem_ReportIssue;
            buttonCopyAll.Text = Language.CopyAll;
            buttonClose.Text = _isFatal
                ? Language.Exit
                : Language._Close;
        }

        private void buttonCopyAll_Click(object? sender, EventArgs e)
        {
            string text = new StringBuilder()
               .AppendLine("```")
               .AppendLine(labelExceptionMessageHeader.Text)
               .AppendLine("\"" + textBoxExceptionMessage.Text + "\"")
               .AppendLine()
               .AppendLine(labelStackTraceHeader.Text)
               .AppendLine(textBoxStackTrace.Text)
               .AppendLine()
               .AppendLine(labelEnvironment.Text)
               .AppendLine(textBoxEnvironment.Text)
               .AppendLine("```")
               .ToString();

            Clipboard.SetText(text);
        }

        private void buttonClose_Click(object? sender, EventArgs e)
        {
            if (_isFatal)
                Shutdown.Quit();

            Close();
        }

        private void buttonCreateBug_Click(object? sender, EventArgs e)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = GeneralAppInfo.UrlBugs,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
    }
}
