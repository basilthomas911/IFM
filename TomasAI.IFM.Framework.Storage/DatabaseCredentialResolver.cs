using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cassandra;
using Npgsql;

namespace TomasAI.IFM.Framework.Storage;

internal enum DatabaseProvider
{
    Postgres,
    ScyllaDb
}
internal enum DatabaseRuntimeEnvironment
{
    Development,
    Test,
    Staging,
    Production
}

internal readonly record struct DatabaseCredentials(string UserId, string Password);

internal static class DatabaseCredentialResolver
{
    const string DotNetEnvironment = "DOTNET_ENVIRONMENT";
    const string AspNetCoreEnvironment = "ASPNETCORE_ENVIRONMENT";

    static readonly ConcurrentDictionary<string, CachedCredential> CredentialCache = new(StringComparer.Ordinal);
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static DatabaseCredentials Resolve(DatabaseProvider provider)
        => Resolve(provider, Environment.GetEnvironmentVariable);

    internal static DatabaseCredentials Resolve(
        DatabaseProvider provider,
        Func<string, string?> getEnvironmentVariable)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        var environment = ResolveRuntimeEnvironment(
            getEnvironmentVariable(DotNetEnvironment),
            getEnvironmentVariable(AspNetCoreEnvironment));
        var variableName = GetEnvironmentVariableName(provider, environment);
        var json = getEnvironmentVariable(variableName);

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException($"Database credential environment variable '{variableName}' is missing or empty.");

        var cached = CredentialCache.AddOrUpdate(
            variableName,
            _ => new CachedCredential(json, Parse(variableName, json)),
            (_, existing) => string.Equals(existing.Json, json, StringComparison.Ordinal)
                ? existing
                : new CachedCredential(json, Parse(variableName, json)));

        return cached.Credentials;
    }

    internal static DatabaseRuntimeEnvironment ResolveRuntimeEnvironment(
        string? dotNetEnvironment,
        string? aspNetCoreEnvironment)
    {
        var dotNet = NormalizeOptionalEnvironment(dotNetEnvironment);
        var aspNetCore = NormalizeOptionalEnvironment(aspNetCoreEnvironment);

        if (dotNet.HasValue && aspNetCore.HasValue && dotNet.Value != aspNetCore.Value)
        {
            throw new InvalidOperationException(
                $"{DotNetEnvironment} and {AspNetCoreEnvironment} select different runtime environments.");
        }

        return dotNet ?? aspNetCore ?? DatabaseRuntimeEnvironment.Development;
    }

    internal static string GetEnvironmentVariableName(
        DatabaseProvider provider,
        DatabaseRuntimeEnvironment environment)
    {
        var providerName = provider switch
        {
            DatabaseProvider.Postgres => "POSTGRES",
            DatabaseProvider.ScyllaDb => "SCYLLADB",
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null)
        };
        var environmentName = environment switch
        {
            DatabaseRuntimeEnvironment.Development => "DEV",
            DatabaseRuntimeEnvironment.Test => "TEST",
            DatabaseRuntimeEnvironment.Staging => "STAGING",
            DatabaseRuntimeEnvironment.Production => "PROD",
            _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, null)
        };
        return $"{providerName}_{environmentName}_KEY";
    }

    internal static string AddPostgresCredentials(string connectionString, DatabaseCredentials credentials)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        RejectInlineCredentials(builder.Username, builder.Password, DatabaseProvider.Postgres);
        builder.Username = credentials.UserId;
        builder.Password = credentials.Password;
        return builder.ConnectionString;
    }

    internal static CassandraConnectionStringBuilder GetScyllaConnectionSettings(string connectionString)
    {
        var builder = new CassandraConnectionStringBuilder(connectionString);
        RejectInlineCredentials(builder.Username, builder.Password, DatabaseProvider.ScyllaDb);
        return builder;
    }

    static DatabaseCredentials Parse(string variableName, string json)
    {
        CredentialDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<CredentialDocument>(json, JsonOptions);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                $"Database credential environment variable '{variableName}' does not contain valid JSON.");
        }

        if (document is null || string.IsNullOrWhiteSpace(document.UserId))
            throw new InvalidOperationException($"Database credential environment variable '{variableName}' has no userid.");
        if (string.IsNullOrWhiteSpace(document.Password))
            throw new InvalidOperationException($"Database credential environment variable '{variableName}' has no password.");

        return new DatabaseCredentials(document.UserId, document.Password);
    }

    static DatabaseRuntimeEnvironment? NormalizeOptionalEnvironment(string? environment)
    {
        if (string.IsNullOrWhiteSpace(environment))
            return null;

        return environment.Trim().ToUpperInvariant() switch
        {
            "DEVELOPMENT" or "DEV" => DatabaseRuntimeEnvironment.Development,
            "TEST" or "TESTING" => DatabaseRuntimeEnvironment.Test,
            "STAGING" or "STAGE" => DatabaseRuntimeEnvironment.Staging,
            "PRODUCTION" or "PROD" => DatabaseRuntimeEnvironment.Production,
            _ => throw new InvalidOperationException($"Unsupported database runtime environment '{environment}'.")
        };
    }

    static void RejectInlineCredentials(string? userId, string? password, DatabaseProvider provider)
    {
        if (!string.IsNullOrWhiteSpace(userId) || !string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                $"The {provider} base connection string must not contain a username or password.");
        }
    }

    sealed record CachedCredential(string Json, DatabaseCredentials Credentials);

    sealed class CredentialDocument
    {
        [JsonPropertyName("userid")]
        public string? UserId { get; init; }

        [JsonPropertyName("password")]
        public string? Password { get; init; }
    }
}
