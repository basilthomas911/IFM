using System;
using System.Runtime.CompilerServices;
using Xunit;
using FluentAssertions;
using NSubstitute;
using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Exceptions;

namespace TomasAI.IFM.Framework.Storage.UnitTests.Postgres;

public class PostgresTransactionTests
{
    // --- BeginTransaction ---

    [Fact]
    public void BeginTransaction_NullDb_ThrowsArgumentNullException()
    {
        // Arrange
        var transaction = new PostgresObjectDataRepositoryTransaction<IObjectRepository>();

        // Act
        Action act = () => transaction.BeginTransaction(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BeginTransaction_WhenTransactionAlreadyStarted_ThrowsStorageException()
    {
        // Arrange
        var transaction = new PostgresObjectDataRepositoryTransaction<IObjectRepository>();
        transaction.Transaction = (NpgsqlTransaction)RuntimeHelpers.GetUninitializedObject(typeof(NpgsqlTransaction));

        // Act
        Action act = () => transaction.BeginTransaction(null!);

        // Assert
        act.Should().Throw<StorageException>()
            .WithMessage("*transaction already started*");
    }

    // --- CreateCommand ---

    [Fact]
    public void CreateCommand_WhenNoTransaction_ThrowsStorageException()
    {
        // Arrange
        var transaction = new PostgresObjectDataRepositoryTransaction<IObjectRepository>();
        transaction.Transaction = null;

        // Act
        Action act = () => transaction.CreateCommand();

        // Assert
        act.Should().Throw<StorageException>()
            .WithMessage("*transaction has not been started*");
    }

    [Fact]
    public void CreateCommand_WhenNoConnection_ThrowsInvalidOperationException()
    {
        // Arrange
        var transaction = new PostgresObjectDataRepositoryTransaction<IObjectRepository>();
        transaction.Transaction = (NpgsqlTransaction)RuntimeHelpers.GetUninitializedObject(typeof(NpgsqlTransaction));
        transaction.Connection = null;

        // Act
        Action act = () => transaction.CreateCommand();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*connection has not been opened*");
    }

    // --- Commit ---

    [Fact]
    public void Commit_WhenNoTransaction_ThrowsInvalidOperationException()
    {
        // Arrange
        var transaction = new PostgresObjectDataRepositoryTransaction<IObjectRepository>();
        transaction.Transaction = null;

        // Act
        Action act = () => transaction.Commit();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*transaction has not been started*");
    }

    // --- Rollback ---

    [Fact]
    public void Rollback_WhenNoTransaction_ThrowsInvalidOperationException()
    {
        // Arrange
        var transaction = new PostgresObjectDataRepositoryTransaction<IObjectRepository>();
        transaction.Transaction = null;

        // Act
        Action act = () => transaction.Rollback();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*transaction has not been started*");
    }

    // --- Initial state ---

    [Fact]
    public void InitialState_RepositoryIsNull()
    {
        // Arrange & Act
        var transaction = new PostgresObjectDataRepositoryTransaction<IObjectRepository>();

        // Assert
        transaction.Repository.Should().BeNull();
    }

    [Fact]
    public void InitialState_ConnectionIsNull()
    {
        // Arrange & Act
        var transaction = new PostgresObjectDataRepositoryTransaction<IObjectRepository>();

        // Assert
        transaction.Connection.Should().BeNull();
    }

    [Fact]
    public void InitialState_TransactionIsNull()
    {
        // Arrange & Act
        var transaction = new PostgresObjectDataRepositoryTransaction<IObjectRepository>();

        // Assert
        transaction.Transaction.Should().BeNull();
    }
}
