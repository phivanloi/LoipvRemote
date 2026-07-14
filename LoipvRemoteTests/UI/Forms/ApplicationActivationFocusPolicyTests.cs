using LoipvRemote.Infrastructure.Windows.Interop;
using LoipvRemote.Desktop.Shell;
using LoipvRemote.UI.Forms;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Forms
{
    [TestFixture]
    public class ApplicationActivationFocusPolicyTests
    {
        [Test]
        public void RestoresTheActiveConnectionWhenTheApplicationIsReactivated()
        {
            Assert.That(ApplicationActivationFocusPolicy.ShouldRestoreActiveConnectionFocus(
                            NativeMethods.WM_ACTIVATEAPP,
                            new System.IntPtr(1)),
                        Is.True);
        }

        [Test]
        public void DoesNotRestoreTheActiveConnectionWhenTheApplicationIsDeactivated()
        {
            Assert.That(ApplicationActivationFocusPolicy.ShouldRestoreActiveConnectionFocus(
                            NativeMethods.WM_ACTIVATEAPP,
                            System.IntPtr.Zero),
                        Is.False);
        }

        [Test]
        public void IgnoresOtherWindowMessages()
        {
            Assert.That(ApplicationActivationFocusPolicy.ShouldRestoreActiveConnectionFocus(
                            NativeMethods.WM_ACTIVATE,
                            new System.IntPtr(NativeMethods.WA_ACTIVE)),
                        Is.False);
        }
    }
}
