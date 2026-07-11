using System.Threading;
using System.Windows.Forms;
using LoipvRemoteTests.TestHelpers;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Forms.OptionsPages
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class OptionsStartupExitPageTests : OptionsFormSetupAndTeardown
    {
        [Test]
        public void StartupExitPageLinkExistsInListView()
        {
            ListViewTester listViewTester = new("lstOptionPages", _optionsForm);
            Assert.That(listViewTester.Items[0].Text, Does.Match("Startup/Exit"));
        }

        [Test]
        public void IconShownInListView()
        {
            ListViewTester listViewTester = new("lstOptionPages", _optionsForm);
            Assert.That(listViewTester.Items[0].ImageList, Is.Not.Null);
        }

        [Test]
        public void SelectingStartupExitPageLoadsSettings()
        {
            ListViewTester listViewTester = new("lstOptionPages", _optionsForm);
            listViewTester.Select("Startup/Exit");
            CheckBox checkboxTester = _optionsForm.FindControl<CheckBox>("chkSaveConsOnExit");
            Assert.That(checkboxTester.Text, Does.Match("Save connections"));
        }
    }
}