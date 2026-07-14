using System;
using System.Collections.Generic;
using LoipvRemote.Infrastructure.Persistence.MySql;
using LoipvRemote.Infrastructure.Persistence.Odbc;
using LoipvRemote.Infrastructure.Persistence.SqlServer;
using NUnit.Framework;

namespace LoipvRemoteTests.Infrastructure.Persistence;

public class RelationalConnectionDefinitionStoreTests
{
    [TestCaseSource(nameof(CreateStores))]
    public void RejectsBlankConnectionString(Func<object> createStore)
    {
        Assert.That(createStore, Throws.InstanceOf<ArgumentException>());
    }

    private static IEnumerable<Func<object>> CreateStores()
    {
        yield return () => new SqlServerConnectionDefinitionStore(string.Empty);
        yield return () => new MySqlConnectionDefinitionStore(string.Empty);
        yield return () => new OdbcConnectionDefinitionStore(string.Empty);
    }
}
