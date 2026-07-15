using System;
using System.Linq;
using LoipvRemote.Config.Serializers.ConnectionSerializers.Csv;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Credential;
using LoipvRemote.Security;
using LoipvRemote.Tree;
using LoipvRemoteTests.TestHelpers;
using NSubstitute;
using NUnit.Framework;

namespace LoipvRemoteTests.Config.Serializers.ConnectionSerializers.Csv;

public class CsvConnectionsSerializerTests
{
    private ICredentialRepositoryList _credentialRepositoryList;
    private const string ConnectionName = "myconnection";
    private const string Username = "myuser";
    private const string Domain = "mydomain";
    private const string Password = "mypass123";

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        var credRecord = Substitute.For<ICredentialRecord>();
        credRecord.Username.Returns(Username);
        credRecord.Domain.Returns(Domain);
        credRecord.Password.Returns(Password.ConvertToSecureString());
        _credentialRepositoryList = Substitute.For<ICredentialRepositoryList>();
        _credentialRepositoryList.GetCredentialRecord(new Guid()).ReturnsForAnyArgs(credRecord);
    }

    [Test]
    public void SerializesNodeId()
    {
        var serializer = new CsvConnectionsSerializer(new SaveFilter(), _credentialRepositoryList);
        var connectionInfo = BuildConnectionInfo();
        var csv = serializer.Serialize(connectionInfo);
        Assert.That(csv, Does.Match(connectionInfo.ConstantID));
    }

    [Test]
    public void DoesntSerializeTheRootNode()
    {
        var serializer = new CsvConnectionsSerializer(new SaveFilter(), _credentialRepositoryList);
        var treeModel = new ConnectionTreeModelBuilder().Build();
        var csv = serializer.Serialize(treeModel);
        Assert.That(csv, Does.Not.Match($"{treeModel.RootNodes[0].ConstantID};.*;{TreeNodeType.Root}"));
    }

    [TestCase(Username)]
    [TestCase(Domain)]
    [TestCase("InheritColors")]
    public void CreatesCsv(string valueThatShouldExist)
    {
        var serializer = new CsvConnectionsSerializer(new SaveFilter(), _credentialRepositoryList);
        var connectionInfo = BuildConnectionInfo();
        var csv = serializer.Serialize(connectionInfo);
        Assert.That(csv, Does.Match(valueThatShouldExist));
    }

    [TestCase(Username)]
    [TestCase(Domain)]
    [TestCase("InheritColors")]
    public void SerializerRespectsSaveFilterSettings(string valueThatShouldntExist)
    {
        var saveFilter = new SaveFilter(true);
        var serializer = new CsvConnectionsSerializer(saveFilter, _credentialRepositoryList);
        var connectionInfo = BuildConnectionInfo();
        var csv = serializer.Serialize(connectionInfo);
        Assert.That(csv, Does.Not.Match(valueThatShouldntExist));
    }

    [Test]
    public void CanSerializeEmptyConnectionInfo()
    {
        var serializer = new CsvConnectionsSerializer(new SaveFilter(), _credentialRepositoryList);
        var connectionInfo = new ConnectionInfo();
        var csv = serializer.Serialize(connectionInfo);
        Assert.That(csv, Is.Not.Empty);
    }

    [Test]
    public void CantPassNullToConstructor()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CsvConnectionsSerializer(null, _credentialRepositoryList));
    }

    [Test]
    public void CantPassNullToSerializeConnectionInfo()
    {
        var serializer = new CsvConnectionsSerializer(new SaveFilter(), _credentialRepositoryList);
        Assert.Throws<ArgumentNullException>(() => serializer.Serialize((ConnectionInfo)null));
    }

    [Test]
    public void CantPassNullToSerializeConnectionTreeModel()
    {
        var serializer = new CsvConnectionsSerializer(new SaveFilter(), _credentialRepositoryList);
        Assert.Throws<ArgumentNullException>(() => serializer.Serialize((ConnectionTreeModel)null));
    }

    [Test]
    public void FoldersAreSerialized()
    {
        var serializer = new CsvConnectionsSerializer(new SaveFilter(), _credentialRepositoryList);
        var container = BuildContainer();
        var csv = serializer.Serialize(container);
        Assert.That(csv, Does.Match(container.Name));
        Assert.That(csv, Does.Match(container.Username));
        Assert.That(csv, Does.Match(container.Domain));
        Assert.That(csv, Does.Not.Contain(container.Password));
        Assert.That(csv, Does.Contain(TreeNodeType.Container.ToString()));
    }

    [Test]
    public void SerializationIncludesRawInheritedValuesIfObjectInheritsFromParentOutsideOfSerializationScope()
    {
        var serializer = new CsvConnectionsSerializer(new SaveFilter(), _credentialRepositoryList);
        var treeModel = new ConnectionTreeModelBuilder().Build();
        var serializationTarget = treeModel.GetRecursiveChildList().First(info => info.Name == "folder3");
        var csv = serializer.Serialize(serializationTarget);
        var lineWithFolder3 = csv.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
            .First(s => s.Contains(serializationTarget.Name));
        Assert.That(lineWithFolder3, Does.Contain(serializationTarget.Username));
        Assert.That(lineWithFolder3, Does.Contain(serializationTarget.Domain));
        Assert.That(lineWithFolder3, Does.Not.Contain(serializationTarget.Password));
    }

    [Test]
    public void CsvExportNeverContainsSecrets()
    {
        var serializer = new CsvConnectionsSerializer(new SaveFilter(), _credentialRepositoryList);
        var connectionInfo = BuildConnectionInfo();
        connectionInfo.VNCProxyPassword = "vnc-secret";
        connectionInfo.RDGatewayPassword = "gateway-secret";
        connectionInfo.RDGatewayAccessToken = "token-secret";

        var csv = serializer.Serialize(connectionInfo);

        Assert.Multiple(() =>
        {
            Assert.That(csv, Does.Not.Contain(Password));
            Assert.That(csv, Does.Not.Contain("vnc-secret"));
            Assert.That(csv, Does.Not.Contain("gateway-secret"));
            Assert.That(csv, Does.Not.Contain("token-secret"));
            Assert.That(csv, Does.Not.Contain("Password;"));
            Assert.That(csv, Does.Not.Contain("AccessToken;"));
        });
    }

    private ConnectionInfo BuildConnectionInfo()
    {
        return new ConnectionInfo
        {
            Name = ConnectionName,
            Username = Username,
            Domain = Domain,
            //Password = Password?.ConvertToSecureString(),
            Password = Password,
            Inheritance = { Colors = true }
        };
    }

    private ContainerInfo BuildContainer()
    {
        return new ContainerInfo
        {
            Name = "MyFolder",
            Username = "BlahBlah1",
            Domain = "aklkskkksh8",
            //Password = "qweraslkdjf87".ConvertToSecureString()
            Password = "qweraslkdjf87"
        };
    }
}
