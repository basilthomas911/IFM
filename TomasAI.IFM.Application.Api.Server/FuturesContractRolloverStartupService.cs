using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>
/// Creates the additive rollover schema and blocks application startup until
/// the required currently traded futures contracts are valid and persisted.
/// </summary>
internal sealed class FuturesContractRolloverStartupService(
    SecuritiesSchemaDb schema,
    FuturesContractRolloverStartupCheck check,
    IMarketDataApi marketDataApi,
    TimeProvider timeProvider,
    ILogger<FuturesContractRolloverStartupService> logger) : IHostedService
{
    private DateOnly? _activeValueDate;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating or validating the futures rollover schema.");
        await schema.CreateAsync(["futures_contract_rollover"], cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation("Futures rollover schema is ready.");
        var valueDate = FuturesTradingValueDate.GetOperational(timeProvider.GetUtcNow());
        logger.LogInformation(
            "Resolving required futures rollover contracts for value date {ValueDate}.",
            valueDate);
        var rows = await check.ExecuteAsync(valueDate, cancellationToken)
            .ConfigureAwait(false);
        logger.LogInformation(
            "Resolved {RolloverCount} futures rollover rows; starting the market-data runtime.",
            rows.Count);
        await marketDataApi.StartAsync(valueDate, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        _activeValueDate = valueDate;
        logger.LogInformation(
            "Validated {RolloverCount} futures rollover rows for value date {ValueDate}.",
            rows.Count,
            valueDate);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_activeValueDate is not { } valueDate)
            return;
        await marketDataApi.StopAsync(valueDate).ConfigureAwait(false);
        _activeValueDate = null;
    }
}
