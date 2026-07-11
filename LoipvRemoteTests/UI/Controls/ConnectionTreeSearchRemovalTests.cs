using System.Linq;
using System.Reflection;
using LoipvRemote.Properties;
using LoipvRemote.UI.Window;
using NUnit.Framework;
using ConnectionTreeControl = LoipvRemote.UI.Controls.ConnectionTree.ConnectionTree;

namespace LoipvRemoteTests.UI.Controls;

[TestFixture]
public class ConnectionTreeSearchRemovalTests
{
    [Test]
    public void ConnectionTreeDoesNotExposeSearchOrFilteringApi()
    {
        Assert.That(typeof(ConnectionTreeControl).GetProperty("NodeSearcher"), Is.Null);
        Assert.That(typeof(ConnectionTreeControl).GetMethod("ApplyFilter"), Is.Null);
        Assert.That(typeof(ConnectionTreeControl).GetMethod("RemoveFilter"), Is.Null);
    }

    [Test]
    public void ConnectionTreeWindowDoesNotContainSearchControls()
    {
        string[] fieldNames = typeof(ConnectionTreeWindow)
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(field => field.Name)
            .ToArray();

        Assert.That(fieldNames, Does.Not.Contain("txtSearch"));
        Assert.That(fieldNames, Does.Not.Contain("pbSearch"));
        Assert.That(fieldNames, Does.Not.Contain("searchBoxLayoutPanel"));
    }

    [Test]
    public void ApplicationSettingsDoNotExposeConnectionTreeSearchOptions()
    {
        Assert.That(typeof(Settings).GetProperty("UseFilterSearch"), Is.Null);
        Assert.That(typeof(Settings).GetProperty("PlaceSearchBarAboveConnectionTree"), Is.Null);
    }
}
