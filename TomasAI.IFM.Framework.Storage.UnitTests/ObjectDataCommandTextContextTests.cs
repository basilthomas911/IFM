using System;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Data;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public class ObjectDataCommandTextContextTests
{
    [Fact]
    public void CreateObjectDataCommandTextContextOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();

        // Act
        var ctx = new ObjectDataCommandTextContext(mockRepo, mockLogger, "cmdText");

        // Assert
        ctx.Should().NotBeNull();
    }

    [Fact]
    public void CreateObjectDataCommandTextContextWithNullRepo()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger<DbProvider>>();

        // Act
        var act = () => new ObjectDataCommandTextContext(null, mockLogger, "cmdText");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetCommandOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var odCommandTextCtx = new ObjectDataCommandTextContext(mockRepo, mockLogger, "Rain In Spain");
        var mockDbCommand = Substitute.For<IDbCommand>();

        // Act
        odCommandTextCtx.SetCommand(mockDbCommand);

        // Assert
        mockDbCommand.Received().CommandType = CommandType.Text;
        mockDbCommand.Received().CommandText = "Rain In Spain";
    }

    [Fact]
    public void GetCommandTextOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var odCommandTextCtx = new ObjectDataCommandTextContext(mockRepo, mockLogger, "Rain In Spain");

        // Act
        var result = odCommandTextCtx.GetCommandText();

        // Assert
        result.Should().Be("Rain In Spain");
    }

    [Fact]
    public void GetParameterNameOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("System.Data.SqlServer");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var odCommandTextCtx = new ObjectDataCommandTextContext(mockRepo, mockLogger, "Rain In Spain");

        // Act
        var result = odCommandTextCtx.GetParameterName("parameterName");

        // Assert
        result.Should().Be("@parameterName");
    }

    [Fact]
    public void GetParameterNameWithEmptyProviderName()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var odCommandTextCtx = new ObjectDataCommandTextContext(mockRepo, mockLogger, "Rain In Spain");

        // Act
        var act = () => odCommandTextCtx.GetParameterName("parameterName");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetCommandTypeOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var odCommandTextCtx = new ObjectDataCommandTextContext(mockRepo, mockLogger, "SELECT 1");

        // Act
        var result = odCommandTextCtx.GetCommandType();

        // Assert
        result.Should().Be(CommandType.Text);
    }

    [Fact]
    public void CommandTextPropertyOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var odCommandTextCtx = new ObjectDataCommandTextContext(mockRepo, mockLogger, "Rain In Spain");

        // Act & Assert
        odCommandTextCtx.CommandText.Should().Be("Rain In Spain");
    }

    [Fact]
    public void GetParameterNameForSqlServer()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("System.Data.SqlServer");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var odCommandTextCtx = new ObjectDataCommandTextContext(mockRepo, mockLogger, "SELECT 1");

        // Act
        var result = odCommandTextCtx.GetParameterName("id");

        // Assert
        result.Should().Be("@id");
    }

    [Fact]
    public void GetParameterNameForPostgres()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("System.Data.Postgres");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var odCommandTextCtx = new ObjectDataCommandTextContext(mockRepo, mockLogger, "SELECT 1");

        // Act
        var result = odCommandTextCtx.GetParameterName("id");

        // Assert
        result.Should().Be("_id");
    }

    [Fact]
    public void GetParameterNameForCassandra()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("System.Data.Cassandra");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var odCommandTextCtx = new ObjectDataCommandTextContext(mockRepo, mockLogger, "SELECT 1");

        // Act
        var result = odCommandTextCtx.GetParameterName("id");

        // Assert
        result.Should().Be("id");
    }

    [Fact]
    public void GetParameterNameForScylla()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("System.Data.Scylla");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var odCommandTextCtx = new ObjectDataCommandTextContext(mockRepo, mockLogger, "SELECT 1");

        // Act
        var result = odCommandTextCtx.GetParameterName("id");

        // Assert
        result.Should().Be("id");
    }
}
