using System.Threading;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Window.ConfigWindowTests
{
    [Apartment(ApartmentState.STA)]
    public class ConfigWindowRdpSpecialTests : ConfigWindowSpecialTestsBase
    {
        protected override ProtocolKind Protocol => ProtocolKind.Rdp;

        [Test]
        public void PropertyShownWhenActive_RdpMinutesToIdleTimeout()
        {
            ConnectionInfo.RDPMinutesToIdleTimeout = 1;
            ExpectedPropertyList.Add(nameof(LoipvRemote.Connection.ConnectionInfo.RDPAlertIdleTimeout));

            RunVerification();
        }

        [TestCase(RDGatewayUsageMethod.Always)]
        [TestCase(RDGatewayUsageMethod.Detect)]
        public void RdGatewayPropertiesShown_WhenRdGatewayUsageMethodIsNotNever(RDGatewayUsageMethod gatewayUsageMethod)
        {
            ConnectionInfo.RDGatewayUsageMethod = gatewayUsageMethod;
            ConnectionInfo.RDGatewayUseConnectionCredentials = RDGatewayUseConnectionCredentials.Yes;
            ExpectedPropertyList.AddRange(new[]
            {
                nameof(LoipvRemote.Connection.ConnectionInfo.RDGatewayHostname),
                nameof(LoipvRemote.Connection.ConnectionInfo.RDGatewayUseConnectionCredentials),
            });
            ExpectedPropertyList.Remove(nameof(LoipvRemote.Connection.ConnectionInfo.RDGatewayUserViaAPI));
            ExpectedPropertyList.Remove(nameof(LoipvRemote.Connection.ConnectionInfo.RDGatewayExternalCredentialProvider));

            RunVerification();
        }

        [TestCase(RDGatewayUseConnectionCredentials.No)]
        [TestCase(RDGatewayUseConnectionCredentials.SmartCard)]
        public void RdGatewayPropertiesShown_WhenRDGatewayUseConnectionCredentialsIsNotYes(RDGatewayUseConnectionCredentials useConnectionCredentials)
        {
            ConnectionInfo.RDGatewayUsageMethod = RDGatewayUsageMethod.Always;
            ConnectionInfo.RDGatewayUseConnectionCredentials = useConnectionCredentials;
            switch (useConnectionCredentials)
            {
                case RDGatewayUseConnectionCredentials.No:
                    ExpectedPropertyList.AddRange(new[]
                    {
                        nameof(LoipvRemote.Connection.ConnectionInfo.RDGatewayHostname),
                        nameof(LoipvRemote.Connection.ConnectionInfo.RDGatewayUsername),
                        nameof(LoipvRemote.Connection.ConnectionInfo.RDGatewayPassword),
                        nameof(LoipvRemote.Connection.ConnectionInfo.RDGatewayDomain),
                        nameof(LoipvRemote.Connection.ConnectionInfo.RDGatewayUseConnectionCredentials),
                        nameof(LoipvRemote.Connection.ConnectionInfo.RDGatewayAccessToken)
                    });
                    break;
                case RDGatewayUseConnectionCredentials.SmartCard:
                    ExpectedPropertyList.AddRange(new[]
                    {
                        nameof(LoipvRemote.Connection.ConnectionInfo.RDGatewayHostname),
                        nameof(LoipvRemote.Connection.ConnectionInfo.RDGatewayUseConnectionCredentials)
                    });
                    ExpectedPropertyList.Remove(nameof(LoipvRemote.Connection.ConnectionInfo.RDGatewayUserViaAPI));
                    ExpectedPropertyList.Remove(nameof(LoipvRemote.Connection.ConnectionInfo.RDGatewayExternalCredentialProvider));
                    break;
            }

            RunVerification();
        }

        [Test]
        public void SoundQualityPropertyShown_WhenRdpSoundsSetToBringToThisComputer()
        {
            ConnectionInfo.RedirectSound = RDPSounds.BringToThisComputer;
            ExpectedPropertyList.Add(nameof(LoipvRemote.Connection.ConnectionInfo.SoundQuality));

            RunVerification();
        }

        [TestCase(RDPResolutions.Fullscreen)]
        public void AutomaticResizePropertyShown_WhenResolutionIsDynamic(RDPResolutions resolution)
        {
            ConnectionInfo.Resolution = resolution;
            ExpectedPropertyList.Add(nameof(LoipvRemote.Connection.ConnectionInfo.AutomaticResize));

            RunVerification();
        }
    }
}
