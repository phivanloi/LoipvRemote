using LoipvRemote.Protocols.Putty;
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
                "PuTTYNG",
                "Release 0.84",
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

    }
}
