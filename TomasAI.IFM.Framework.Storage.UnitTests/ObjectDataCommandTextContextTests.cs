using System;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Data;
using System.Linq;

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
        var ctx = new ObjectDataCommandTextContext(
            mockRepo,
            mockLogger,
            $"{nameof(ObjectDataCommandTextContextTests)}.{nameof(CreateObjectDataCommandTextContextOk)}",
            "cmdText");

        // Assert
        ctx.Should().NotBeNull();
    }

    [Fact]
    public void CreateObjectDataCommandTextContextWithNullRepo()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger<DbProvider>>();

        // Act
        var act = () => new ObjectDataCommandTextContext(
            null,
            mockLogger,
            $"{nameof(ObjectDataCommandTextContextTests)}.{nameof(CreateObjectDataCommandTextContextWithNullRepo)}",
            "cmdText");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SetCommandOk()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var odCommandTextCtx = new ObjectDataCommandTextContext(
            mockRepo, mockLogger,
            $"{nameof(ObjectDataCommandTextContextTests)}.{nameof(SetCommandOk)}",
            "Rain In Spain");
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
        var odCommandTextCtx = new ObjectDataCommandTextContext(
            mockRepo, mockLogger,
            $"{nameof(ObjectDataCommandTextContextTests)}.{nameof(GetCommandTextOk)}",
            "Rain In Spain");

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
        var odCommandTextCtx = new ObjectDataCommandTextContext(
            mockRepo, mockLogger,
            $"{nameof(ObjectDataCommandTextContextTests)}.{nameof(GetParameterNameOk)}",
            "Rain In Spain");

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
        var odCommandTextCtx = new ObjectDataCommandTextContext(
            mockRepo, mockLogger,
            $"{nameof(ObjectDataCommandTextContextTests)}.{nameof(GetParameterNameWithEmptyProviderName)}",
            "Rain In Spain");

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
        var odCommandTextCtx = new ObjectDataCommandTextContext(
            mockRepo, mockLogger,
            $"{nameof(ObjectDataCommandTextContextTests)}.{nameof(GetCommandTypeOk)}",
            "SELECT 1");

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
        var odCommandTextCtx = new ObjectDataCommandTextContext(
            mockRepo, mockLogger,
            $"{nameof(ObjectDataCommandTextContextTests)}.{nameof(CommandTextPropertyOk)}",
            "Rain In Spain");

        // Act & Assert
        odCommandTextCtx.CommandText.Should().Be("Rain In Spain");
    }

    [Fact]
    public void CommandMetadataPropertiesOk()
    {
        var mockRepo = Substitute.For<IObjectRepository>();
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var commandName = $"{nameof(ObjectDataCommandTextContextTests)}.{nameof(CommandMetadataPropertiesOk)}";
        var context = new ObjectDataCommandTextContext(
            mockRepo,
            mockLogger,
            commandName,
            "SELECT 1");

        context.CommandName.Should().Be(commandName);
        context.CommandText.Should().Be("SELECT 1");
        context.CommandLogText.Should().Be($"command name: {commandName}{Environment.NewLine}SELECT 1");
    }

    [Fact]
    public void SetParametersEnumerableInvokesBindValueForEveryItem()
    {
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("System.Data.SqlServer");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var context = new ObjectDataCommandTextContext(
            mockRepo, mockLogger,
            $"{nameof(ObjectDataCommandTextContextTests)}.{nameof(SetParametersEnumerableInvokesBindValueForEveryItem)}",
            "SELECT 1");

        context.SetParameters(new[] { new PositionalBindValue(11), new PositionalBindValue(12) });

        context.ParameterValues.Should().HaveCount(2);
        context.ParameterValues[0].Should().BeEquivalentTo(new object?[] { 11 });
        context.ParameterValues[1].Should().BeEquivalentTo(new object?[] { 12 });
    }

    [Fact]
    public void SetParametersEnumerablePreservesNonBindValues()
    {
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("System.Data.SqlServer");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var context = new ObjectDataCommandTextContext(
            mockRepo, mockLogger,
            $"{nameof(ObjectDataCommandTextContextTests)}.{nameof(SetParametersEnumerablePreservesNonBindValues)}",
            "SELECT 1");
        var values = new[] { new PlainParameter(11), new PlainParameter(12) };

        context.SetParameters(values);

        context.ParameterValues.Should().Equal(values.Cast<object>());
    }

    [Fact]
    public void GetParameterNameForSqlServer()
    {
        // Arrange
        var mockRepo = Substitute.For<IObjectRepository>();
        mockRepo.ProviderName.Returns("System.Data.SqlServer");
        var mockLogger = Substitute.For<ILogger<DbProvider>>();
        var odCommandTextCtx = new ObjectDataCommandTextContext(
            mockRepo, mockLogger,
            $"{nameof(ObjectDataCommandTextContextTests)}.{nameof(GetParameterNameForSqlServer)}",
            "SELECT 1");

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
        var odCommandTextCtx = new ObjectDataCommandTextContext(
            mockRepo, mockLogger,
            $"{nameof(ObjectDataCommandTextContextTests)}.{nameof(GetParameterNameForPostgres)}",
            "SELECT 1");

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
        var odCommandTextCtx = new ObjectDataCommandTextContext(
            mockRepo, mockLogger,
            $"{nameof(ObjectDataCommandTextContextTests)}.{nameof(GetParameterNameForCassandra)}",
            "SELECT 1");

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
        var odCommandTextCtx = new ObjectDataCommandTextContext(
            mockRepo, mockLogger,
            $"{nameof(ObjectDataCommandTextContextTests)}.{nameof(GetParameterNameForScylla)}",
            "SELECT 1");

        // Act
        var result = odCommandTextCtx.GetParameterName("id");

        // Assert
        result.Should().Be("id");
    }

    readonly record struct PositionalBindValue(int Value) : IBindValue
    {
        public object Bind() => new object?[] { Value };
    }

    sealed record PlainParameter(int Value);
}
