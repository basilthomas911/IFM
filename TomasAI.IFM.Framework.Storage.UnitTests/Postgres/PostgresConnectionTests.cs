using System;
using System.Data;
using Xunit;
using FluentAssertions;
using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;

namespace TomasAI.IFM.Framework.Storage.UnitTests.Postgres;

[Collection(PostgresCredentialEnvironmentCollection.Name)]
public class PostgresConnectionTests : IDisposable
{
    readonly string? _previousDotNetEnvironment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    readonly string? _previousAspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
    readonly string? _previousPostgresTestKey = Environment.GetEnvironmentVariable("POSTGRES_TEST_KEY");

    public PostgresConnectionTests()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Test");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable(
            "POSTGRES_TEST_KEY",
            "{\"userid\":\"unit-test-user\",\"password\":\"unit-test-password\"}");
    }

    // --- PostgresObjectDataRepositoryConnection ---

    [Fact]
    public void As_WithValidConnectionString_ReturnsNpgsqlConnection()
    {
        // Arrange
        var connection = new PostgresObjectDataRepositoryConnection();
        var connectionString = "Host=localhost;Database=testdb";

        // Act
        var result = connection.As<NpgsqlConnection>(connectionString);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<NpgsqlConnection>();
        result.ConnectionString.Should().Contain("localhost");
        var builder = new NpgsqlConnectionStringBuilder(result.ConnectionString);
        builder.Username.Should().Be("unit-test-user");
        builder.Password.Should().BeNull("NpgsqlDataSource redacts passwords from created connections");
    }

    [Fact]
    public void As_ReturnsNewInstanceEachTime()
    {
        // Arrange
        var connection = new PostgresObjectDataRepositoryConnection();
        var connectionString = "Host=localhost;Database=testdb";

        // Act
        var result1 = connection.As<NpgsqlConnection>(connectionString);
        var result2 = connection.As<NpgsqlConnection>(connectionString);

        // Assert
        result1.Should().NotBeSameAs(result2);
    }

    [Fact]
    public void As_ReusesCachedDataSourceForEquivalentConnectionString()
    {
        var connection = new PostgresObjectDataRepositoryConnection();
        var databaseName = $"cache_test_{Guid.NewGuid():N}";
        var connectionString = $"Host=localhost;Database={databaseName}";
        var reorderedConnectionString = $"Database={databaseName};Host=localhost";
        var before = PostgresObjectDataRepositoryConnection.CachedDataSourceCount;

        using var result1 = connection.As<NpgsqlConnection>(connectionString);
        using var result2 = connection.As<NpgsqlConnection>(reorderedConnectionString);

        PostgresObjectDataRepositoryConnection.CachedDataSourceCount.Should().Be(before + 1);
    }

    [Fact]
    public void As_WhenDataSourceCreationFails_DoesNotPoisonCache()
    {
        var connection = new PostgresObjectDataRepositoryConnection();
        var connectionString = $"Host=localhost;Database=retry_test_{Guid.NewGuid():N}";
        var before = PostgresObjectDataRepositoryConnection.CachedDataSourceCount;
        Environment.SetEnvironmentVariable("POSTGRES_TEST_KEY", null);

        FluentActions.Invoking(() => connection.As<NpgsqlConnection>(connectionString))
            .Should().Throw<InvalidOperationException>();
        PostgresObjectDataRepositoryConnection.CachedDataSourceCount.Should().Be(before);

        Environment.SetEnvironmentVariable(
            "POSTGRES_TEST_KEY",
            "{\"userid\":\"unit-test-user\",\"password\":\"unit-test-password\"}");
        using var result = connection.As<NpgsqlConnection>(connectionString);

        result.Should().NotBeNull();
        PostgresObjectDataRepositoryConnection.CachedDataSourceCount.Should().Be(before + 1);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", _previousDotNetEnvironment);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _previousAspNetCoreEnvironment);
        Environment.SetEnvironmentVariable("POSTGRES_TEST_KEY", _previousPostgresTestKey);
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

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresCredentialEnvironmentCollection
{
    public const string Name = "PostgreSQL credential environment";
}
