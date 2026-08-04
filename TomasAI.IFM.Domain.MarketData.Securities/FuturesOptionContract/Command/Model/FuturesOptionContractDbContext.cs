using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;

internal static class FuturesOptionContractDbContext
{
	internal static async ValueTask<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
		this IDbContextFactory dbFactory,
		string contractId)
		=> await dbFactory.SecuritiesDb.GetFuturesOptionContractAsync(contractId);

	internal static async ValueTask<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(
		this IDbContextFactory dbFactory,
		string symbol)
		=> [.. await dbFactory.SecuritiesDb.GetFuturesOptionContractsAsync(symbol)];

	internal static async ValueTask<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(
		this IDbContextFactory dbFactory)
		=> [.. await dbFactory.SecuritiesDb.GetFuturesOptionContractsAsync()];

	internal static async ValueTask<string[]> GetFuturesOptionContractIdsAsync(
		this IDbContextFactory dbFactory,
		string[] contractIds)
	{
		var existingContractIds = new List<string>();
		foreach (var contractId in contractIds)
		{
			if (await dbFactory.GetFuturesOptionContractAsync(contractId) is not null)
				existingContractIds.Add(contractId);
		}
		return [.. existingContractIds];
	}

	internal static async ValueTask InsertFuturesOptionContractAsync(
		this IDbContextFactory dbFactory,
		FuturesOptionContractReadModel qfOptionContract,
		IActorService actorService)
	{
		var oc = qfOptionContract;
		var localSymbol = FuturesOptionContractReadModel.GetLocalSymbol(oc.Symbol, oc.ContractMonth);
		oc = oc with { LocalSymbol = FuturesOptionContractReadModel.GetContractLocalSymbol(localSymbol, oc.OptionType, oc.StrikePrice) };
		var qry = new GetFuturesOptionContractQuery(oc.ContractId, oc)
		{
			Subject = new ActorSubject(ActorType.Query, GetFuturesOptionContractQuery.Actor, GetFuturesOptionContractQuery.Verb, oc.ContractId),
			EntityId = new FuturesOptionContractId(oc.ContractId)
		};
		var serviceResult = await actorService.RequestAsync<FuturesOptionContractReadModel, GetFuturesOptionContractQuery>(qry);
		oc = serviceResult.Success && serviceResult.Value is not null ? serviceResult.Value : oc;
		await dbFactory.SecuritiesDb.InsertFuturesOptionContractAsync(oc);
	}

	internal static async ValueTask InsertFuturesOptionContractsAsync(
		this IDbContextFactory dbFactory,
		FuturesOptionContractReadModel[] qfOptionContracts,
		IActorService actorService)
	{
		foreach (var qfOptionContract in qfOptionContracts)
			await dbFactory.InsertFuturesOptionContractAsync(qfOptionContract, actorService);
	}

	internal static async ValueTask UpdateFuturesOptionContractAsync(
		this IDbContextFactory dbFactory,
		string originalContractId,
		FuturesOptionContractReadModel qfOptionContract,
		IActorService actorService)
	{
		var oc = qfOptionContract;
		var localSymbol = FuturesOptionContractReadModel.GetLocalSymbol(oc.Symbol, oc.ContractMonth);
		oc = oc with { LocalSymbol = FuturesOptionContractReadModel.GetContractLocalSymbol(localSymbol, oc.OptionType, oc.StrikePrice) };
		var qry = new GetFuturesOptionContractQuery(oc.ContractId, oc)
		{
			Subject = new ActorSubject(ActorType.Query, GetFuturesOptionContractQuery.Actor, GetFuturesOptionContractQuery.Verb, originalContractId),
			EntityId = new FuturesOptionContractId(originalContractId)
		};
		var serviceResult = await actorService.RequestAsync<FuturesOptionContractReadModel, GetFuturesOptionContractQuery>(qry);
		oc = serviceResult.Success && serviceResult.Value is not null ? serviceResult.Value : oc;
		await dbFactory.SecuritiesDb.UpdateFuturesOptionContractAsync(originalContractId, oc);
	}

	internal static async ValueTask DeleteFuturesOptionContractAsync(
		this IDbContextFactory dbFactory,
		string contractId)
		=> await dbFactory.SecuritiesDb.DeleteFuturesOptionContractAsync(contractId);
}
