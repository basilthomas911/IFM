using System;
using System.Collections.Generic;
using Cassandra;
using Npgsql;
using TomasAI.IFM.Framework.Storage;
using Xunit;

namespace TomasAI.IFM.Framework.Storage.UnitTests;

public sealed class DatabaseCredentialResolverTests
{
    const string ValidCredentials = "{\"userid\":\"test-user\",\"password\":\"test-password\"}";

    [Fact]
    public void Resolve_DefaultsToDevelopmentWhenNoRuntimeEnvironmentIsSet()
    {
        var environment = new Dictionary<string, string?>
        {
            ["POSTGRES_DEV_KEY"] = ValidCredentials
        };

        var credentials = DatabaseCredentialResolver.Resolve(
            DatabaseProvider.Postgres,
            name => environment.GetValueOrDefault(name));

        Assert.Equal("test-user", credentials.UserId);
        Assert.Equal("test-password", credentials.Password);
    }

    [Theory]
    [InlineData("Dev", (int)DatabaseRuntimeEnvironment.Development)]
    [InlineData("Development", (int)DatabaseRuntimeEnvironment.Development)]
    [InlineData("Testing", (int)DatabaseRuntimeEnvironment.Test)]
    [InlineData("Test", (int)DatabaseRuntimeEnvironment.Test)]
    [InlineData("Stage", (int)DatabaseRuntimeEnvironment.Staging)]
    [InlineData("Staging", (int)DatabaseRuntimeEnvironment.Staging)]
    [InlineData("Prod", (int)DatabaseRuntimeEnvironment.Production)]
    [InlineData("Production", (int)DatabaseRuntimeEnvironment.Production)]
    public void ResolveRuntimeEnvironment_NormalizesSupportedNames(
        string value,
        int expected)
        => Assert.Equal((DatabaseRuntimeEnvironment)expected, DatabaseCredentialResolver.ResolveRuntimeEnvironment(value, null));

    [Fact]
    public void ResolveRuntimeEnvironment_AllowsEquivalentDotNetAndAspNetCoreValues()
        => Assert.Equal(
            DatabaseRuntimeEnvironment.Production,
            DatabaseCredentialResolver.ResolveRuntimeEnvironment("Production", "prod"));

