using LoipvRemote.Connection;
using LoipvRemote.UI.Window;
using NUnit.Framework;
using System;
using System.Reflection;
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

        [Test]
        public void ActiveContentChangedDoesNotThrowBeforeProtocolSurfaceExists()
        {
            using ConnectionWindow window = new(new DockContent());
            MethodInfo handler = typeof(ConnectionWindow).GetMethod(
                "ConnDockOnActiveContentChanged",
                BindingFlags.Instance | BindingFlags.NonPublic)!;

            Assert.DoesNotThrow(() => handler.Invoke(window, new object?[] { null, EventArgs.Empty }));
        }

        [Test]
        public void InterfaceLookupReturnsNullAfterWindowDisposal()
        {
            ConnectionWindow window = new(new DockContent());
            window.Dispose();

            Assert.That(window.TryGetInterfaceControl(), Is.Null);
        }

        [Test]
        public void InterfaceLookupReturnsNullWhenNestedDockIsDisposed()
        {
            using ConnectionWindow window = new(new DockContent());
            window.connDock.Dispose();

            Assert.DoesNotThrow(() =>
                Assert.That(InterfaceControl.FindInterfaceControl(window.connDock), Is.Null));
        }
    }
}
