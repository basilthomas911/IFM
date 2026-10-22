using TomasAI.IFM.Domain.Reference.Repository;
using TomasAI.IFM.Application.Api.Server.Core.MarketData.InstrumentDefinitions;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.Application.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using System.Diagnostics;
using TomasAI.IFM.Application.Storage;

namespace TomasAI.IFM.Application.Api.Server.Core.MarketData.OptionChains;

public sealed record OptionContractExpiryCalendarOptions
{
    public string[] Symbols { get; init; } = ["ES"];
    public int MaximumProviderConcurrency { get; init; } = 2;
    public int MaximumVerificationConcurrency { get; init; } = 4;
    public bool EnableDiagnosticWindowBenchmark { get; init; }

    public OptionContractExpiryCalendarOptions Validate()
    {
        if (MaximumProviderConcurrency is < 1 or > 16)
            throw new InvalidOperationException(
                $"{nameof(MaximumProviderConcurrency)} must be between 1 and 16.");
        if (MaximumVerificationConcurrency is < 1 or > 16)
            throw new InvalidOperationException(
                $"{nameof(MaximumVerificationConcurrency)} must be between 1 and 16.");
        return this;
    }
}

internal static class OptionRefreshAlgorithms
{
    internal static string FindUnderlyingContractId(
        IReadOnlyList<FuturesContractV3ReadModel> orderedFutures,
        DateOnly expiry)
    {
        ArgumentNullException.ThrowIfNull(orderedFutures);
        if (orderedFutures.Count == 0)
            throw new ArgumentException("At least one futures contract is required.", nameof(orderedFutures));
        var low = 0;
        var high = orderedFutures.Count - 1;
        while (low < high)
        {
            var middle = low + ((high - low) >> 1);
            if (orderedFutures[middle].LastTradeDate < expiry)
                low = middle + 1;
            else
                high = middle;
        }
        return orderedFutures[low].LastTradeDate >= expiry
            ? orderedFutures[low].ContractId
            : orderedFutures[^1].ContractId;
    }
}

public sealed class OptionContractExpiryCalendarRefreshService(IMarketDataApi marketData)
{
    public Task<int> RefreshAsync(string symbol, CancellationToken cancellationToken = default)
        => marketData.RefreshOptionCalendarAsync(symbol, cancellationToken);
}

public sealed class OptionContractExpiryCalendarStartupService(
    IApplicationStartupStatusStore startupStatus,
    OptionContractExpiryCalendarRefreshService refresh,
    OptionContractExpiryCalendarOptions options,
    ILogger<OptionContractExpiryCalendarStartupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var status = startupStatus.Current;
        while (status.State is not (ApplicationLifecycleState.Running or ApplicationLifecycleState.Degraded))
        {
            status = await startupStatus.WaitForChangeAsync(status, stoppingToken).ConfigureAwait(false);
        }

        foreach (var symbol in options.Symbols.Where(symbol => !string.IsNullOrWhiteSpace(symbol)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                await refresh.RefreshAsync(symbol, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception,"{Component}.{Method} "+"Option-expiry startup refresh failed for {Symbol}; the prior published cache remains active.",nameof(OptionContractExpiryCalendarStartupService),nameof(ExecuteAsync),symbol);
            }
        }
    }

}
