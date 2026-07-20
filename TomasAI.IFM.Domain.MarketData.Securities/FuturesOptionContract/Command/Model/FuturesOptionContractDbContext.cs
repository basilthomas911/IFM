using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;

namespace TomasAI.IFM.Domain.MarketData.Securities.FuturesOptionContract.Command.Model;

internal static class FuturesOptionContractDbContext
{
	internal static async ValueTask<FuturesOptionContractReadModel?> GetFuturesOptionContractAsync(
		this IDbContextFactory dbFactory,
		string contractId)
		=> await dbFactory.SecuritiesDb
			.Use(FuturesOptionContractDbCql.GetFuturesOptionContract)
			.SetParameters(new GetFuturesOptionContract(contractId))
			.ExecuteSingleAsync(MapToFuturesOptionContract);

	internal static async ValueTask<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(
		this IDbContextFactory dbFactory,
		string symbol)
		=> [.. await dbFactory.SecuritiesDb
			.Use(FuturesOptionContractDbCql.GetFuturesOptionContractsBySymbol)
			.SetParameters(new GetFuturesOptionContractsBySymbol(symbol))
			.ExecuteQueryAsync(MapToFuturesOptionContract)];

	internal static async ValueTask<FuturesOptionContractReadModel[]> GetFuturesOptionContractsAsync(
		this IDbContextFactory dbFactory)
		=> [.. await dbFactory.SecuritiesDb
			.Use(FuturesOptionContractDbCql.GetFuturesOptionContracts)
			.ExecuteQueryAsync(MapToFuturesOptionContract)];

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
		await dbFactory.SecuritiesDb
			.Use(FuturesOptionContractDbCql.InsertFuturesOptionContract)
			.SetParameters(new UpsertFuturesOptionContract(
				oc.ContractId,
				oc.Description,
				oc.Symbol,
				oc.LocalSymbol,
				oc.SecurityType,
				oc.Currency,
				oc.Exchange,
				oc.Multiplier,
				oc.ContractMonth,
				oc.StrikePrice,
				oc.OptionType))
			.ExecuteCommandAsync();
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
		var db = dbFactory.SecuritiesDb;
		List<object> queuedCommands =
		[
			db.Use(FuturesOptionContractDbCql.DeleteFuturesOptionContractById)
				.SetParameters(new DeleteFuturesOptionContractById(originalContractId))
				.QueueCommand(),
			db.Use(FuturesOptionContractDbCql.InsertFuturesOptionContract)
				.SetParameters(new UpsertFuturesOptionContract(
					oc.ContractId,
					oc.Description,
					oc.Symbol,
					oc.LocalSymbol,
					oc.SecurityType,
					oc.Currency,
					oc.Exchange,
					oc.Multiplier,
					oc.ContractMonth,
					oc.StrikePrice,
					oc.OptionType))
				.QueueCommand()
		];
		await db.ExecuteQueuedCommandsAsync(queuedCommands, false);
	}

	internal static async ValueTask DeleteFuturesOptionContractAsync(
		this IDbContextFactory dbFactory,
		string contractId)
		=> await dbFactory.SecuritiesDb
			.Use(FuturesOptionContractDbCql.DeleteFuturesOptionContractById)
			.SetParameters(new DeleteFuturesOptionContractById(contractId))
			.ExecuteCommandAsync();

	static FuturesOptionContractReadModel MapToFuturesOptionContract<TDataRecord>(TDataRecord e) where TDataRecord : IObjectDataRecord
		=> new(
			contractId: e.GetString(0),
			description: e.GetString(1),
			symbol: e.GetString(2),
			localSymbol: e.GetString(3),
			securityType: e.GetString(4),
			currency: e.GetString(5),
			exchange: e.GetString(6),
			multiplier: e.GetString(7),
			contractMonth: e.GetDateOnly(8),
			strikePrice: e.GetDouble(9),
			optionType: e.GetString(10));

	internal readonly record struct GetFuturesOptionContract(string contractId) : IBindValue
	{
		public object Bind() => new { contractId };
	}

	internal readonly record struct GetFuturesOptionContractsBySymbol(string symbol) : IBindValue
	{
		public object Bind() => new { symbol };
	}

	internal readonly record struct UpsertFuturesOptionContract(
		string contractId,
		string description,
		string symbol,
		string localSymbol,
		string securityType,
		string currency,
		string exchange,
		string multiplier,
		DateOnly contractMonth,
		double strikePrice,
		string optionType) : IBindValue
	{
		public object Bind() => new
		{
			contractId,
			description,
			symbol,
			localSymbol,
			securityType,
			currency,
			exchange,
			multiplier,
			contractMonth,
			strikePrice,
			optionType
		};
	}

	internal readonly record struct DeleteFuturesOptionContractById(string contractId) : IBindValue
	{
		public object Bind() => new { contractId };
	}
}
