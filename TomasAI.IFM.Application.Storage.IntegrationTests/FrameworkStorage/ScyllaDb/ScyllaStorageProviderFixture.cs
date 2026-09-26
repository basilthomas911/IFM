using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.FrameworkStorage.ScyllaDb;

/// <summary>Defines the shared ScyllaDB integration-test collection.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ScyllaStorageProviderCollection : ICollectionFixture<ScyllaStorageProviderFixture>
{
    /// <summary>Gets the shared collection name.</summary>
    public const string Name = "Framework.Storage ScyllaDB integration";
}

/// <summary>Provides a neutral ScyllaDB repository for integration tests.</summary>
public sealed class ScyllaStorageProviderFixture : IAsyncLifetime
{
    const string ConnectionVariable = "IFM_SCYLLA_TEST_CONNECTION";
    const string ProviderName = "System.Data.ScyllaDb";
    readonly ILogger<DbProvider> _logger = Substitute.For<ILogger<DbProvider>>();

    /// <summary>Gets the initialized test repository.</summary>
    public ScyllaTestRepository Repository { get; private set; } = null!;

    /// <summary>Gets the dedicated integration-test connection.</summary>
    public IDbConnectionSetting ConnectionSetting { get; private set; } = null!;

    /// <summary>Initializes the shared ScyllaDB repository.</summary>
    public Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"Set {ConnectionVariable} to a credential-free ScyllaDB connection string whose default keyspace is dedicated to integration tests.");
        var settings = new DbConnectionSettings().Add("ScyllaIntegrationDbConnection", connectionString, ProviderName);
        ConnectionSetting = settings["ScyllaIntegrationDbConnection"];
        Repository = new ScyllaTestRepository(ConnectionSetting, _logger);
        return Task.CompletedTask;
    }

    /// <summary>Releases fixture resources.</summary>
    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>Exposes object-data operations against the integration-test ScyllaDB keyspace.</summary>
public sealed class ScyllaTestRepository : ObjectDataRepository<ScyllaTestRepository>
{
    /// <summary>Initializes a ScyllaDB test repository.</summary>
    /// <param name="connectionSetting">The dedicated integration-test connection.</param>
    /// <param name="logger">The database provider logger.</param>
    public ScyllaTestRepository(IDbConnectionSetting connectionSetting, ILogger<DbProvider> logger)
        : base(connectionSetting, logger) { }

    /// <summary>Gets this repository through the common repository contract.</summary>
    public override IObjectRepository Database => this;
}
