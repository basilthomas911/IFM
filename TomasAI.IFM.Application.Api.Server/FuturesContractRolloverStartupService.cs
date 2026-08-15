using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;

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
        await schema.CreateAsync(["futures_contract_rollover"], cancellationToken)
            .ConfigureAwait(false);
        var valueDate = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var rows = await check.ExecuteAsync(valueDate, cancellationToken)
            .ConfigureAwait(false);
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
