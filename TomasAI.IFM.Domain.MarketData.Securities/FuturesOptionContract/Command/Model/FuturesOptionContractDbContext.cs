using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;

internal static class FuturesOptionContractDbContext
{
    const int EnrichmentConcurrency = 8;

    extension(IDbContextFactory dbFactory)
    {
        internal ValueTask<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(string contractId) => new(dbFactory.SecuritiesDb.GetFuturesOptionContractAsync(contractId));

        internal async ValueTask<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(string symbol) => [.. await (dbFactory.SecuritiesDb.GetFuturesOptionContractsAsync(symbol)).ConfigureAwait(false)];

        internal async ValueTask<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync() => [.. await (dbFactory.SecuritiesDb.GetFuturesOptionContractsAsync()).ConfigureAwait(false)];

        internal async ValueTask<string[]> GetFuturesOptionContractIdsAsync(string[] contractIds, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(contractIds);
            if (contractIds.Length == 0)
                return [];
            var contracts = await (dbFactory.SecuritiesDb.GetFuturesOptionContractsByIdsAsync(contractIds, cancellationToken)).ConfigureAwait(false);
            var existingIds = new HashSet<string>(contracts.Select(static contract => contract.ContractId), StringComparer.Ordinal);
            var existingContractIds = new List<string>(Math.Min(contractIds.Length, existingIds.Count));
            foreach (var contractId in (contractIds).Where(contractId => existingIds.Contains(contractId)))
                existingContractIds.Add(contractId);
            return [.. existingContractIds];
        }
    }

    /// <summary>Resolves broker facts before accepted source event creation.</summary>
    internal static async Task<FuturesOptionContractReadModel[]> EnrichContractsAsync(
        FuturesOptionContractReadModel[] contracts, IActorService actorService, CancellationToken token = default)
    {
        var enriched = new FuturesOptionContractReadModel[contracts.Length];
        for (var offset = 0; offset < contracts.Length; offset += EnrichmentConcurrency)
        {
            var count = Math.Min(EnrichmentConcurrency, contracts.Length - offset);
            token.ThrowIfCancellationRequested();
            var work = Enumerable.Range(offset, count).Select(index => EnrichFuturesOptionContractAsync(
                contracts[index], contracts[index].ContractId, actorService, token).AsTask());
            var results = await Task.WhenAll(work).ConfigureAwait(false);
            Array.Copy(results, 0, enriched, offset, count);
        }

        return enriched;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1244", Justification = "Exact equality is required for persisted observation deduplication, exact contract strikes and defined zero/uninitialized sentinels; an epsilon would change these decisions.")]
    internal static async ValueTask<FuturesOptionContractReadModel> EnrichFuturesOptionContractAsync(
        FuturesOptionContractReadModel optionContract, string routeContractId, IActorService actorService, CancellationToken token = default)
    {
        // Imported reference facts must not be replaced by the legacy broker lookup.
        if (optionContract.SchemaVersion > 0)
        {
            _ = optionContract.GetExactStrikePrice();
            return optionContract;
        }

        var localSymbol = FuturesOptionContractReadModel.GetLocalSymbol(optionContract.Symbol, optionContract.ContractMonth);
        var normalizedContract = optionContract with
        {
            LocalSymbol = FuturesOptionContractReadModel.GetExactContractLocalSymbol(localSymbol, optionContract.OptionType, optionContract.GetExactStrikePrice())
        };
        var query = new GetFuturesOptionContractQuery(normalizedContract.ContractId, normalizedContract)
        {
            Subject = new ActorSubject(ActorType.Query, GetFuturesOptionContractQuery.Actor, GetFuturesOptionContractQuery.Verb, routeContractId),
            EntityId = new FuturesOptionContractId(routeContractId)
        };
        var serviceResult = token.CanBeCanceled
            ? await actorService.RequestAsync<FuturesOptionContractReadModel, GetFuturesOptionContractQuery>(query, token).ConfigureAwait(false)
            : await actorService.RequestAsync<FuturesOptionContractReadModel, GetFuturesOptionContractQuery>(query).ConfigureAwait(false);
        if (!serviceResult.Success || serviceResult.Value is null)
            return normalizedContract;
        var enriched = serviceResult.Value;
        if (!string.Equals(enriched.ContractId, optionContract.ContractId, StringComparison.Ordinal) || enriched.StrikePrice != optionContract.StrikePrice)
            throw new InvalidOperationException("Broker enrichment changed option contract identity or strike.");
        if (!enriched.IsValid)
            return normalizedContract;
        if (enriched.ContractMonth != optionContract.ContractMonth
            || !string.Equals(enriched.OptionType, optionContract.OptionType, StringComparison.OrdinalIgnoreCase)
            || (enriched.StrikePriceDecimal is { } strike && strike != optionContract.GetExactStrikePrice()))
            throw new InvalidOperationException("Broker enrichment changed the expiry, option right, or exact strike.");
        return enriched with
        {
            StrikePriceDecimal = optionContract.StrikePriceDecimal
        };
    }
}
