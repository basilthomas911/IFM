using System.Data.Common;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.Databento;

/// <summary>
/// Prevents a synthetic feed from writing deterministic test values into a shared
/// development, paper-trading, or production persistence target.
/// </summary>
public static class SyntheticPersistenceIsolationGuard
{
    public static void Validate(
        DatabentoFeedOptions feedOptions,
        string? eventSourceConnectionString,
        string? marketDataConnectionString)
    {
        ArgumentNullException.ThrowIfNull(feedOptions);
        if (feedOptions.DataSource != FeedDataSourceMode.Synthetic)
            return;

        if (feedOptions.DeploymentProfile != FeedDeploymentProfile.SyntheticCi)
        {
            throw new InvalidOperationException(
                "The synthetic Databento feed may persist snapshots only under the SyntheticCi deployment profile.");
        }

        RequireSyntheticTarget(
            eventSourceConnectionString,
            "Database",
            "EventSourceActorDbConnection");
        RequireSyntheticTarget(
            marketDataConnectionString,
            "Default Keyspace",
            "MarketDataDbConnection");
    }

    static void RequireSyntheticTarget(
        string? connectionString,
        string targetKey,
        string connectionName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw InvalidTarget(connectionName, targetKey);

        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };
        if (!builder.TryGetValue(targetKey, out var value)
            || value is not string target
            || !target.Contains("synthetic", StringComparison.OrdinalIgnoreCase))
        {
            throw InvalidTarget(connectionName, targetKey);
        }
    }

    static InvalidOperationException InvalidTarget(
        string connectionName,
        string targetKey) => new(
            $"Synthetic Databento persistence requires {connectionName} {targetKey} to identify an isolated synthetic store.");
}
