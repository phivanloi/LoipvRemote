using System.Collections.Generic;
using LoipvRemote.Connection;
using LoipvRemote.UI.Window;
using NUnit.Framework;

namespace LoipvRemoteTests.UI.Window.ConfigWindowTests
{
	public abstract class ConfigWindowSpecialTestsBase
    {
        protected abstract ProtocolKind Protocol { get; }
        protected bool TestAgainstContainerInfo { get; set; } = false;
        protected ConfigWindow ConfigWindow;
        protected ConnectionInfo ConnectionInfo;
        protected List<string> ExpectedPropertyList;

        [SetUp]
        public virtual void Setup()
        {
            ConnectionInfo = ConfigWindowGeneralTests.ConstructConnectionInfo(Protocol, TestAgainstContainerInfo);
            ExpectedPropertyList = ConfigWindowGeneralTests.BuildExpectedConnectionInfoPropertyList(Protocol, TestAgainstContainerInfo);

            ConfigWindow = new ConfigWindow();
        }

        public void RunVerification()
        {
            ConfigWindow.SelectedTreeNode = ConnectionInfo;
            Assert.That(
                ConfigWindow.VisibleObjectProperties,
                Is.EquivalentTo(ExpectedPropertyList));
        }
    }
}
