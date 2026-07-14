using System.Threading;
using System.Windows.Forms;
using LoipvRemoteTests.TestHelpers;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Forms.OptionsPages
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    public class OptionsThemePageTests : OptionsFormSetupAndTeardown
    {
        [Test]
        public void ThemePageLinkExistsInListView()
        {
            ListViewTester listViewTester = new("lstOptionPages", _optionsForm);
            Assert.That(listViewTester.Items[7].Text, Does.Match("Theme"));
        }

        [Test]
        public void ThemeIconShownInListView()
        {
            ListViewTester listViewTester = new("lstOptionPages", _optionsForm);
            Assert.That(listViewTester.Items[7].ImageList, Is.Not.Null);
        }

        [Test]
        public void SelectingThemePageLoadsSettings()
        {
            ListViewTester listViewTester = new("lstOptionPages", _optionsForm);
            listViewTester.Select("Theme");
            Button buttonTester = _optionsForm.FindControl<Button>("btnThemeNew");
            Assert.That(buttonTester.Text, Does.Match("New"));
        }
    }
}
