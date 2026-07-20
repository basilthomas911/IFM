using System;
using System.Data;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public class ObjectDataStoredProcedureContextTests
{
    [Fact]
    public void CreateObjectDataStoredProcedureContextOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();

        // Act
        var ctx = new ObjectDataStoredProcedureContext(mockRepo, mockLogger, "spTestProc");

        // Assert
        ctx.Should().NotBeNull();
    }

    [Fact]
    public void CreateObjectDataStoredProcedureContextWithNullRepo()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger<DbProvider>>();

        // Act
        var act = () => new ObjectDataStoredProcedureContext(null, mockLogger, "spTestProc");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetCommandOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var ctx = new ObjectDataStoredProcedureContext(mockRepo, mockLogger, "spGetData");
        var mockDbCommand = Substitute.For<IDbCommand>();

        // Act
        ctx.SetCommand(mockDbCommand);

        // Assert
        mockDbCommand.Received().CommandType = CommandType.StoredProcedure;
        mockDbCommand.Received().CommandText = "spGetData";
    }

    [Fact]
    public void GetCommandTextOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var ctx = new ObjectDataStoredProcedureContext(mockRepo, mockLogger, "spGetData");

        // Act
        var result = ctx.GetCommandText();

        // Assert
        result.Should().Be("spGetData");
    }

    [Fact]
    public void GetCommandTypeOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var ctx = new ObjectDataStoredProcedureContext(mockRepo, mockLogger, "spGetData");

        // Act
        var result = ctx.GetCommandType();

        // Assert
        result.Should().Be(CommandType.StoredProcedure);
    }

    [Fact]
    public void CommandTextPropertyOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var ctx = new ObjectDataStoredProcedureContext(mockRepo, mockLogger, "spGetData");

        // Act & Assert
        ctx.CommandText.Should().Be("spGetData");
    }

    [Fact]
    public void GetParameterNameForSqlServer()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("System.Data.SqlServer");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var ctx = new ObjectDataStoredProcedureContext(mockRepo, mockLogger, "spGetData");

        // Act
        var result = ctx.GetParameterName("parameterName");

        // Assert
        result.Should().Be("@parameterName");
    }

    [Fact]
    public void GetParameterNameForPostgres()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("System.Data.Postgres");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var ctx = new ObjectDataStoredProcedureContext(mockRepo, mockLogger, "spGetData");

        // Act
        var result = ctx.GetParameterName("parameterName");

        // Assert
        result.Should().Be("_parameterName");
    }

    [Fact]
    public void GetParameterNameForCassandra()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("System.Data.Cassandra");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var ctx = new ObjectDataStoredProcedureContext(mockRepo, mockLogger, "spGetData");

        // Act
        var result = ctx.GetParameterName("parameterName");

        // Assert
        result.Should().Be("parameterName");
    }

    [Fact]
    public void GetParameterNameForScylla()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("System.Data.Scylla");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var ctx = new ObjectDataStoredProcedureContext(mockRepo, mockLogger, "spGetData");

        // Act
        var result = ctx.GetParameterName("parameterName");

        // Assert
        result.Should().Be("parameterName");
    }

    [Fact]
    public void GetParameterNameWithUnknownProviderThrows()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("Unknown.Provider");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var ctx = new ObjectDataStoredProcedureContext(mockRepo, mockLogger, "spGetData");

        // Act
        var act = () => ctx.GetParameterName("parameterName");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetParameterNameWithEmptyProviderThrows()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var ctx = new ObjectDataStoredProcedureContext(mockRepo, mockLogger, "spGetData");

        // Act
        var act = () => ctx.GetParameterName("parameterName");

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
