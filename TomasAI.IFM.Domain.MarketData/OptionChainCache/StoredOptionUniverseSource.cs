using System.Collections.Immutable;
using Microsoft.Extensions.Hosting;
using TomasAI.IFM.Application.MarketData.OptionChainCache;
using TomasAI.IFM.Domain.MarketData.Query;
using TomasAI.IFM.Domain.MarketData.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.OptionChainCache;

/// <summary>Reads global PostgreSQL policies and reviewed definitions on the hosted background path.</summary>
public sealed class StoredOptionUniverseSource(TomasAI.IFM.Application.Storage.IDbContextFactory databases,
    TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi api,
    TomasAI.IFM.Application.MarketData.Pricing.TreasuryPublicationPolicy publication,
    TomasAI.IFM.Shared.MarketData.Contracts.TreasuryRateConversionPolicy conversion,
    IHostEnvironment environment, IOptionUniversePlanner planner, OptionChainCoverageObservations coverage,
    Microsoft.Extensions.Logging.ILogger<StoredOptionUniverseSource> logger) : IOptionUniverseSource
{
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "This actor/service boundary records the exception and contains the failure according to its existing lifecycle or message contract.")] 
    /// <inheritdoc />
    public async Task<ImmutableArray<PreparedOptionUniverse>> LoadAsync(DateOnly valueDate, CancellationToken cancellationToken)
    {
        var parameters = await databases.MarketDataDb.ReadPublishedAsync(environment.EnvironmentName, cancellationToken).ConfigureAwait(false);
        if (parameters.IsEmpty) return [];
        var output = ImmutableArray.CreateBuilder<PreparedOptionUniverse>();
        foreach (var policy in parameters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
            var rows = policy.BiasRows.Where(x => x.Enabled).ToArray();
            if (rows.Length == 0) continue;
            var future = await databases.SecuritiesDb.GetOnTheRunFuturesContractAsync(policy.InstrumentRoot, cancellationToken).ConfigureAwait(false);
            if (future is null) throw new InvalidDataException("No authoritative futures contract is available for strategy cache coverage.");
            var price = await api.GetFuturesPriceAsync(future.ContractId).ConfigureAwait(false);
            if (price is not > 0) continue;
            var through = valueDate.AddDays(rows.Max(x => x.MaximumDte));
            var definitions = await ReadDefinitionsAsync(policy.InstrumentRoot,
                valueDate.AddDays(rows.Min(x => x.MinimumDte)), through, cancellationToken).ConfigureAwait(false);
            if (definitions.Count == 0) continue;
            var first = definitions.Values.First();
            var tradingDates = await databases.MarketDataDb.GetTradingDatesAsync(valueDate, through, MarketType.Futures, CurrencyType.USD, cancellationToken).ConfigureAwait(false);
            var calendar = new TomasAI.IFM.Shared.MarketData.Contracts.Pricing.OptionPricingCalendar(
                first.CalendarVersion ?? "IFM-MarketDates", first.ExchangeTimeZoneId ?? "America/New_York", valueDate, through,
                new TimeOnly(18, 0), tradingDates.ToImmutableArray());
            var underlyingPrices = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var underlying in definitions.Values.Select(x => QualifiedEvaluatedOptionChain.Candidate(x).Definition.Underlying).Distinct(StringComparer.Ordinal))
            {
                var mark = await api.GetFuturesPriceAsync(underlying).ConfigureAwait(false);
                if (mark is > 0) underlyingPrices.Add(underlying, mark.Value);
            }
            // Qualified observed IV expands subscriptions on the background policy refresh. Bootstrap coverage is never execution IV.
            var planned = planner.Plan(policy, valueDate, definitions.Values.Select(QualifiedEvaluatedOptionChain.Candidate).ToImmutableArray(),
                true, price.Value, coverage.Read(policy.ParameterSetId), calendar, publication, conversion, underlyingPrices);
            if (!string.IsNullOrEmpty(planned.ReasonCode))
                throw new InvalidDataException($"Strategy cache coverage unavailable: {policy.ParameterSetId}; {planned.ReasonCode}");
            var selectedIds = planned.Universes.SelectMany(x => x.MarketData.Options).Select(x => x.ContractId).ToHashSet(StringComparer.Ordinal);
            await QualifiedEvaluatedOptionChain.PublishSelectedReferencesAsync("strategy:" + policy.ParameterSetId,
                databases.SecuritiesDb, logger, definitions.Values.Where(x => selectedIds.Contains(x.ContractId)).ToArray(), cancellationToken).ConfigureAwait(false);
            output.AddRange(planned.Universes);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                
            var diagnosticLogger = logger;
            if (diagnosticLogger.IsEnabled(global::Microsoft.Extensions.Logging.LogLevel.Warning))
            {
                global::TomasAI.IFM.Domain.MarketData.StorageMigrationDiagnosticLog.Write5(diagnosticLogger, ex, policy.ParameterSetId, policy.Version, valueDate);
            }
        
            }
        }
        return output.ToImmutable();
    }    /// <summary>Loads cached contract definitions for the full configured expiry range on the background refresh path.</summary>
    async Task<Dictionary<string, FuturesOptionContractReadModel>> ReadDefinitionsAsync(string instrumentRoot,
        DateOnly from, DateOnly through, CancellationToken cancellationToken)
    {
            var expirations = await databases.SecuritiesDb.GetOptionContractExpiriesAsync(instrumentRoot,
                from, through, cancellationToken).ConfigureAwait(false);
            var definitions = new Dictionary<string, FuturesOptionContractReadModel>(StringComparer.Ordinal);
            foreach (var expiry in expirations.Select(x => (x.ExpiryDate, x.ContractId)).Distinct())
            {
                var found = await databases.SecuritiesDb.GetCachedOptionContractDefinitionsAsync(instrumentRoot,
                    expiry.ContractId, expiry.ExpiryDate, null, cancellationToken).ConfigureAwait(false);
                foreach (var row in found) definitions.TryAdd(row.Definition.ContractId, row.Definition);
            }
        return definitions;
    }

}
