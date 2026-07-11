using System.Threading;
using System.Windows.Forms;
using LoipvRemoteTests.TestHelpers;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Forms.OptionsPages
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class OptionsAppearancePageTests : OptionsFormSetupAndTeardown
    {
        [Test]
        public void AppearancePageLinkExistsInListView()
        {
            ListViewTester listViewTester = new("lstOptionPages", _optionsForm);
            Assert.That(listViewTester.Items[1].Text, Does.Match("Appearance"));
        }

        [Test]
        public void IconShownInListView()
        {
            ListViewTester listViewTester = new("lstOptionPages", _optionsForm);
            Assert.That(listViewTester.Items[1].ImageList, Is.Not.Null);
        }

        [Test]
        public void SelectingAppearancePageLoadsSettings()
        {
            ListViewTester listViewTester = new("lstOptionPages", _optionsForm);
            listViewTester.Select("Appearance");
            CheckBox checkboxTester = _optionsForm.FindControl<CheckBox>("chkShowSystemTrayIcon");
            Assert.That(checkboxTester.Text, Does.Match("show notification area icon"));
        }
    }
}