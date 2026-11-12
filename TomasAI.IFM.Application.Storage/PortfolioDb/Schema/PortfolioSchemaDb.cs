using Microsoft.Extensions.Logging;

using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.PortfolioDb.Schema;

/// <summary>Creates all Portfolio projection and financial tables after the event-log schema.</summary>
/// <param name="settings">Named database connections.</param>
/// <param name="logger">Database provider diagnostics.</param>
public sealed class PortfolioSchemaDb(IDbConnectionSettings settings, ILogger<DbProvider> logger)
    : SchemaDbContext<PortfolioSchemaDb>(settings[PortfolioDbContext.PortfolioDbConnection], logger)
{
    /// <summary>Gets the portfolio projection schema for baseline rebuilds.</summary>
    public static string CreateStatement => PortfolioSchemaSql.Portfolio;

    /// <summary>Gets the financial schema for event-store initialization and baseline rebuilds.</summary>
    public static string FinancialCreateStatement => PortfolioSchemaSql.Financial;

    /// <summary>Gets the financial-history projection schema for event-store initialization.</summary>
    public static string FinancialHistoryCreateStatement => PortfolioSchemaSql.FinancialHistory;

    static readonly SchemaObjectDefinition[] Objects =
    [
        new("portfolio_schema", PortfolioSchemaSql.Portfolio, PortfolioSchemaSql.Drop),
        new("portfolio_financial", PortfolioSchemaSql.Financial, PortfolioSchemaSql.DropFinancial)
    ];
    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
