using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace LoipvRemote.Protocols.ExternalApps;

/// <summary>
/// Hosts a console process in a managed WinForms control.
///
/// This deliberately does not embed a foreign console window.  Foreign window
/// parenting loses keyboard focus when tabs are activated or resized; input and
/// output instead stay on the UI thread owned by the connection tab.
/// </summary>
public sealed class ExternalConsoleRuntime : IDisposable
{
    private readonly ManagedTerminalControl _control;
    private Process? _process;
    private bool _disposed;

    public ExternalConsoleRuntime(Color backgroundColor)
    {
        _control = new ManagedTerminalControl(backgroundColor);
    }

    public Control Control => _control;
    public IntPtr Handle => _control.Handle;
    public bool IsHandleCreated => _control.IsHandleCreated;

    /// <summary>Writes terminal input through the same redirected stream used by keyboard events.</summary>
    public void SendInput(string value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(value);
        _control.SendInput(value);
    }

    public void StartProcess(string fileName, string arguments)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (_process is not null)
            throw new InvalidOperationException("A console process has already been started for this session.");

        // Force a real WinForms handle before output arrives. This makes focus
        // ownership deterministic even when a tab is created in the background.
        _control.CreateControl();

        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };

        _process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        _process.Exited += (_, _) => _control.AppendOutput(Environment.NewLine + "[process exited]" + Environment.NewLine);

        if (!_process.Start())
            throw new InvalidOperationException($"Could not start console process '{fileName}'.");

        _control.AttachInput(_process.StandardInput);
        _ = PumpOutputAsync(_process.StandardOutput);
        _ = PumpOutputAsync(_process.StandardError);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _control.DetachInput();

        if (_process is not null)
        {
            try
            {
                if (!_process.HasExited)
                    _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process exited while the session was closing.
            }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }

        _control.Dispose();
    }

    private async Task PumpOutputAsync(StreamReader reader)
    {
        char[] buffer = new char[1024];
        try
        {
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false)) > 0)
                _control.AppendOutput(new string(buffer, 0, count));
        }
        catch (ObjectDisposedException)
        {
            // Expected when a connection closes while output is still being read.
        }
        catch (InvalidOperationException)
        {
            // Expected when the redirected stream is closed by the child process.
        }
    }

    private sealed class ManagedTerminalControl : RichTextBox
    {
        private readonly Queue<string> _pendingOutput = [];
        private TextWriter? _input;

        public ManagedTerminalControl(Color backgroundColor)
        {
            Dock = DockStyle.Fill;
            BackColor = backgroundColor;
            ForeColor = Color.White;
            BorderStyle = BorderStyle.None;
            DetectUrls = false;
            ReadOnly = true;
            ShortcutsEnabled = false;
            WordWrap = false;
            Font = new Font(FontFamily.GenericMonospace, 10.0F);
            TabStop = true;
            AccessibleName = "Console session";
        }

        public void AttachInput(TextWriter input) => _input = input ?? throw new ArgumentNullException(nameof(input));

        public void DetachInput() => _input = null;

        public void SendInput(string value) => WriteInput(value);

        public void AppendOutput(string value)
        {
            if (IsDisposed || Disposing || string.IsNullOrEmpty(value))
                return;

            if (!IsHandleCreated)
            {
                lock (_pendingOutput)
                    _pendingOutput.Enqueue(value);
                return;
            }

            if (InvokeRequired)
            {
                try { BeginInvoke((Action)(() => AppendOutput(value))); }
                catch (InvalidOperationException) { }
                return;
            }

            SelectionStart = TextLength;
            SelectionLength = 0;
            AppendText(value);
            SelectionStart = TextLength;
            ScrollToCaret();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            lock (_pendingOutput)
            {
                while (_pendingOutput.TryDequeue(out string? value))
                    AppendOutput(value);
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (_input is not null && !char.IsControl(e.KeyChar))
            {
                WriteInput(e.KeyChar.ToString());
                e.Handled = true;
            }

            base.OnKeyPress(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            string? sequence = e.KeyCode switch
            {
                Keys.Enter => "\r\n",
                Keys.Back => "\b",
                Keys.Tab => "\t",
                Keys.Escape => "\u001b",
                Keys.Up => "\u001b[A",
                Keys.Down => "\u001b[B",
                Keys.Right => "\u001b[C",
                Keys.Left => "\u001b[D",
                Keys.Home => "\u001b[H",
                Keys.End => "\u001b[F",
                Keys.Delete => "\u001b[3~",
                _ when e.Control && e.KeyCode == Keys.C => "\u0003",
                _ when e.Control && e.KeyCode == Keys.D => "\u0004",
                _ => null
            };

            if (sequence is not null && _input is not null)
            {
                WriteInput(sequence);
                e.SuppressKeyPress = true;
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            base.OnMouseDown(e);
        }

        private void WriteInput(string value)
        {
            try
            {
                _input?.Write(value);
                _input?.Flush();
            }
            catch (ObjectDisposedException)
            {
                DetachInput();
            }
            catch (InvalidOperationException)
            {
                DetachInput();
            }
        }
    }
}
