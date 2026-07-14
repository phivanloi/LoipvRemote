using LoipvRemote.Resources.Language;
using LoipvRemote.Tools;
using NUnit.Framework;

namespace LoipvRemoteTests.Domain.Protocols;

public class RdpOptionPresentationTests
{
    [TestCase(RDGatewayUsageMethod.Never, 0)]
    [TestCase(RDGatewayUsageMethod.Always, 1)]
    [TestCase(RDGatewayUseConnectionCredentials.AccessToken, 4)]
    [TestCase(RDPColors.Colors256, 8)]
    [TestCase(RDPColors.Colors32Bit, 32)]
    [TestCase(RDPDiskDrives.Custom, 3)]
    [TestCase(RDPResolutions.Fullscreen, 2)]
    [TestCase(RDPSoundQuality.High, 2)]
    [TestCase(RDPSounds.DoNotPlay, 2)]
    [TestCase(RDPPerformanceFlags.EnableDesktopComposition, 0x100)]
    public void PersistedOptionValues_AreStable(Enum option, int expectedValue)
    {
        Assert.That(Convert.ToInt32(option), Is.EqualTo(expectedValue));
    }

    [Test]
    public void EnumTypeConverter_ResolvesDomainDisplayKeyThroughUiResources()
    {
        var converter = new MiscTools.EnumTypeConverter(typeof(AuthenticationLevel));

        string displayValue = (string)converter.ConvertTo(
            context: null,
            culture: null,
            value: AuthenticationLevel.AuthRequired,
            destType: typeof(string));

        Assert.That(displayValue, Is.EqualTo(Language.DontConnectWhenAuthFails));
    }
}
