using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.Queries;
using TomasAI.IFM.Shared.MarketDataFeed.QueryParameters;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.Trade.Events;

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
		this IEventActorContext context,
		DateOnly valueDate,
		FuturesTickDataV2ReadModel futuresTickData,
		FuturesContractV2ReadModel futuresContract,
		FuturesEodDataV2ReadModel eodDataToday,
		ICollection<FuturesEodDataV2ReadModel> eodDataRange,
		NormalCurveTableReadModel normCurveData,
		int windowSize,
		ICollection<VixFuturesEodDataReadModel> vixEodData)
	{
		var entityId = new FuturesEodDataId(futuresContract.ContractId, valueDate);
		InsertFuturesEodDataCommand cmd = new(valueDate, futuresTickData, futuresContract, eodDataToday, eodDataRange, normCurveData, windowSize, vixEodData)
		{
			Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
			EntityId = entityId,
			ErrorCode = InsertFuturesOptionTickDataCommand.ErrorId
		};
		var serviceResult = await context.RequestAsync<InsertFuturesEodDataCommand, FuturesEodDataId>(cmd);
		if (serviceResult?.Success != true)
			throw new InvalidOperationException(serviceResult?.ErrorMessage);
	}

	internal static async ValueTask InsertVixFuturesEodDataAsync(this IEventActorContext context, FuturesTickDataV2ReadModel futuresTickData)
	{
		var entityId = new FuturesEodDataId(futuresTickData.ContractId, futuresTickData.ValueDate);
		InsertVixFuturesEodDataCommand cmd = new(futuresTickData)
		{
			Subject = new ActorSubject(ActorType.Command, InsertVixFuturesEodDataCommand.Actor, InsertVixFuturesEodDataCommand.Verb, entityId.Format()),
			EntityId = entityId,
			ErrorCode = InsertVixFuturesEodDataCommand.ErrorId
		};
		var serviceResult = await context.RequestAsync<InsertVixFuturesEodDataCommand, FuturesEodDataId>(cmd);
		if (serviceResult?.Success != true)
			throw new InvalidOperationException(serviceResult?.ErrorMessage);
	}

	internal static async ValueTask SendFuturesOptionTickDataUpdatedEventAsync(this IEventActorContext context, FuturesOptionTickDataInsertedEvent e)
	{
		var entityId = new FuturesOptionTickEntityId(e.TickData.ContractId, e.TickData.ValueDate);
		OptionTradeTickPriceDataUpdatedEvent updatedEvent = new(e.TickData)
		{
			Subject = new ActorSubject(ActorType.Event, OptionTradeTickPriceDataUpdatedEvent.Actor, OptionTradeTickPriceDataUpdatedEvent.Verb, entityId.Format()),
			EntityId = entityId,
			Id = Guid.NewGuid(),
			CommandId = e.CommandId,
			AggregateId = e.AggregateId,
			ReceivedOn = DateTime.UtcNow
		};
		await context.SendAsync<OptionTradeTickPriceDataUpdatedEvent, FuturesOptionTickEntityId>(updatedEvent);
	}

	internal static async ValueTask FuturesTickDataStreamingStartedCompleteAsync(this IEventActorContext context, FuturesTickDataStreamingStartedEvent e)
	{
		var completeEvent = e.ToCompleteEvent<FuturesTickDataStreamingStartedCompleteEvent, FuturesTickDataStreamingId>() as FuturesTickDataStreamingStartedCompleteEvent;
		await context.SendAsync<FuturesTickDataStreamingStartedCompleteEvent, FuturesTickDataStreamingId>(completeEvent!);
	}

	internal static async ValueTask FuturesTickDataStreamingStartedFailAsync(this IEventActorContext context, FuturesTickDataStreamingStartedEvent e, Exception ex)
	{
		var completeEvent = e.ToFailEvent<FuturesTickDataStreamingStartedFailEvent, FuturesTickDataStreamingId>(ex) as FuturesTickDataStreamingStartedFailEvent;
		await context.SendAsync<FuturesTickDataStreamingStartedFailEvent, FuturesTickDataStreamingId>(completeEvent!);
	}

	internal static async ValueTask FuturesTickDataStreamingStoppedCompleteAsync(this IEventActorContext context, FuturesTickDataStreamingStoppedEvent e)
	{
		var completeEvent = e.ToCompleteEvent<FuturesTickDataStreamingStoppedCompleteEvent, FuturesTickDataStreamingId>() as FuturesTickDataStreamingStoppedCompleteEvent;
		await context.SendAsync<FuturesTickDataStreamingStoppedCompleteEvent, FuturesTickDataStreamingId>(completeEvent!);
	}

	internal static async ValueTask FuturesTickDataStreamingStoppedFailAsync(this IEventActorContext context, FuturesTickDataStreamingStoppedEvent e, Exception ex)
	{
		var completeEvent = e.ToFailEvent<FuturesTickDataStreamingStoppedFailEvent, FuturesTickDataStreamingId>(ex) as FuturesTickDataStreamingStoppedFailEvent;
		await context.SendAsync<FuturesTickDataStreamingStoppedFailEvent, FuturesTickDataStreamingId>(completeEvent!);
	}

	internal static async ValueTask InsertFuturesTickDataAsync(
		this IEventActorContext context,
		FuturesContractV2ReadModel futuresContract,
		FuturesTickDataV2ReadModel futuresTickData)
	{
		var entityId = new FuturesDataId(futuresContract.ContractId, futuresTickData.ValueDate);
		InsertFuturesTickDataCommand cmd = new(futuresContract, futuresTickData)
		{
			Subject = new ActorSubject(ActorType.Command, InsertFuturesTickDataCommand.Actor, InsertFuturesTickDataCommand.Verb, entityId.Format()),
			EntityId = entityId,
			ErrorCode = InsertFuturesTickDataCommand.ErrorId
		};
		var serviceResult = await context.RequestAsync<InsertFuturesTickDataCommand, FuturesDataId>(cmd);
		if (serviceResult?.Success != true)
			throw new InvalidOperationException(serviceResult?.ErrorMessage);
	}
}