    [Fact]
    public void ResolveRuntimeEnvironment_RejectsConflictingEnvironmentVariables()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => DatabaseCredentialResolver.ResolveRuntimeEnvironment("Development", "Production"));

        Assert.Contains("different runtime environments", exception.Message);
    }

    [Fact]
    public void ResolveRuntimeEnvironment_RejectsUnsupportedEnvironment()
        => Assert.Throws<InvalidOperationException>(
            () => DatabaseCredentialResolver.ResolveRuntimeEnvironment("Sandbox", null));

    [Theory]
    [InlineData((int)DatabaseProvider.Postgres, (int)DatabaseRuntimeEnvironment.Development, "POSTGRES_DEV_KEY")]
    [InlineData((int)DatabaseProvider.Postgres, (int)DatabaseRuntimeEnvironment.Test, "POSTGRES_TEST_KEY")]
    [InlineData((int)DatabaseProvider.Postgres, (int)DatabaseRuntimeEnvironment.Staging, "POSTGRES_STAGING_KEY")]
    [InlineData((int)DatabaseProvider.Postgres, (int)DatabaseRuntimeEnvironment.Production, "POSTGRES_PROD_KEY")]
    [InlineData((int)DatabaseProvider.ScyllaDb, (int)DatabaseRuntimeEnvironment.Development, "SCYLLADB_DEV_KEY")]
    [InlineData((int)DatabaseProvider.ScyllaDb, (int)DatabaseRuntimeEnvironment.Test, "SCYLLADB_TEST_KEY")]
    [InlineData((int)DatabaseProvider.ScyllaDb, (int)DatabaseRuntimeEnvironment.Staging, "SCYLLADB_STAGING_KEY")]
    [InlineData((int)DatabaseProvider.ScyllaDb, (int)DatabaseRuntimeEnvironment.Production, "SCYLLADB_PROD_KEY")]
    public void GetEnvironmentVariableName_ReturnsProviderEnvironmentKey(
        int provider,
        int environment,
        string expected)
        => Assert.Equal(
            expected,
            DatabaseCredentialResolver.GetEnvironmentVariableName(
                (DatabaseProvider)provider,
                (DatabaseRuntimeEnvironment)environment));

    [Fact]
    public void Resolve_UsesCaseInsensitiveJsonProperties()
    {
        var environment = TestEnvironment("POSTGRES_TEST_KEY", "{\"UserId\":\"case-user\",\"PASSWORD\":\"case-password\"}");

        var credentials = DatabaseCredentialResolver.Resolve(
            DatabaseProvider.Postgres,
            name => environment.GetValueOrDefault(name));

        Assert.Equal("case-user", credentials.UserId);
        Assert.Equal("case-password", credentials.Password);
    }

    [Fact]
    public void Resolve_RejectsMissingCredentialVariableWithoutExposingValues()
    {
        var environment = TestEnvironment("UNRELATED_KEY", "do-not-log-this");

        var exception = Assert.Throws<InvalidOperationException>(
            () => DatabaseCredentialResolver.Resolve(
                DatabaseProvider.Postgres,
                name => environment.GetValueOrDefault(name)));

        Assert.Contains("POSTGRES_TEST_KEY", exception.Message);
        Assert.DoesNotContain("do-not-log-this", exception.Message);
    }

    [Fact]
    public void Resolve_RejectsMalformedJsonWithoutExposingJson()
    {
        const string malformed = "{secret-password";
        var environment = TestEnvironment("POSTGRES_TEST_KEY", malformed);

        var exception = Assert.Throws<InvalidOperationException>(
            () => DatabaseCredentialResolver.Resolve(
                DatabaseProvider.Postgres,
                name => environment.GetValueOrDefault(name)));

        Assert.Contains("valid JSON", exception.Message);
        Assert.DoesNotContain(malformed, exception.Message);
    }

    [Theory]
    [InlineData("{\"userid\":\"\",\"password\":\"password\"}", "userid")]
    [InlineData("{\"userid\":\"user\",\"password\":\"\"}", "password")]
    [InlineData("{}", "userid")]
    public void Resolve_RejectsMissingCredentialFields(string json, string expectedField)
    {
        var environment = TestEnvironment("SCYLLADB_TEST_KEY", json);

        var exception = Assert.Throws<InvalidOperationException>(
            () => DatabaseCredentialResolver.Resolve(
                DatabaseProvider.ScyllaDb,
                name => environment.GetValueOrDefault(name)));

        Assert.Contains(expectedField, exception.Message);
    }

    [Fact]
    public void AddPostgresCredentials_PreservesEndpointAndAddsCredentials()
    {
        const string baseConnection = "Host=localhost;Port=5432;Database=event-source-test-db";

        var resolved = DatabaseCredentialResolver.AddPostgresCredentials(
            baseConnection,
            new DatabaseCredentials("postgres-user", "postgres-password"));
        var builder = new NpgsqlConnectionStringBuilder(resolved);

        Assert.Equal("localhost", builder.Host);
        Assert.Equal(5432, builder.Port);
        Assert.Equal("event-source-test-db", builder.Database);
        Assert.Equal("postgres-user", builder.Username);
        Assert.Equal("postgres-password", builder.Password);
    }

    [Fact]
    public void AddPostgresCredentials_RejectsInlineCredentials()
    {
        var builder = new NpgsqlConnectionStringBuilder("Host=localhost;Database=test")
        {
            Username = "inline-user"
        };

        Assert.Throws<InvalidOperationException>(() =>
            DatabaseCredentialResolver.AddPostgresCredentials(
                builder.ConnectionString,
                new DatabaseCredentials("environment-user", "environment-password")));
    }

    [Fact]
    public void GetScyllaConnectionSettings_PreservesEndpointAndKeyspace()
    {
        var builder = DatabaseCredentialResolver.GetScyllaConnectionSettings(
            "Contact Points=localhost;Port=9042;Default Keyspace=fund_test_db");

        Assert.Equal(["localhost"], builder.ContactPoints);
        Assert.Equal(9042, builder.Port);
        Assert.Equal("fund_test_db", builder.DefaultKeyspace);
    }

    [Fact]
    public void GetScyllaConnectionSettings_RejectsInlineCredentials()
    {
        var builder = new CassandraConnectionStringBuilder(
            "Contact Points=localhost;Default Keyspace=test")
        {
            Username = "inline-user"
        };

        Assert.Throws<InvalidOperationException>(() =>
            DatabaseCredentialResolver.GetScyllaConnectionSettings(builder.ConnectionString));
    }

    static Dictionary<string, string?> TestEnvironment(string credentialName, string credentialValue)
        => new()
        {
            ["DOTNET_ENVIRONMENT"] = "Test",
            [credentialName] = credentialValue
        };
}
