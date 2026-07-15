using System.Threading;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Window.ConfigWindowTests
{
    [Apartment(ApartmentState.STA)]
    public class ConfigWindowVncSpecialTests : ConfigWindowSpecialTestsBase
    {
        protected override ProtocolKind Protocol => ProtocolKind.Vnc;

        [Test]
        public void UserDomainPropertiesShown_WhenAuthModeIsWindows()
        {
            ConnectionInfo.VNCAuthMode = VncAuthMode.AuthWin;
            ExpectedPropertyList.AddRange(new []
            {
                nameof(ConnectionInfo.Username),
                nameof(ConnectionInfo.Domain),
            });
        }

        [TestCase(VncProxyType.ProxyHTTP)]
        [TestCase(VncProxyType.ProxySocks5)]
        [TestCase(VncProxyType.ProxyUltra)]
        public void ProxyPropertiesShown_WhenProxyModeIsNotNone(VncProxyType proxyType)
        {
            ConnectionInfo.VNCProxyType = proxyType;
            ExpectedPropertyList.AddRange(new[]
            {
                nameof(ConnectionInfo.VNCProxyIP),
                nameof(ConnectionInfo.VNCProxyPort),
                nameof(ConnectionInfo.VNCProxyUsername),
                nameof(ConnectionInfo.VNCProxyPassword),
            });
        }
    }
}
