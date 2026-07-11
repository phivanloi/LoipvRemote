using System.Threading;
using LoipvRemote.UI.Window;
using NUnit.Framework;
using WeifenLuo.WinFormsUI.Docking;


namespace LoipvRemoteTests.UI.Window
{
    [Apartment(ApartmentState.STA)]
    public class ConnectionTreeWindowTests
    {
        private ConnectionTreeWindow _connectionTreeWindow;

        [SetUp]
        public void Setup()
        {
            _connectionTreeWindow = new ConnectionTreeWindow(new DockContent());
        }

        [TearDown]
        public void Teardown()
        {
            _connectionTreeWindow.Close();
        }

        [Test, Apartment(ApartmentState.STA)]
        public void CanShowWindow()
        {
            _connectionTreeWindow.Show();
            Assert.That(_connectionTreeWindow.Visible);
        }
    }
}