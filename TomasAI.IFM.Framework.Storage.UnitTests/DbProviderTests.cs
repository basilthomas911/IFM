using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Data;
using Microsoft.Data.SqlClient;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public class DbProviderTests
{
    [Fact]
    public void CreateDbProviderOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();

        // Act
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);

        // Assert
        dbProvider.Should().NotBeNull();
    }

    [Fact]
    public void CreateDbProviderWithNullRepo()
    {
        // Arrange & Act
        var act = () => new ObjectDataDbProvider(null, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateConnectionOk()
    {
        // Arrange
        var connString = "Data Source=DEV-SERVER;Initial Catalog=logdb;Integrated Security=True;MultipleActiveResultSets=True";
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ConnectionString.Returns(connString);
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);

        // Act
        var dbConn = dbProvider.CreateConnection().As<SqlConnection>(connString);

        // Assert
        dbConn.Should().NotBeNull();
        dbConn.ConnectionString.Should().Be(connString);
    }

    [Fact]
    public void CreateParameterOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);

        // Act
        var dbParam = dbProvider.CreateParameter();

        // Assert
        dbParam.Should().NotBeNull();
    }

    [Fact]
    public void CreateStoredProcedureContextOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);

        // Act
        var storedProcCtx = dbProvider.CreateStoredProcedureContext("spTestProc");

        // Assert
        storedProcCtx.Should().NotBeNull();
    }

    [Fact]
    public void CreateCommandTextContextOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);

        // Act
        var cmdTextCtx = dbProvider.CreateCommandTextContext("cmdText");

        // Assert
        cmdTextCtx.Should().NotBeNull();
    }

    [Fact]
    public void CreateBulkCopyContextOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);
        var dataTable = new DataTable("TestTable");

        // Act
        var bulkCopyCtx = dbProvider.CreateBulkCopyContext(dataTable);

        // Assert
        bulkCopyCtx.Should().NotBeNull();
    }

    [Fact]
    public void CreateQueuedCommandsContextWithAllTextOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);
        List<object> queuedCommands =
        [
            new ObjectDataQueuedCommand(CommandType.Text, "SELECT 1", null),
            new ObjectDataQueuedCommand(CommandType.Text, "SELECT 2", null)
        ];

        // Act
        var queuedCtx = dbProvider.CreateQueuedCommandsContext(queuedCommands);

        // Assert
        queuedCtx.Should().NotBeNull();
    }

    [Fact]
    public void CreateQueuedCommandsContextWithAllStoredProcsOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);
        List<object> queuedCommands =
        [
            new ObjectDataQueuedCommand(CommandType.StoredProcedure, "spProc1", null),
            new ObjectDataQueuedCommand(CommandType.StoredProcedure, "spProc2", null)
        ];

        // Act
        var queuedCtx = dbProvider.CreateQueuedCommandsContext(queuedCommands);

        // Assert
        queuedCtx.Should().NotBeNull();
    }

    [Fact]
    public void CreateQueuedCommandsContextLegacyOverloadCreatesNeutralTextContext()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);

        // Act
#pragma warning disable CS0618 // Deliberately exercise the legacy compatibility overload.
        var queuedCtx = dbProvider.CreateQueuedCommandsContext();
