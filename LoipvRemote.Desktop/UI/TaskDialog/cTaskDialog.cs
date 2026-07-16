using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace LoipvRemote.UI.TaskDialog
{
    [SupportedOSPlatform("windows")]
    #region PUBLIC enums
    public enum ESysIcons
    {
        Information,
        Question,
        Warning,
        Error
    }

    public enum ETaskDialogButtons
    {
        YesNo,
        YesNoCancel,
        OkCancel,
        Ok,
        Close,
        Cancel,
        None
    }
    #endregion

    public static class CTaskDialog
    {
        // PUBLIC static values...
        public static bool VerificationChecked { get; private set; }
        public static int RadioButtonResult { get; private set; } = -1;
        public static int CommandButtonResult { get; private set; } = -1;
        public static int EmulatedFormWidth { get; set; } = 450;
        public static bool ForceEmulationMode { get; set; }
        public static bool UseToolWindowOnXp { get; set; } = true;
        public static bool PlaySystemSounds { get; set; } = true;
        public static event EventHandler? OnTaskDialogShown;
        public static event EventHandler? OnTaskDialogClosed;

        #region [ShowTaskDialogBox]

        [SupportedOSPlatform("windows")]
        public static DialogResult ShowTaskDialogBox(IWin32Window? owner, string? title, string? mainInstruction, string? content, string? expandedInfo, string? footer, string? verificationText, string? radioButtons, string? commandButtons, ETaskDialogButtons buttons, ESysIcons mainIcon, ESysIcons footerIcon, int defaultIndex)
        {
            DialogResult result;
            OnTaskDialogShown?.Invoke(null, EventArgs.Empty);

            using (frmTaskDialog td = new())
            {
                td.Title = title ?? string.Empty;
                td.MainInstruction = mainInstruction ?? string.Empty;
                td.Content = content ?? string.Empty;
                td.ExpandedInfo = expandedInfo ?? string.Empty;
                td.Footer = footer ?? string.Empty;
                td.RadioButtons = radioButtons ?? string.Empty;
                td.CommandButtons = commandButtons ?? string.Empty;
                td.Buttons = buttons;
                td.MainIcon = mainIcon;
                td.FooterIcon = footerIcon;
                td.VerificationText = verificationText ?? string.Empty;
                td.Width = EmulatedFormWidth;
                td.DefaultButtonIndex = defaultIndex;
                td.BuildForm();
                result = td.ShowDialog(owner);

                RadioButtonResult = td.RadioButtonIndex;
                CommandButtonResult = td.CommandButtonClickedIndex;
                VerificationChecked = td.VerificationCheckBoxChecked;
            }

            OnTaskDialogClosed?.Invoke(null, EventArgs.Empty);
            return result;
        }

        //--------------------------------------------------------------------------------
        // Overloaded versions...
        //--------------------------------------------------------------------------------
        [SupportedOSPlatform("windows")]
        public static DialogResult ShowTaskDialogBox(IWin32Window? owner,
                                                     string? title,
                                                     string? mainInstruction,
                                                     string? content,
                                                     string? expandedInfo,
                                                     string? footer,
                                                     string? verificationText,
                                                     string? radioButtons,
                                                     string? commandButtons,
                                                     ETaskDialogButtons buttons,
                                                     ESysIcons mainIcon,
                                                     ESysIcons footerIcon)
        {
            return ShowTaskDialogBox(owner, title, mainInstruction, content, expandedInfo, footer, verificationText, radioButtons, commandButtons, buttons, mainIcon, footerIcon, 0);
        }

        [SupportedOSPlatform("windows")]
        public static DialogResult ShowTaskDialogBox(string? title,
                                                     string? mainInstruction,
                                                     string? content,
                                                     string? expandedInfo,
                                                     string? footer,
                                                     string? verificationText,
                                                     string? radioButtons,
                                                     string? commandButtons,
                                                     ETaskDialogButtons buttons,
                                                     ESysIcons mainIcon,
                                                     ESysIcons footerIcon)
        {
            return ShowTaskDialogBox(null, title, mainInstruction, content, expandedInfo, footer, verificationText, radioButtons, commandButtons, buttons, mainIcon, footerIcon, 0);
        }

        #endregion

        #region [MessageBox]

        [SupportedOSPlatform("windows")]
        public static DialogResult MessageBox(IWin32Window? owner, string? title, string? mainInstruction, string? content, string? expandedInfo, string? footer, string? verificationText, ETaskDialogButtons buttons, ESysIcons mainIcon, ESysIcons footerIcon)
        {
            return ShowTaskDialogBox(owner, title, mainInstruction, content, expandedInfo, footer, verificationText, "", "", buttons, mainIcon, footerIcon);
        }

        //--------------------------------------------------------------------------------
        // Overloaded versions...
        //--------------------------------------------------------------------------------
        [SupportedOSPlatform("windows")]
        public static DialogResult MessageBox(string? title, string? mainInstruction, string? content, string? expandedInfo, string? footer, string? verificationText, ETaskDialogButtons buttons, ESysIcons mainIcon, ESysIcons footerIcon)
        {
            return ShowTaskDialogBox(null, title, mainInstruction, content, expandedInfo, footer, verificationText, "", "", buttons, mainIcon, footerIcon);
        }

        [SupportedOSPlatform("windows")]
        public static DialogResult MessageBox(IWin32Window? owner, string? title, string? mainInstruction, string? content, ETaskDialogButtons buttons, ESysIcons mainIcon)
        {
            return MessageBox(owner, title, mainInstruction, content, "", "", "", buttons, mainIcon, ESysIcons.Information);
        }

        [SupportedOSPlatform("windows")]
        public static DialogResult MessageBox(string? title, string? mainInstruction, string? content, ETaskDialogButtons buttons, ESysIcons mainIcon)
        {
            return MessageBox(null, title, mainInstruction, content, "", "", "", buttons, mainIcon, ESysIcons.Information);
        }

        //--------------------------------------------------------------------------------

        #endregion

        //--------------------------------------------------------------------------------

        #region [ShowRadioBox]

        //--------------------------------------------------------------------------------
        [SupportedOSPlatform("windows")]
        public static int ShowRadioBox(IWin32Window? owner, string? title, string? mainInstruction, string? content, string? expandedInfo, string? footer, string? verificationText, string? radioButtons, ESysIcons mainIcon, ESysIcons footerIcon, int defaultIndex)
        {
            DialogResult res = ShowTaskDialogBox(owner, title, mainInstruction, content, expandedInfo, footer, verificationText, radioButtons, "", ETaskDialogButtons.OkCancel, mainIcon, footerIcon, defaultIndex);
            if (res == DialogResult.OK)
                return RadioButtonResult;
            return -1;
        }

        //--------------------------------------------------------------------------------
        // Overloaded versions...
        //--------------------------------------------------------------------------------
        [SupportedOSPlatform("windows")]
        public static int ShowRadioBox(string? title, string? mainInstruction, string? content, string? expandedInfo, string? footer, string? verificationText, string? radioButtons, ESysIcons mainIcon, ESysIcons footerIcon, int defaultIndex)
        {
            DialogResult res = ShowTaskDialogBox(null, title, mainInstruction, content, expandedInfo, footer, verificationText, radioButtons, "", ETaskDialogButtons.OkCancel, mainIcon, footerIcon, defaultIndex);
            if (res == DialogResult.OK)
                return RadioButtonResult;
            return -1;
        }

        [SupportedOSPlatform("windows")]
        public static int ShowRadioBox(IWin32Window? owner, string? title, string? mainInstruction, string? content, string? expandedInfo, string? footer, string? verificationText, string? radioButtons, ESysIcons mainIcon, ESysIcons footerIcon)
        {
            return ShowRadioBox(owner, title, mainInstruction, content, expandedInfo, footer, verificationText, radioButtons, ESysIcons.Question, ESysIcons.Information, 0);
        }

        [SupportedOSPlatform("windows")]
        public static int ShowRadioBox(IWin32Window? owner, string? title, string? mainInstruction, string? content, string? radioButtons, int defaultIndex)
        {
            return ShowRadioBox(owner, title, mainInstruction, content, "", "", "", radioButtons, ESysIcons.Question, ESysIcons.Information, defaultIndex);
        }

        [SupportedOSPlatform("windows")]
        public static int ShowRadioBox(IWin32Window? owner, string? title, string? mainInstruction, string? content, string? radioButtons)
        {
            return ShowRadioBox(owner, title, mainInstruction, content, "", "", "", radioButtons, ESysIcons.Question, ESysIcons.Information, 0);
        }

        [SupportedOSPlatform("windows")]
        public static int ShowRadioBox(string? title, string? mainInstruction, string? content, string? radioButtons)
        {
            return ShowRadioBox(null, title, mainInstruction, content, "", "", "", radioButtons, ESysIcons.Question, ESysIcons.Information, 0);
        }

        #endregion

        //--------------------------------------------------------------------------------

        #region ShowCommandBox

        //--------------------------------------------------------------------------------
        [SupportedOSPlatform("windows")]
        public static int ShowCommandBox(IWin32Window? owner, string? title, string? mainInstruction, string? content, string? expandedInfo, string? footer, string? verificationText, string? commandButtons, bool showCancelButton, ESysIcons mainIcon, ESysIcons footerIcon)
        {
            DialogResult res = ShowTaskDialogBox(owner, title, mainInstruction, content, expandedInfo, footer, verificationText, "", commandButtons, showCancelButton ? ETaskDialogButtons.Cancel : ETaskDialogButtons.None, mainIcon, footerIcon);
            if (res == DialogResult.OK)
                return CommandButtonResult;
            return -1;
        }

        //--------------------------------------------------------------------------------
        // Overloaded versions...
        //--------------------------------------------------------------------------------
        [SupportedOSPlatform("windows")]
        public static int ShowCommandBox(string? title, string? mainInstruction, string? content, string? expandedInfo, string? footer, string? verificationText, string? commandButtons, bool showCancelButton, ESysIcons mainIcon, ESysIcons footerIcon)
        {
            DialogResult res = ShowTaskDialogBox(null, title, mainInstruction, content, expandedInfo, footer, verificationText, "", commandButtons, showCancelButton ? ETaskDialogButtons.Cancel : ETaskDialogButtons.None, mainIcon, footerIcon);
            if (res == DialogResult.OK)
                return CommandButtonResult;
            return -1;
        }

        [SupportedOSPlatform("windows")]
        public static int ShowCommandBox(IWin32Window? owner, string? title, string? mainInstruction, string? content, string? commandButtons, bool showCancelButton)
        {
            return ShowCommandBox(owner, title, mainInstruction, content, "", "", "", commandButtons, showCancelButton, ESysIcons.Question, ESysIcons.Information);
        }

        [SupportedOSPlatform("windows")]
        public static int ShowCommandBox(string? title, string? mainInstruction, string? content, string? commandButtons, bool showCancelButton)
        {
            return ShowCommandBox(null, title, mainInstruction, content, "", "", "", commandButtons, showCancelButton, ESysIcons.Question, ESysIcons.Information);
        }

        #endregion
    }
}
