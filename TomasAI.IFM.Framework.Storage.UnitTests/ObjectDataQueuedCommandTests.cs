using System;
using System.Data;
using System.Data.Common;
using Xunit;
using FluentAssertions;
using Microsoft.Data.SqlClient;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public class ObjectDataQueuedCommandTests
{
    [Fact]
    public void CreateObjectDataQueuedCommandOk()
    {
        // Arrange
        var parameters = new DbParameter[] { new SqlParameter("@id", 1) };

        // Act
        var cmd = new ObjectDataQueuedCommand($"{nameof(ObjectDataQueuedCommandTests)}.Command", CommandType.Text, "SELECT * FROM Test", parameters);

        // Assert
        cmd.Should().NotBeNull();
        cmd.CommandType.Should().Be(CommandType.Text);
        cmd.CommandName.Should().Be($"{nameof(ObjectDataQueuedCommandTests)}.Command");
        cmd.CommandText.Should().Be("SELECT * FROM Test");
        cmd.CommandLogText.Should().Be(
            $"command name: {nameof(ObjectDataQueuedCommandTests)}.Command{Environment.NewLine}SELECT * FROM Test");
        cmd.Parameters.Should().NotBeNull();
        cmd.Parameters.Should().HaveCount(1);
    }

    [Fact]
    public void CreateObjectDataQueuedCommandWithStoredProcedureType()
    {
        // Arrange & Act
        var cmd = new ObjectDataQueuedCommand($"{nameof(ObjectDataQueuedCommandTests)}.Command", CommandType.StoredProcedure, "spGetData", null);

        // Assert
        cmd.CommandType.Should().Be(CommandType.StoredProcedure);
        cmd.CommandText.Should().Be("spGetData");
        cmd.Parameters.Should().BeNull();
    }

    [Fact]
    public void CreateObjectDataQueuedCommandWithNullParameters()
    {
        // Arrange & Act
        var cmd = new ObjectDataQueuedCommand($"{nameof(ObjectDataQueuedCommandTests)}.Command", CommandType.Text, "SELECT 1", null);

        // Assert
        cmd.Should().NotBeNull();
        cmd.Parameters.Should().BeNull();
    }

    [Fact]
    public void CreateObjectDataQueuedCommandWithEmptyParameters()
    {
        // Arrange & Act
        var cmd = new ObjectDataQueuedCommand($"{nameof(ObjectDataQueuedCommandTests)}.Command", CommandType.Text, "SELECT 1", Array.Empty<DbParameter>());

        // Assert
        cmd.Should().NotBeNull();
        cmd.Parameters.Should().BeEmpty();
    }

    [Fact]
    public void CreateObjectDataQueuedCommandWithEmptyCommandText()
    {
        // Arrange & Act
        var act = () => new ObjectDataQueuedCommand($"{nameof(ObjectDataQueuedCommandTests)}.Command", CommandType.Text, "", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateObjectDataQueuedCommandWithNullCommandText()
    {
        // Arrange & Act
        var act = () => new ObjectDataQueuedCommand($"{nameof(ObjectDataQueuedCommandTests)}.Command", CommandType.Text, null, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateObjectDataQueuedCommandWithWhitespaceCommandText()
    {
        // Arrange & Act
        var act = () => new ObjectDataQueuedCommand($"{nameof(ObjectDataQueuedCommandTests)}.Command", CommandType.Text, "   ", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateObjectDataQueuedCommandWithInvalidCommandName(string? commandName)
    {
        var act = () => new ObjectDataQueuedCommand(commandName!, CommandType.Text, "SELECT 1", null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CreateObjectDataQueuedCommandWithMultipleParameters()
    {
        // Arrange
        var parameters = new DbParameter[]
        {
            new SqlParameter("@id", 1),
            new SqlParameter("@name", "test"),
            new SqlParameter("@active", true)
        };

        // Act
        var cmd = new ObjectDataQueuedCommand($"{nameof(ObjectDataQueuedCommandTests)}.Command", CommandType.Text, "INSERT INTO Test VALUES (@id, @name, @active)", parameters);

        // Assert
        cmd.Parameters.Should().HaveCount(3);
    }
}
