using System.IO;
using LoipvRemote.App;
using LoipvRemote.Config.Putty;
using LoipvRemote.Connection;
using LoipvRemote.Container;
using LoipvRemote.Messages;
using LoipvRemote.Domain.Connections;
using LoipvRemote.Domain.Credentials;
using LoipvRemote.Infrastructure.Persistence.Xml;
using LoipvRemote.Infrastructure.Persistence;
using LoipvRemote.Config.Import;
using LoipvRemoteTests.Properties;
using LoipvRemoteTests.TestHelpers;
using NUnit.Framework;
using Microsoft.Extensions.DependencyInjection;

namespace LoipvRemoteTests.App;

public class ImportTests
{
    private readonly List<string> _temporaryFiles = [];

    [TearDown]
    public void TearDown()
    {
        foreach (string file in _temporaryFiles)
            if (File.Exists(file))
                File.Delete(file);
    }

    [Test]
    public async Task CanonicalXmlImporterReadsTheDomainConnectionStoreFormat()
    {
        string file = Path.Combine(Path.GetTempPath(), $"loipvremote-import-{Guid.NewGuid():N}.xml");
        _temporaryFiles.Add(file);
        ConnectionTreeDefinition definition = new(
            [],
            [new ConnectionDefinition(
                Guid.NewGuid(),
                "canonical-ssh",
                "host.example",
                22,
                ProtocolKind.Ssh2,
                CredentialReference.None)]);
        await new XmlConnectionDefinitionStore(file).SaveAsync(definition);

        ContainerInfo destination = new();
        await new LoipvRemoteXmlImporter(RuntimeHostFixture.Services.GetRequiredService<MessageCollector>())
            .ImportAsync(file, destination);

        Assert.That(((ContainerInfo)destination.Children.Single()).Children.Single().Name, Is.EqualTo("canonical-ssh"));
    }

    [Test]
    public async Task ErrorHandlerCalledWhenUnsupportedFileExtensionFound()
    {
        using (FileTestHelpers.DisposableTempFile(out var file, ".blah"))
        {
            var conService = RuntimeHostFixture.Services.GetRequiredService<IConnectionTreeWorkspace>();
            var container = new ContainerInfo();
            var exceptionOccurred = false;

            var importService = new ConnectionImportService(conService, RuntimeHostFixture.Services.GetRequiredService<MessageCollector>());
            await importService.HeadlessFileImportAsync(new[] { file }, container, s => exceptionOccurred = true);

            Assert.That(exceptionOccurred);
        }
    }

    [Test]
    public async Task AnErrorInOneFileDoNotPreventOtherFilesFromProcessing()
    {
        using (FileTestHelpers.DisposableTempFile(out var badFile, ".blah"))
        using (FileTestHelpers.DisposableTempFile(out var rdpFile, ".rdp"))
        {
            File.AppendAllText(rdpFile, Resources.test_remotedesktopconnection_rdp);
            var conService = RuntimeHostFixture.Services.GetRequiredService<IConnectionTreeWorkspace>();
            var container = new ContainerInfo();
            var exceptionCount = 0;

            var importService = new ConnectionImportService(conService, RuntimeHostFixture.Services.GetRequiredService<MessageCollector>());
            await importService.HeadlessFileImportAsync(new[] { badFile, rdpFile }, container, s => exceptionCount++);

            Assert.That(exceptionCount, Is.EqualTo(1));
            Assert.That(container.Children, Has.One.Items);
        }
    }
}
