namespace LoipvRemote.Desktop.UI.Connectors.AWS
{
    public partial class AWSConnectionForm : Form
    {
        public AWSConnectionForm()
        {
            InitializeComponent();

        }

        private void AWSConnectionForm_Activated(object? sender, EventArgs e)
        {
            tbAccesKeyID.Focus();
        }
    }
}
