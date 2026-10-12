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
    internal static ValueTask<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(this IDbContextFactory dbFactory, string contractId) => new(dbFactory.SecuritiesDb.GetFuturesOptionContractAsync(contractId));
    internal static async ValueTask<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(this IDbContextFactory dbFactory, string symbol) => [..await (dbFactory.SecuritiesDb.GetFuturesOptionContractsAsync(symbol)).ConfigureAwait(false)];
    internal static async ValueTask<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(this IDbContextFactory dbFactory) => [..await (dbFactory.SecuritiesDb.GetFuturesOptionContractsAsync()).ConfigureAwait(false)];
    internal static async ValueTask<string[]> GetFuturesOptionContractIdsAsync(this IDbContextFactory dbFactory, string[] contractIds, CancellationToken cancellationToken = default)
    {
        if (contractIds.Length == 0)
            return[];
        var contracts = await (dbFactory.SecuritiesDb.GetFuturesOptionContractsByIdsAsync(contractIds, cancellationToken)).ConfigureAwait(false);
        var existingIds = new HashSet<string>(contracts.Select(static contract => contract.ContractId), StringComparer.Ordinal);
        var existingContractIds = new List<string>(Math.Min(contractIds.Length, existingIds.Count));
        foreach (var contractId in (contractIds).Where(contractId => existingIds.Contains(contractId)))
            existingContractIds.Add(contractId);
        return[..existingContractIds];
    }

    /// <summary>Resolves broker facts before accepted source event creation.</summary>
    internal static async Task<FuturesOptionContractReadModel[]> EnrichContractsAsync(FuturesOptionContractReadModel[] contracts, IActorService actorService)
    {
        var enriched = new FuturesOptionContractReadModel[contracts.Length];
        for (var offset = 0; offset < contracts.Length; offset += EnrichmentConcurrency)
        {
            var count = Math.Min(EnrichmentConcurrency, contracts.Length - offset);
            var work = Enumerable.Range(offset, count).Select(index => EnrichFuturesOptionContractAsync(contracts[index], contracts[index].ContractId, actorService).AsTask());
            var results = await Task.WhenAll(work).ConfigureAwait(false);
            Array.Copy(results, 0, enriched, offset, count);
        }

        return enriched;
    }

    internal static async ValueTask InsertFuturesOptionContractAsync(this IDbContextFactory dbFactory, FuturesOptionContractReadModel qfOptionContract, IActorService actorService)
    {
        var oc = await (EnrichFuturesOptionContractAsync(qfOptionContract, qfOptionContract.ContractId, actorService)).ConfigureAwait(false);
        await (dbFactory.SecuritiesDb.InsertFuturesOptionContractAsync(oc)).ConfigureAwait(false);
    }

    internal static async ValueTask InsertFuturesOptionContractsAsync(this IDbContextFactory dbFactory, FuturesOptionContractReadModel[] qfOptionContracts, IActorService actorService)
    {
        if (qfOptionContracts.Length == 0)
            return;
        var enrichedContracts = new FuturesOptionContractReadModel[qfOptionContracts.Length];
        for (var offset = 0; offset < qfOptionContracts.Length; offset += EnrichmentConcurrency)
        {
            var count = Math.Min(EnrichmentConcurrency, qfOptionContracts.Length - offset);
            var enrichments = new Task<FuturesOptionContractReadModel>[count];
            for (var index = 0; index < count; index++)
            {
                var contract = qfOptionContracts[offset + index];
                enrichments[index] = EnrichFuturesOptionContractAsync(contract, contract.ContractId, actorService).AsTask();
            }

            var results = await (Task.WhenAll(enrichments)).ConfigureAwait(false);
            Array.Copy(results, 0, enrichedContracts, offset, results.Length);
        }

        await (dbFactory.SecuritiesDb.InsertFuturesOptionContractsAsync(enrichedContracts)).ConfigureAwait(false);
    }

    internal static async ValueTask UpdateFuturesOptionContractAsync(this IDbContextFactory dbFactory, string originalContractId, FuturesOptionContractReadModel qfOptionContract, IActorService actorService)
    {
        var oc = await (EnrichFuturesOptionContractAsync(qfOptionContract, originalContractId, actorService)).ConfigureAwait(false);
        await (dbFactory.SecuritiesDb.UpdateFuturesOptionContractAsync(originalContractId, oc)).ConfigureAwait(false);
    }

    internal static ValueTask DeleteFuturesOptionContractAsync(this IDbContextFactory dbFactory, string contractId) => new(dbFactory.SecuritiesDb.DeleteFuturesOptionContractAsync(contractId));
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S1244", Justification = "Exact equality is required for persisted observation deduplication, exact contract strikes and defined zero/uninitialized sentinels; an epsilon would change these decisions.")]
    internal static async ValueTask<FuturesOptionContractReadModel> EnrichFuturesOptionContractAsync(FuturesOptionContractReadModel optionContract, string routeContractId, IActorService actorService)
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
        var serviceResult = await (actorService.RequestAsync<FuturesOptionContractReadModel, GetFuturesOptionContractQuery>(query)).ConfigureAwait(false);
        if (!serviceResult.Success || serviceResult.Value is null)
            return normalizedContract;
        var enriched = serviceResult.Value;
        if (!string.Equals(enriched.ContractId, optionContract.ContractId, StringComparison.Ordinal) || enriched.StrikePrice != optionContract.StrikePrice)
            throw new InvalidOperationException("Broker enrichment changed option contract identity or strike.");
        return enriched with
        {
            StrikePriceDecimal = optionContract.StrikePriceDecimal
        };
    }
}
