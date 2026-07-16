using LoipvRemote.Connection;
using LoipvRemote.Container;
using System;
using System.Windows.Forms;
using LoipvRemote.Resources.Language;
using System.Runtime.Versioning;

namespace LoipvRemote.Tree
{
    [SupportedOSPlatform("windows")]
    public class SelectedConnectionDeletionConfirmer(Func<string, DialogResult> confirmationFunc) : IConfirm<ConnectionInfo>
    {
        private readonly Func<string, DialogResult> _confirmationFunc = confirmationFunc;

        public bool Confirm(ConnectionInfo deletionTarget)
        {
            if (deletionTarget == null)
                return false;

            ContainerInfo? deletionTargetAsContainer = deletionTarget as ContainerInfo;
            if (deletionTargetAsContainer != null)
                return deletionTargetAsContainer.HasChildren()
                    ? UserConfirmsNonEmptyFolderDeletion(deletionTargetAsContainer)
                    : UserConfirmsEmptyFolderDeletion(deletionTargetAsContainer);
            return UserConfirmsConnectionDeletion(deletionTarget);
        }

        private bool UserConfirmsEmptyFolderDeletion(AbstractConnectionRecord deletionTarget)
        {
            string messagePrompt = FormatText(Language.ConfirmDeleteNodeFolder, deletionTarget.Name);
            return PromptUser(messagePrompt);
        }

        private bool UserConfirmsNonEmptyFolderDeletion(AbstractConnectionRecord deletionTarget)
        {
            string messagePrompt = FormatText(Language.ConfirmDeleteNodeFolderNotEmpty, deletionTarget.Name);
            return PromptUser(messagePrompt);
        }

        private bool UserConfirmsConnectionDeletion(AbstractConnectionRecord deletionTarget)
        {
            string messagePrompt = FormatText(Language.ConfirmDeleteNodeConnection, deletionTarget.Name);
            return PromptUser(messagePrompt);
        }

        private bool PromptUser(string promptMessage)
        {
            DialogResult msgBoxResponse = _confirmationFunc(promptMessage);
            return msgBoxResponse == DialogResult.Yes;
        }
    }
}
