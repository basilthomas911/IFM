using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage;

/// <summary>Creates the shared PostgreSQL domain connections and the sole raw ScyllaDB connection.</summary>
public static class ApplicationStorageConnections
{
    /// <summary>Uses one PostgreSQL identity for all transactional domain schemas and an explicit raw quote destination.</summary>
    public static DbConnectionSettings Create(string postgresConnection, string rawScyllaConnection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawScyllaConnection);
        var settings = CreatePostgres(postgresConnection);
        settings.Add("TradeTickQuoteDbConnection", rawScyllaConnection, "System.Data.ScyllaDb");
        return settings;
    }

    /// <summary>Creates the single PostgreSQL identity for domain-only maintenance processes.</summary>
    public static DbConnectionSettings CreatePostgres(string postgresConnection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(postgresConnection);
        _ = EventSourceDb.PostgresDatabaseIdentity.Parse(postgresConnection);
        var settings = new DbConnectionSettings();
        string[] postgresContexts =
        [
            "EventSourceActorDbConnection", "ConfigurationDbConnection", "MarketDataServiceDbConnection",
            "SystemAdminDbConnection", "SequenceIdDbConnection", "PortfolioDbConnection", "MarketDataDbConnection",
            "ReferenceDbConnection", "SecuritiesDbConnection", "TradeDbConnection",
            "TradePlanDbConnection", "ScheduledTaskDbConnection"
        ];
        foreach (var name in postgresContexts)
            settings.Add(name, postgresConnection, "System.Data.Postgres");
        return settings;
    }
}
