using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Runtime.Versioning;
using System.Windows.Forms.Design;
using LoipvRemote.Credential;

namespace LoipvRemote.UI.Controls.Adapters
{
    [SupportedOSPlatform("windows")]
    public class CredentialRecordListAdaptor : UITypeEditor
    {
        private static Func<ICredentialRepositoryList>? s_catalogSource;
        private IWindowsFormsEditorService? _editorService;

        public static void Configure(Func<ICredentialRepositoryList> catalogSource)
        {
            s_catalogSource = catalogSource ?? throw new ArgumentNullException(nameof(catalogSource));
        }

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext? context)
        {
            return UITypeEditorEditStyle.DropDown;
        }

        public override object? EditValue(ITypeDescriptorContext? context, IServiceProvider provider, object? value)
        {
            if (provider == null) return value;
            _editorService = provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
            if (_editorService == null) return value;

            Credential.ICredentialRepositoryList credentialManager = s_catalogSource?.Invoke()
                ?? throw new InvalidOperationException("CredentialRecordListAdaptor has not been configured by the desktop host.");

            CredentialRecordListBox listBox = new(credentialManager.GetCredentialRecords());
            listBox.SelectedValueChanged += ListBoxOnSelectedValueChanged;

            _editorService.DropDownControl(listBox);
            if (listBox.SelectedItem == listBox.NoneSelection)
                return null!;
            if (listBox.SelectedItem == listBox.AddNewSelection)
            {
                //var newCred = new CredentialRecord();
                //credentialManager.Add(newCred);
                //return newCred;
            }

            return listBox.SelectedItem ?? value;
        }

        private void ListBoxOnSelectedValueChanged(object? sender, EventArgs eventArgs)
        {
            _editorService?.CloseDropDown();
        }
    }
}
