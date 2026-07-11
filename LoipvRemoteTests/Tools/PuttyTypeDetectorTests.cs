using LoipvRemote.Tools;
using NUnit.Framework;

namespace LoipvRemoteTests.Tools
{
    [TestFixture]
    public class PuttyTypeDetectorTests
    {
        [Test]
        public void ClassifyTreatsBundledPuttyNgWithPuttyInternalNameAsPuttyNg()
        {
            PuttyTypeDetector.PuttyType result = PuttyTypeDetector.Classify(
                "C:\\Program Files\\LoipvRemote\\PuTTYNG.exe",
                "PuTTY",
                "Release 0.84 mRemoteNG (LoipvRemote)",
                "");

            Assert.That(result, Is.EqualTo(PuttyTypeDetector.PuttyType.PuttyNg));
        }

        [Test]
        public void ClassifyKeepsOfficialPuttyAsPutty()
        {
            PuttyTypeDetector.PuttyType result = PuttyTypeDetector.Classify(
                "C:\\Tools\\putty.exe",
                "PuTTY",
                "Release 0.84",
                "");

            Assert.That(result, Is.EqualTo(PuttyTypeDetector.PuttyType.Putty));
        }

        [Test]
        public void ClassifyKeepsTheLegacyPuttyNgBuildOnTheFallbackPath()
        {
            PuttyTypeDetector.PuttyType result = PuttyTypeDetector.Classify(
                "C:\\Program Files\\LoipvRemote\\PuTTYNG.exe",
                "PuTTY",
                "Release 0.83 mRemoteNG",
                "");

            Assert.That(result, Is.EqualTo(PuttyTypeDetector.PuttyType.Putty));
        }
    }
}
