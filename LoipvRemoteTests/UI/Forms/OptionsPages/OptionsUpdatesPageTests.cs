using System.Threading;
using System.Windows.Forms;
using LoipvRemoteTests.TestHelpers;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Forms.OptionsPages
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class OptionsUpdatesPageTests : OptionsFormSetupAndTeardown
    {
        [Test]
        public void UpdatesPageLinkExistsInListView()
        {
            ListViewTester listViewTester = new("lstOptionPages", _optionsForm);
            Assert.That(listViewTester.Items[7].Text, Does.Match("Updates"));
        }

        [Test]
        public void UpdatesIconShownInListView()
        {
            ListViewTester listViewTester = new("lstOptionPages", _optionsForm);
            Assert.That(listViewTester.Items[7].ImageList, Is.Not.Null);
        }

        [Test]
        public void SelectingUpdatesPageLoadsSettings()
        {
            ListViewTester listViewTester = new("lstOptionPages", _optionsForm);
            listViewTester.Select("Updates");
            CheckBox checkboxTester = _optionsForm.FindControl<CheckBox>("chkCheckForUpdatesOnStartup");
            Assert.That(checkboxTester.Text, Does.Match("Check for updates"));
        }
    }
}