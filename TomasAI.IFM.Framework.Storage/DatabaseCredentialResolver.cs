using System.Collections.Concurrent;
using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.Text;
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
        var builder = GetPostgresConnectionSettings(connectionString);
        builder.Username = credentials.UserId;
        builder.Password = credentials.Password;
        return builder.ConnectionString;
    }

    internal static NpgsqlConnectionStringBuilder GetPostgresConnectionSettings(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        RejectInlineCredentials(builder.Username, builder.Password, DatabaseProvider.Postgres);
        return builder;
    }

    internal static CassandraConnectionStringBuilder GetScyllaConnectionSettings(string connectionString)
    {
        var builder = new CassandraConnectionStringBuilder(connectionString);
        RejectInlineCredentials(builder.Username, builder.Password, DatabaseProvider.ScyllaDb);
        return builder;
    }

    /// <summary>
    /// Produces an order-independent cache key from a parsed connection string.
    /// Connection-string builders preserve the caller's keyword order in
    /// <see cref="DbConnectionStringBuilder.ConnectionString"/>, so that value is not a
    /// reliable identity for a process-wide data-source/session cache.
    /// </summary>
    internal static string GetCanonicalConnectionKey(DbConnectionStringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var keys = builder.Keys
            .Cast<string>()
            .OrderBy(static key => key, StringComparer.OrdinalIgnoreCase);
        var result = new StringBuilder(builder.ConnectionString.Length);
        foreach (var key in keys)
        {
            var value = FormatConnectionValue(builder[key]);
            // Length prefixes make the key unambiguous even when values contain
            // connection-string separator characters.
            result.Append(key.Length)
                .Append(':')
                .Append(key.ToUpperInvariant())
                .Append('=')
                .Append(value.Length)
                .Append(':')
                .Append(value)
                .Append(';');
        }

        return result.ToString();
    }

    static string FormatConnectionValue(object? value)
    {
        if (value is null)
            return string.Empty;
        if (value is string text)
            return text;
        if (value is IEnumerable values)
        {
            var result = new StringBuilder();
            foreach (var item in values)
            {
                var itemText = FormatConnectionValue(item);
                result.Append(itemText.Length).Append(':').Append(itemText).Append(';');
            }

            return result.ToString();
        }

        return value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty
            : value.ToString() ?? string.Empty;
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
