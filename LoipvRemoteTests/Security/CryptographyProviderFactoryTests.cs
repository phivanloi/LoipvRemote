using System;
using System.Collections;
using LoipvRemote.Security;
using LoipvRemote.Security.Factories;
using NUnit.Framework;


namespace LoipvRemoteTests.Security
{
    [TestFixture]
    public class CryptographyProviderFactoryTests
    {
        [TestCaseSource(typeof(TestCaseSources), nameof(TestCaseSources.AllEngineAndModeCombos))]
        public void CanCreateAeadProvidersWithCorrectEngine(BlockCipherEngines engine, BlockCipherModes mode)
        {
            var cryptoProvider = new CryptoProviderFactory(engine, mode).Build();
            Assert.That(cryptoProvider.CipherEngine, Is.EqualTo(engine));
        }

        [TestCaseSource(typeof(TestCaseSources), nameof(TestCaseSources.AllEngineAndModeCombos))]
        public void CanCreateAeadProvidersWithCorrectMode(BlockCipherEngines engine, BlockCipherModes mode)
        {
            var cryptoProvider = new CryptoProviderFactory(engine, mode).Build();
            Assert.That(cryptoProvider.CipherMode, Is.EqualTo(mode));
        }

        private class TestCaseSources
        {
            public static IEnumerable AllEngineAndModeCombos
            {
                get
                {
                    foreach (var engine in Enum.GetValues(typeof(BlockCipherEngines)))
                    {
                        foreach (var mode in Enum.GetValues(typeof(BlockCipherModes)))
                        {
                            yield return new TestCaseData(engine, mode);
                        }
                    }
                }
            }
        }
    }
}