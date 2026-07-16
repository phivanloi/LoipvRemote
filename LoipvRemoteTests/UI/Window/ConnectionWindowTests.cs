using LoipvRemote.Connection;
using LoipvRemote.UI.Window;
using NUnit.Framework;
using WeifenLuo.WinFormsUI.Docking;

namespace LoipvRemoteTests.UI.Window
{
    [TestFixture]
    public sealed class ConnectionWindowTests
    {
        [Test]
        public void ActiveInterfaceLookupReturnsNullWhileTheWindowIsEmpty()
        {
            using ConnectionWindow window = new(new DockContent());

            Assert.That(window.TryGetInterfaceControl(), Is.Null);
            Assert.That(InterfaceControl.FindInterfaceControl(window.connDock), Is.Null);
        }
    }
}
