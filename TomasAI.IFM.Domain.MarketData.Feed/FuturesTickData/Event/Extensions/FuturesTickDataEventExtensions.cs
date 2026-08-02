using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.Events;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Extensions;

internal static class FuturesTickDataEventExtensions
{
	internal static async ValueTask<FuturesEodDataV2ReadModel?> GetFuturesEodDataAsync(
		this IEventActorContext context,
		string contractId,
		DateOnly valueDate)
	{
		FuturesEodDataV2ReadModel? futureEodData = default;
		var entityId = new GetFuturesEodDataParameter(contractId, valueDate);
		GetFuturesEodDataQuery query = new(contractId, valueDate)
		{
			Subject = new(ActorType.Query, GetFuturesEodDataQuery.Actor, GetFuturesEodDataQuery.Verb, entityId.Format()),
			EntityId = entityId,
			ErrorCode = GetFuturesEodDataQuery.ErrorId
		};
		var serviceResult = await context.RequestAsync<FuturesEodDataV2ReadModel, GetFuturesEodDataQuery>(query);
		if (serviceResult.Success && serviceResult is not null)
			futureEodData = serviceResult.Value;
		return futureEodData;
	}

	internal static async ValueTask<VixFuturesEodDataReadModel[]> GetVixFuturesEodDataAsync(
		this IEventActorContext context,
		string contractId,
		DateOnly valueDate)
	{
		VixFuturesEodDataReadModel[] vixFutureEodData = [];
		var entityId = new GetVixFuturesEodDataParameter(contractId, valueDate);
		GetVixFuturesEodDataQuery query = new()
		{
			Subject = new(ActorType.Query, GetVixFuturesEodDataQuery.Actor, GetVixFuturesEodDataQuery.Verb, entityId.Format()),
			EntityId = entityId,
			ErrorCode = GetVixFuturesEodDataQuery.ErrorId
		};
		var serviceResult = await context.RequestAsync<VixFuturesEodDataReadModel[], GetVixFuturesEodDataQuery>(query);
		if (serviceResult.Success && serviceResult is not null)
			vixFutureEodData = serviceResult.Value!;
		return vixFutureEodData;
	}

	internal static async ValueTask<FuturesEodDataV2ReadModel[]> GetFuturesEodDataByDateRangeAsync(
		this IEventActorContext context,
		string contractId,
		DateOnly startDate,
		DateOnly endDate)
	{
		FuturesEodDataV2ReadModel[] futureEodDataRange = [];
		var entityId = new GetFuturesEodDataByDateRangeParameter(contractId, startDate, endDate);
		GetFuturesEodDataByDateRangeQuery query = new(contractId, startDate, endDate)
		{
			Subject = new(ActorType.Query, GetFuturesEodDataByDateRangeQuery.Actor, GetFuturesEodDataByDateRangeQuery.Verb, entityId.Format()),
			EntityId = entityId,
			ErrorCode = GetFuturesEodDataByDateRangeQuery.ErrorId
		};
		var serviceResult = await context.RequestAsync<FuturesEodDataV2ReadModel[], GetFuturesEodDataByDateRangeQuery>(query);
		if (serviceResult.Success && serviceResult.Value is not null)
			futureEodDataRange = serviceResult.Value;
		return futureEodDataRange;
	}

	internal static async ValueTask<NormalCurveTableReadModel?> GetNormalCurveTableAsync(this IEventActorContext context)
	{
		var normalCurveTable = default(NormalCurveTableReadModel);
		var entityId = new GetNormalCurveTableParameter();
		GetNormalCurveTableQuery query = new()
		{
			Subject = new(ActorType.Query, GetNormalCurveTableQuery.Actor, GetNormalCurveTableQuery.Verb, entityId.Format()),
			EntityId = entityId,
			ErrorCode = GetNormalCurveTableQuery.ErrorId
		};
		var serviceResult = await context.RequestAsync<NormalCurveTableReadModel, GetNormalCurveTableQuery>(query);
		if (serviceResult.Success && serviceResult.Value is not null)
			normalCurveTable = serviceResult.Value;
		return normalCurveTable;
	}

	internal static async ValueTask InsertFuturesEodDataAsync(
		this IActorMarketDataFeedCommandApi commandApi,
		DateOnly valueDate,
		FuturesTickDataV2ReadModel futuresTickData,
		FuturesContractV2ReadModel futuresContract,
		FuturesEodDataV2ReadModel eodDataToday,
		ICollection<FuturesEodDataV2ReadModel> eodDataRange,
		NormalCurveTableReadModel normCurveData,
		int windowSize,
		ICollection<VixFuturesEodDataReadModel> vixEodData)
	{
		_ = await commandApi.InsertFuturesEodDataAsync(
			valueDate,
			futuresTickData,
			futuresContract,
			eodDataToday,
			eodDataRange,
			normCurveData,
			windowSize,
			vixEodData);
	}

	internal static async ValueTask InsertVixFuturesEodDataAsync(this IActorMarketDataFeedCommandApi commandApi, FuturesTickDataV2ReadModel futuresTickData)
	{
		_ = await commandApi.InsertVixFuturesEodDataAsync(futuresTickData);
	}

	internal static async ValueTask InsertFuturesTickDataAsync(
		this IActorMarketDataFeedCommandApi commandApi,
		FuturesContractV2ReadModel futuresContract,
		FuturesTickDataV2ReadModel futuresTickData)
	{
		_ = await commandApi.InsertFuturesTickDataAsync(futuresContract, futuresTickData);
	}
}
