using System;
using System.Data;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.ScyllaDb;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Framework.Storage.UnitTests.ScyllaDb;

public class ScyllaDbQueuedCommandTests
{
    // --- Constructor happy path ---

    [Fact]
    public void Constructor_ValidParams_SetsProperties()
    {
        // Arrange
        var commandType = CommandType.Text;
        var commandText = "SELECT * FROM table";
        var bindValues = new List<object> { "value1", 42 };

        // Act
        var command = new ScyllaDbObjectDataQueuedCommand(commandType, commandText, bindValues);

        // Assert
        command.CommandType.Should().Be(CommandType.Text);
        command.CommandText.Should().Be("SELECT * FROM table");
        command.BindValues.Should().BeSameAs(bindValues);
    }

    [Fact]
    public void Constructor_StoredProcedureType_SetsCorrectly()
    {
        // Arrange & Act
        var command = new ScyllaDbObjectDataQueuedCommand(
            CommandType.StoredProcedure,
            "sp_my_procedure",
            null);

        // Assert
        command.CommandType.Should().Be(CommandType.StoredProcedure);
        command.CommandText.Should().Be("sp_my_procedure");
    }

    [Fact]
    public void Constructor_NullBindValues_SetsBindValuesToNull()
    {
        // Arrange & Act
        var command = new ScyllaDbObjectDataQueuedCommand(CommandType.Text, "SELECT 1", null);

        // Assert
        command.BindValues.Should().BeNull();
    }

    [Fact]
    public void Constructor_EmptyBindValues_SetsEmptyList()
    {
        // Arrange
        var bindValues = new List<object>();

        // Act
        var command = new ScyllaDbObjectDataQueuedCommand(CommandType.Text, "SELECT 1", bindValues);

        // Assert
        command.BindValues.Should().NotBeNull();
        command.BindValues.Should().BeEmpty();
    }

    // --- Constructor edge cases / validation ---

    [Fact]
    public void Constructor_NullCommandText_ThrowsStorageException()
    {
        // Arrange & Act
        Action act = () => new ScyllaDbObjectDataQueuedCommand(CommandType.Text, null!, null);

        // Assert
        act.Should().Throw<StorageException>()
            .WithMessage("*commandText parameter is empty*");
    }

    [Fact]
    public void Constructor_EmptyCommandText_ThrowsStorageException()
    {
        // Arrange & Act
        Action act = () => new ScyllaDbObjectDataQueuedCommand(CommandType.Text, string.Empty, null);

        // Assert
        act.Should().Throw<StorageException>()
            .WithMessage("*commandText parameter is empty*");
    }

    [Fact]
    public void Constructor_WhitespaceCommandText_ThrowsStorageException()
    {
        // Arrange & Act
        Action act = () => new ScyllaDbObjectDataQueuedCommand(CommandType.Text, "   ", null);

        // Assert
        act.Should().Throw<StorageException>()
            .WithMessage("*commandText parameter is empty*");
    }

    // --- Multiple bind values ---

    [Fact]
    public void Constructor_MultipleBindValues_PreservesAll()
    {
        // Arrange
        var bindValues = new List<object> { "text", 42, 3.14, true, DateTime.Now };

        // Act
        var command = new ScyllaDbObjectDataQueuedCommand(CommandType.Text, "INSERT INTO t VALUES (?, ?, ?, ?, ?)", bindValues);

        // Assert
        command.BindValues.Should().HaveCount(5);
    }
}