#pragma warning restore CS0618

        // Assert
        queuedCtx.Should().BeOfType<ObjectDataCommandTextContext>();
        ((ObjectDataRepositoryContext)queuedCtx).GetCommandType().Should().Be(CommandType.Text);
    }

    [Fact]
    public void CreateQueuedCommandsContextWithMixedTypesThrows()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);
        List<object> queuedCommands =
        [
            new ObjectDataQueuedCommand(CommandType.Text, "SELECT 1", null),
            new ObjectDataQueuedCommand(CommandType.StoredProcedure, "spProc1", null)
        ];

        // Act
        var act = () => dbProvider.CreateQueuedCommandsContext(queuedCommands);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateQueuedCommandsContextWithDifferentProviderThrows()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("System.Data.Postgres");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);
        List<object> queuedCommands =
        [
            new ObjectDataQueuedCommand(
                CommandType.Text,
                "SELECT 1",
                null,
                "System.Data.SqlServer",
                null)
        ];

        // Act
        var act = () => dbProvider.CreateQueuedCommandsContext(queuedCommands);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*repository provider*");
    }

    [Fact]
    public void CreateQueuedCommandsContextWithDifferentScyllaKeyspaceThrows()
    {
        var targetRepo = Substitute.For<IObjectRepository>();
        targetRepo.ProviderName.Returns("System.Data.ScyllaDb");
        targetRepo.ConnectionString.Returns(
            "Contact Points=localhost;Default Keyspace=market_data;User Id=test-user;Password=test-password");
        var sourceRepo = Substitute.For<IObjectRepository>();
        sourceRepo.ProviderName.Returns("System.Data.ScyllaDb");
        sourceRepo.ConnectionString.Returns(
            "Contact Points=localhost;Default Keyspace=fund;User Id=test-user;Password=test-password");
        var dbProvider = new ObjectDataDbProvider(
            targetRepo,
            Substitute.For<ILogger<DbProvider>>());
        List<object> queuedCommands =
        [
            new ObjectDataQueuedCommand(
                CommandType.Text,
                "SELECT 1",
                null,
                sourceRepo.ProviderName,
                RepositoryConnectionIdentity.Get(sourceRepo))
        ];

        var act = () => dbProvider.CreateQueuedCommandsContext(queuedCommands);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*repository connection*");
    }

    [Fact]
    public void CreateQueuedCommandsContextWithSameConnectionFromDifferentRepositorySucceeds()
    {
        const string connection =
            "Host=localhost;Database=events";
        var targetRepo = Substitute.For<IObjectRepository>();
        targetRepo.ProviderName.Returns("System.Data.Postgres");
        targetRepo.ConnectionString.Returns(connection);
        var sourceRepo = Substitute.For<IObjectRepository>();
        sourceRepo.ProviderName.Returns("System.Data.Postgres");
        sourceRepo.ConnectionString.Returns(connection);
        var dbProvider = new ObjectDataDbProvider(
            targetRepo,
            Substitute.For<ILogger<DbProvider>>());
        List<object> queuedCommands =
        [
            new ObjectDataQueuedCommand(
                CommandType.Text,
                "SELECT 1",
                null,
                sourceRepo.ProviderName,
                RepositoryConnectionIdentity.Get(sourceRepo))
        ];

        var queuedCtx = dbProvider.CreateQueuedCommandsContext(queuedCommands);

        queuedCtx.Should().BeOfType<ObjectDataCommandTextContext>();
    }

    [Fact]
    public void RepositoryConnectionIdentityCachesPerRepositoryAndComparesEquivalentRepositoriesByValue()
    {
        const string connection =
            "Contact Points=localhost;Default Keyspace=fund";
        var firstRepo = Substitute.For<IObjectRepository>();
        firstRepo.ProviderName.Returns("System.Data.ScyllaDb");
        firstRepo.ConnectionString.Returns(connection);
        var secondRepo = Substitute.For<IObjectRepository>();
        secondRepo.ProviderName.Returns("System.Data.ScyllaDb");
        secondRepo.ConnectionString.Returns(connection);

        var firstIdentity = RepositoryConnectionIdentity.Get(firstRepo);
        var cachedFirstIdentity = RepositoryConnectionIdentity.Get(firstRepo);
        var equivalentIdentity = RepositoryConnectionIdentity.Get(secondRepo);

        cachedFirstIdentity.Should().BeSameAs(firstIdentity);
        equivalentIdentity.Should().NotBeSameAs(firstIdentity);
        equivalentIdentity.Should().Be(firstIdentity);
    }

    [Fact]
    public void CreateDataReaderContextOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);
        var connectionString = @"Data Source = https://example.com/data.csv";
        var dataReaderOptions = new DataReaderOptions(connectionString);

        // Act
        var readerCtx = dbProvider.CreateDataReaderContext(dataReaderOptions);

        // Assert
        readerCtx.Should().NotBeNull();
    }

    [Fact]
    public void CreateFileUriContextOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            var uri = new System.Uri(tempFile);
            var connectionString = @"Data Source = https://example.com/data.csv";
            var dataReaderOptions = new DataReaderOptions(connectionString);

            // Act
            var fileCtx = dbProvider.CreateFileUriContext(uri, dataReaderOptions);

            // Assert
            fileCtx.Should().NotBeNull();
        }
        finally
        {
            System.IO.File.Delete(tempFile);
        }
    }

    [Fact]
    public void CreateQueuedCommandsContextIgnoresUnrelatedContextCreation()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);
        // These contexts can be created concurrently by unrelated callers and must
        // not become hidden input to execution of this explicit queue.
        dbProvider.CreateCommandTextContext("SELECT 1");
        dbProvider.CreateStoredProcedureContext("spProc1");
        List<object> queuedCommands =
        [
            new ObjectDataQueuedCommand(CommandType.Text, "SELECT 2", null)
        ];

        // Act
        var queuedCtx = dbProvider.CreateQueuedCommandsContext(queuedCommands);

        // Assert
        queuedCtx.Should().BeOfType<ObjectDataCommandTextContext>();
    }

    [Fact]
    public async Task CreateQueuedCommandsContextConcurrentQueuesAreIndependent()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workers = Enumerable.Range(0, 32).Select(async index =>
        {
            var commandType = index % 2 == 0
                ? CommandType.Text
                : CommandType.StoredProcedure;
            List<object> queue =
            [
                new ObjectDataQueuedCommand(commandType, $"command-{index}", null)
            ];
            await start.Task;

            for (var iteration = 0; iteration < 100; iteration++)
            {
                var context = dbProvider.CreateQueuedCommandsContext(queue);
                context.Should().BeAssignableTo<ObjectDataRepositoryContext>();
                ((ObjectDataRepositoryContext)context).GetCommandType().Should().Be(commandType);
            }
        }).ToArray();

        // Act
        start.SetResult();
        await Task.WhenAll(workers);
    }
}
