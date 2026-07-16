using System;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using LoipvRemote.Infrastructure.Windows.Interop;
using System.Runtime.Versioning;

namespace LoipvRemote.Tools
{
    [SupportedOSPlatform("windows")]
    public class WindowPlacement(Form form)
    {
        private Form _form = form ?? throw new ArgumentNullException(nameof(form));


        #region Public Properties

        public Form Form
        {
            get => _form;
            set => _form = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool RestoreToMaximized
        {
            get
            {
                NativeMethods.WINDOWPLACEMENT windowPlacement = GetWindowPlacement();
                return Convert.ToBoolean(windowPlacement.flags & NativeMethods.WPF_RESTORETOMAXIMIZED);
            }
            set
            {
                NativeMethods.WINDOWPLACEMENT windowPlacement = GetWindowPlacement();
                if (value)
                {
                    windowPlacement.flags = windowPlacement.flags | NativeMethods.WPF_RESTORETOMAXIMIZED;
                }
                else
                {
                    windowPlacement.flags = windowPlacement.flags & ~NativeMethods.WPF_RESTORETOMAXIMIZED;
                }

                SetWindowPlacement(windowPlacement);
            }
        }

        #endregion

        #region Private Functions

        private NativeMethods.WINDOWPLACEMENT GetWindowPlacement()
        {
            NativeMethods.WINDOWPLACEMENT windowPlacement = new();
            windowPlacement.length = (uint)Marshal.SizeOf(windowPlacement);
            try
            {
                NativeMethods.GetWindowPlacement(_form.Handle, ref windowPlacement);
                return windowPlacement;
            }
            catch (Exception)
            {
                throw;
            }
        }

        private bool SetWindowPlacement(NativeMethods.WINDOWPLACEMENT windowPlacement)
        {
            windowPlacement.length = (uint)Marshal.SizeOf(windowPlacement);
            try
            {
                return NativeMethods.SetWindowPlacement(_form.Handle, ref windowPlacement);
            }
            catch (Exception)
            {
                throw;
            }
        }

        #endregion
    }
}
