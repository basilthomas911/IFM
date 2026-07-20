using System;
using System.Data;
using Xunit;
using FluentAssertions;
using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;

namespace TomasAI.IFM.Framework.Storage.UnitTests.Postgres;

public class PostgresConnectionTests
{
    // --- PostgresObjectDataRepositoryConnection ---

    [Fact]
    public void As_WithValidConnectionString_ReturnsNpgsqlConnection()
    {
        // Arrange
        var connection = new PostgresObjectDataRepositoryConnection();
        var connectionString = "Host=localhost;Database=testdb;Username=user;Password=pass";

        // Act
        var result = connection.As<NpgsqlConnection>(connectionString);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NpgsqlConnection>();
        result.ConnectionString.Should().Contain("localhost");
    }

    [Fact]
    public void As_ReturnsNewInstanceEachTime()
    {
        // Arrange
        var connection = new PostgresObjectDataRepositoryConnection();
        var connectionString = "Host=localhost;Database=testdb;Username=user;Password=pass";

        // Act
        var result1 = connection.As<NpgsqlConnection>(connectionString);
        var result2 = connection.As<NpgsqlConnection>(connectionString);

        // Assert
        result1.Should().NotBeSameAs(result2);
    }

    // --- PostgresObjectDataRepositoryParameter ---

    [Fact]
    public void Parameter_ReturnsNpgsqlParameter()
    {
        // Arrange
        var paramProvider = new PostgresObjectDataRepositoryParameter();

        // Act
        var result = paramProvider.Parameter;

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NpgsqlParameter>();
    }

    [Fact]
    public void Parameter_ReturnsNewInstanceEachAccess()
    {
        // Arrange
        var paramProvider = new PostgresObjectDataRepositoryParameter();

        // Act
        var result1 = paramProvider.Parameter;
        var result2 = paramProvider.Parameter;

        // Assert
        result1.Should().NotBeSameAs(result2);
    }
}
