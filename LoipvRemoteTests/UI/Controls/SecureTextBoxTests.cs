using System.Threading;
using LoipvRemote.Security;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Controls
{
    [Apartment(ApartmentState.STA)]
    public class SecureTextBoxTests
    {
        private SecureTextBoxTestForm _testForm;

        [SetUp]
        public void Setup()
        {
            _testForm = new SecureTextBoxTestForm();
            _testForm.Show();
        }

        [TearDown]
        public void TearDown()
        {
            _testForm.Close();
            while (_testForm.Disposing) { }
            _testForm = null;
        }

        [Test]
        public void TextboxInputGetsAddedToSecureString()
        {
            var textBox = _testForm.secureTextBox1;
            const string textToSend = "abc123";
            textBox.Text = textToSend;
            Assert.That(textBox.SecString.ConvertToUnsecureString(), Is.EqualTo(textToSend));
        }
    }
}