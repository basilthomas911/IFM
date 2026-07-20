using System;
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
        dbProvider.CreateCommandTextContext("SELECT 1");
        dbProvider.CreateCommandTextContext("SELECT 2");

        // Act
        var queuedCtx = dbProvider.CreateQueuedCommandsContext();

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
        dbProvider.CreateStoredProcedureContext("spProc1");
        dbProvider.CreateStoredProcedureContext("spProc2");

        // Act
        var queuedCtx = dbProvider.CreateQueuedCommandsContext();

        // Assert
        queuedCtx.Should().NotBeNull();
    }

    [Fact]
    public void CreateQueuedCommandsContextWithNoQueuedCommandsThrows()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);

        // Act
        var act = () => dbProvider.CreateQueuedCommandsContext();

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateQueuedCommandsContextWithMixedTypesThrows()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);
        dbProvider.CreateCommandTextContext("SELECT 1");
        dbProvider.CreateStoredProcedureContext("spProc1");

        // Act
        var act = () => dbProvider.CreateQueuedCommandsContext();

        // Assert
        act.Should().Throw<ArgumentException>();
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
    public void CreateQueuedCommandsContextClearsQueueAfterCreate()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var dbProvider = new ObjectDataDbProvider(mockRepo, mockLogger);
        dbProvider.CreateCommandTextContext("SELECT 1");
        var queuedCtx = dbProvider.CreateQueuedCommandsContext();
        queuedCtx.Should().NotBeNull();

        // Act
        var act = () => dbProvider.CreateQueuedCommandsContext();

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
