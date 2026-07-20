using System;
using Xunit;
using FluentAssertions;
using TomasAI.IFM.Framework.Storage.SqlServer;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Framework.Storage.ScyllaDb;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public class ObjectDataRepositoryConnectionTests
{
    [Fact]
    public void CreateConnectionForSqlServer()
    {
        // Arrange & Act
        var connection = ObjectDataRepositoryConnection.Create(null);

        // Assert
        connection.Should().NotBeNull();
        connection.Should().BeOfType<SqlServerObjectDataRepositoryConnection>();
    }

    [Fact]
    public void CreateConnectionForSqlServerWithDefaultProvider()
    {
        // Arrange & Act
        var connection = ObjectDataRepositoryConnection.Create("SomeUnknownProvider");

        // Assert
        connection.Should().NotBeNull();
        connection.Should().BeOfType<SqlServerObjectDataRepositoryConnection>();
    }

    [Fact]
    public void CreateConnectionForPostgres()
    {
        // Arrange & Act
        var connection = ObjectDataRepositoryConnection.Create("System.Data.Postgres");

        // Assert
        connection.Should().NotBeNull();
        connection.Should().BeOfType<PostgresObjectDataRepositoryConnection>();
    }

    [Fact]
    public void CreateConnectionForScyllaDb()
    {
        // Arrange & Act
        var connection = ObjectDataRepositoryConnection.Create("System.Data.ScyllaDb");

        // Assert
        connection.Should().NotBeNull();
        connection.Should().BeOfType<ScyllaDbObjectDataRepositoryConnection>();
    }

    [Fact]
    public void CreateConnectionForEmptyProvider()
    {
        // Arrange & Act
        var connection = ObjectDataRepositoryConnection.Create("");

        // Assert
        connection.Should().NotBeNull();
        connection.Should().BeOfType<SqlServerObjectDataRepositoryConnection>();
    }
}
