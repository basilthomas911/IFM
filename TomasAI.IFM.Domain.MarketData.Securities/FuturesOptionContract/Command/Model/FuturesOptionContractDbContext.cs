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

	internal static ValueTask<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
		this IDbContextFactory dbFactory,
		string contractId)
		=> new(dbFactory.SecuritiesDb.GetFuturesOptionContractAsync(contractId));

	internal static async ValueTask<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(
		this IDbContextFactory dbFactory,
		string symbol)
		=> [.. await dbFactory.SecuritiesDb.GetFuturesOptionContractsAsync(symbol)];

	internal static async ValueTask<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(
		this IDbContextFactory dbFactory)
		=> [.. await dbFactory.SecuritiesDb.GetFuturesOptionContractsAsync()];

	internal static async ValueTask<string[]> GetFuturesOptionContractIdsAsync(
		this IDbContextFactory dbFactory,
		string[] contractIds,
		CancellationToken cancellationToken = default)
	{
		if (contractIds.Length == 0)
			return [];

		var contracts = await (cancellationToken.CanBeCanceled
			? dbFactory.SecuritiesDb.GetFuturesOptionContractsByIdsAsync(contractIds, cancellationToken)
			: dbFactory.SecuritiesDb.GetFuturesOptionContractsByIdsAsync(contractIds));
		var existingIds = new HashSet<string>(
			contracts.Select(static contract => contract.ContractId),
			StringComparer.Ordinal);
		var existingContractIds = new List<string>(Math.Min(contractIds.Length, existingIds.Count));
		foreach (var contractId in contractIds)
			if (existingIds.Contains(contractId))
				existingContractIds.Add(contractId);
		return [.. existingContractIds];
	}

	internal static async ValueTask InsertFuturesOptionContractAsync(
		this IDbContextFactory dbFactory,
		FuturesOptionContractReadModel qfOptionContract,
		IActorService actorService)
	{
		var oc = await EnrichFuturesOptionContractAsync(qfOptionContract, qfOptionContract.ContractId, actorService);
		await dbFactory.SecuritiesDb.InsertFuturesOptionContractAsync(oc);
	}

	internal static async ValueTask InsertFuturesOptionContractsAsync(
		this IDbContextFactory dbFactory,
		FuturesOptionContractReadModel[] qfOptionContracts,
		IActorService actorService)
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
				enrichments[index] = EnrichFuturesOptionContractAsync(
					contract,
					contract.ContractId,
					actorService).AsTask();
			}

			var results = await Task.WhenAll(enrichments);
			Array.Copy(results, 0, enrichedContracts, offset, results.Length);
		}

		await dbFactory.SecuritiesDb.InsertFuturesOptionContractsAsync(enrichedContracts);
	}

	internal static async ValueTask UpdateFuturesOptionContractAsync(
		this IDbContextFactory dbFactory,
		string originalContractId,
		FuturesOptionContractReadModel qfOptionContract,
		IActorService actorService)
	{
		var oc = await EnrichFuturesOptionContractAsync(qfOptionContract, originalContractId, actorService);
		await dbFactory.SecuritiesDb.UpdateFuturesOptionContractAsync(originalContractId, oc);
	}

	internal static ValueTask DeleteFuturesOptionContractAsync(
		this IDbContextFactory dbFactory,
		string contractId)
		=> new(dbFactory.SecuritiesDb.DeleteFuturesOptionContractAsync(contractId));

	static async ValueTask<FuturesOptionContractReadModel> EnrichFuturesOptionContractAsync(
		FuturesOptionContractReadModel optionContract,
		string routeContractId,
		IActorService actorService)
	{
		var localSymbol = FuturesOptionContractReadModel.GetLocalSymbol(
			optionContract.Symbol,
			optionContract.ContractMonth);
		var normalizedContract = optionContract with
		{
			LocalSymbol = FuturesOptionContractReadModel.GetContractLocalSymbol(
				localSymbol,
				optionContract.OptionType,
				optionContract.StrikePrice)
		};
		var query = new GetFuturesOptionContractQuery(normalizedContract.ContractId, normalizedContract)
		{
			Subject = new ActorSubject(
				ActorType.Query,
				GetFuturesOptionContractQuery.Actor,
				GetFuturesOptionContractQuery.Verb,
				routeContractId),
			EntityId = new FuturesOptionContractId(routeContractId)
		};
		var serviceResult = await actorService.RequestAsync<
			FuturesOptionContractReadModel,
			GetFuturesOptionContractQuery>(query);
		return serviceResult.Success && serviceResult.Value is not null
			? serviceResult.Value
			: normalizedContract;
	}
}
