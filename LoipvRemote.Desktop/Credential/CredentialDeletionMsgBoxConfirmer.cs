using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using LoipvRemote.Tree;
using LoipvRemote.Resources.Language;


namespace LoipvRemote.Credential
{
    public class CredentialDeletionMsgBoxConfirmer : IConfirm<IEnumerable<ICredentialRecord>>
    {
        private readonly Func<string, string, MessageBoxButtons, MessageBoxIcon, DialogResult> _confirmationFunc;

        public CredentialDeletionMsgBoxConfirmer(
            Func<string, string, MessageBoxButtons, MessageBoxIcon, DialogResult> confirmationFunc)
        {
            ArgumentNullException.ThrowIfNull(confirmationFunc);

            _confirmationFunc = confirmationFunc;
        }

        public bool Confirm(IEnumerable<ICredentialRecord> confirmationTargets)
        {
            ICredentialRecord[] targetsArray = confirmationTargets.ToArray();
            if (targetsArray.Length == 0) return false;
            if (targetsArray.Length > 1)
                return PromptUser(FormatText("Are you sure you want to delete these {0} selected credentials?", targetsArray.Length));
            return PromptUser(FormatText(Language.ConfirmDeleteCredentialRecord, targetsArray.First().Title));
        }

        private bool PromptUser(string promptMessage)
        {
            DialogResult msgBoxResponse = _confirmationFunc.Invoke(promptMessage, Application.ProductName ?? string.Empty, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            return msgBoxResponse == DialogResult.Yes;
        }
    }
}
