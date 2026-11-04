using System.Reflection;
using System.Text.RegularExpressions;
using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

/// <summary>Prepares every migrated legacy Trade statement against its real PostgreSQL schema.</summary>
[Collection("PostgreSQL plan atomicity")]
public sealed class TradeSqlQualificationTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task All_legacy_statements_use_native_positional_bindings_and_valid_schema_columns()
    {
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(fixture.Connection);
        await connection.OpenAsync();
        await using (var schema = new NpgsqlCommand(TradeSchemaSql.Create, connection)) await schema.ExecuteNonQueryAsync();
        var catalog = typeof(TradeDbContext).Assembly.GetType("TomasAI.IFM.Application.Storage.TradeDb.TradeDbSql", true)!;
        var failures = new List<string>();
        foreach (var name in Statements)
        {
            var sql = (string)catalog.GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(null)!;
            Assert.DoesNotMatch(@"(?<!:):[A-Za-z]", sql);
            try
            {
                await using var prepare = new NpgsqlCommand("PREPARE qualification_" + name + " AS " + sql, connection);
                await prepare.ExecuteNonQueryAsync();
            }
            catch (PostgresException error) { failures.Add($"{name}: {error.SqlState} {error.MessageText}"); }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    static readonly string[] Statements =
    [
        "ContextStatement2", "ContextStatement5", "HelperStatement7", "HelperStatement8", "HelperStatement9", "HelperStatement10",
        "DeleteOptionTrade",
        "GetOptionTradeSpreadData",
        "GetOptionTrades",
        "InsertOptionTrade",
        "UpdateOptionTrade",
        "GetOptionTradeSpreadBarData",
        "GetTradePositions",
        "GetTradePositionsById",
        "GetOptionLegs",
        "GetOptionLegsByOrderAndTrade",
        "GetOptionLegData",
        "GetTradeTypeLimits",
        "GetTradeHistory",
        "GetTradePosition",
        "GetTradeTypeLimit",
        "GetTradeLimit",
        "GetTradeFills",
        "GetTradeFillData",
        "GetTradePlacementSignal",
        "GetTradePlans",
        "GetLastTradePlans",
        "GetTradePlansByValueDate",
        "GetTradePlanStopLossLimit",
        "GetTradePlansByDateRange",
        "GetTradePlanForwardLossRatios",
        "GetLastTradePlanForwardLossRatio",
        "GetTradePlanForwardLossLimit",
        "GetTradeLiveFeed",
        "GetTradeFillDataByTradeId",
        "GetOptionLeg",
        "InsertOptionLeg",
        "InsertOptionLegData",
        "UpdateOptionLegData",
        "GetOptionLegDataByOptionLegId",
        "DeleteTradePosition",
        "DeleteTradePositionByPrimaryKey",
        "DeleteOptionLeg",
        "DeleteOptionLegById",
        "DeleteOptionLegData",
        "DeleteOptionLegDataById",
        "DeleteTradeTypeLimit",
        "DeleteTradeLimit",
        "DeleteTradeLimitByTradeType",
        "DeleteTradeFill",
        "DeleteTradeFillById",
        "DeleteTradeFillData",
        "DeleteTradePlacementSignal",
        "DeleteTradePlacementSignalBySequenceId",
        "DeleteOptionTradeSpreadData",
        "InsertTradePosition",
        "InsertTradeLimit",
        "InsertTradeTypeLimit",
        "InsertTradeFill",
        "InsertTradeFillData",
        "InsertOptionTradeSpreadData",
        "InsertOptionTradeSpreadBarData",
        "InsertTradeLiveFeed",
        "InsertTradePositionState",
        "UpdateTradePlanForwardLossLimit",
        "InsertTradePlacementSignal",
        "UpdateTradePosition",
        "UpdateOptionTradeState",
        "UpdateTradeLiveFeed",
        "UpdateTradePositionStatus",
        "UpdateOptionLegDataStatus",
        "DeleteTradePositionState",
        "DeleteOptionTradeSpreadBarData",
        "DeleteTradeLiveFeed",
        "DeleteTradeLiveFeeds",
        "DeleteTradePlanForwardLossLimit",
        "UpdateTradeLimitDailyProfitTarget",
        "InsertTradePlanForwardLossLimit",
        "InsertTradePlan",
        "InsertTradePlanForwardLossRatio",
        "GetIntrinsicTimeStrategyWorkflow",
        "GetRegimeDiscovery",
        "GetMarketCondition",
        "GetMarketConditionHistory",
        "UpsertMarketCondition",
        "UpsertMarketConditionByFund",
        "UpsertRegimeDiscovery",
        "GetActiveIntrinsicTimeStrategyWorkflow",
        "GetIntrinsicTimeStrategyWorkflowStartAttempts",
        "GetIntrinsicTimeStrategyWorkflowTimeline",
        "GetIntrinsicTimeStrategyWorkflowsByEntity",
        "GetIntrinsicTimeStrategyWorkflowsByStatusDay",
        "UpsertIntrinsicTimeStrategyWorkflow",
        "UpsertActiveIntrinsicTimeStrategyWorkflow",
        "DeleteActiveIntrinsicTimeStrategyWorkflow",
        "InsertIntrinsicTimeStrategyWorkflowStartAttempt",
        "InsertIntrinsicTimeStrategyWorkflowTimeline",
        "UpsertIntrinsicTimeStrategyWorkflowByEntity",
        "UpsertIntrinsicTimeStrategyWorkflowByStatusDay",
    ];
}
