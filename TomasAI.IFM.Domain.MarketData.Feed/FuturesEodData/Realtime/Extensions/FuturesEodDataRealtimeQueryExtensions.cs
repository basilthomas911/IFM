using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Extensions;

/// <summary>Current-session queries prefer the authoritative live cache; historical queries retain storage semantics.</summary>
internal static class FuturesEodDataRealtimeQueryExtensions
{
    internal static async ValueTask<FuturesEodDataV2ReadModel?> GetFuturesEodDataAsync(
        this IEventActorContext context,
        string contractId,
        DateOnly valueDate)
    {
        if (CurrentFuturesEodCache.Shared.TryGet(contractId, valueDate, out var current)) return current;
        var entityId = new GetFuturesEodDataParameter(contractId, valueDate);
        GetFuturesEodDataQuery query = new(contractId, valueDate)
        {
            Subject = new(
                ActorType.Query,
                GetFuturesEodDataQuery.Actor,
                GetFuturesEodDataQuery.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetFuturesEodDataQuery.ErrorId
        };
        var result = await context.RequestAsync<
            FuturesEodDataV2ReadModel,
            GetFuturesEodDataQuery>(query).ConfigureAwait(false);
        if (CurrentFuturesEodCache.Shared.TryGet(contractId, valueDate, out current)) return current;
        if (result is null || !result.Success)
            throw new InvalidOperationException(
                $"Futures EOD current-row query failed for {contractId}:{valueDate:yyyy-MM-dd}: "
                + (result?.ErrorMessage ?? "No response"));
        return result.Value;
    }

    internal static async ValueTask<FuturesEodDataV2ReadModel?> GetLastFuturesEodDataAsync(
        this IEventActorContext context,
        string contractId,
        DateOnly valueDate)
    {
        var entityId = new GetLastFuturesEodDataParameter(contractId, valueDate);
        GetLastFuturesEodDataQuery query = new(contractId, valueDate)
        {
            Subject = new(
                ActorType.Query,
                GetLastFuturesEodDataQuery.Actor,
                GetLastFuturesEodDataQuery.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetLastFuturesEodDataQuery.ErrorId
        };
        var result = await context.RequestAsync<
            FuturesEodDataV2ReadModel,
            GetLastFuturesEodDataQuery>(query).ConfigureAwait(false);
        if (result is null || !result.Success)
            throw new InvalidOperationException(
                $"Futures EOD last-row query failed for {contractId}:{valueDate:yyyy-MM-dd}: "
                + (result?.ErrorMessage ?? "No response"));
        return result.Value;
    }

    internal static async ValueTask<VixFuturesEodDataReadModel[]> GetVixFuturesEodDataAsync(
        this IEventActorContext context,
        string contractId,
        DateOnly valueDate)
    {
        if (CurrentVixEodCache.Shared.TryGet(contractId, valueDate, out var current)) return [current!];
        var entityId = new GetVixFuturesEodDataParameter(contractId, valueDate);
        GetVixFuturesEodDataQuery query = new(contractId, valueDate)
        {
            Subject = new(
                ActorType.Query,
                GetVixFuturesEodDataQuery.Actor,
                GetVixFuturesEodDataQuery.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetVixFuturesEodDataQuery.ErrorId
        };
        var result = await context.RequestAsync<
            VixFuturesEodDataReadModel[],
            GetVixFuturesEodDataQuery>(query).ConfigureAwait(false);
        if (CurrentVixEodCache.Shared.TryGet(contractId, valueDate, out current)) return [current!];
        return result?.Success == true && result.Value is not null ? result.Value : [];
    }

    internal static async ValueTask<FuturesEodDataV2ReadModel[]> GetFuturesEodDataByDateRangeAsync(
        this IEventActorContext context,
        string contractId,
        DateOnly startDate,
        DateOnly endDate)
    {
        var entityId = new GetFuturesEodDataByDateRangeParameter(
            contractId,
            startDate,
            endDate);
        GetFuturesEodDataByDateRangeQuery query = new(contractId, startDate, endDate)
        {
            Subject = new(
                ActorType.Query,
                GetFuturesEodDataByDateRangeQuery.Actor,
                GetFuturesEodDataByDateRangeQuery.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetFuturesEodDataByDateRangeQuery.ErrorId
        };
        var result = await context.RequestAsync<
            FuturesEodDataV2ReadModel[],
            GetFuturesEodDataByDateRangeQuery>(query).ConfigureAwait(false);
        return result?.Success == true && result.Value is not null ? result.Value : [];
    }

    internal static async ValueTask<NormalCurveTableReadModel?> GetNormalCurveTableAsync(
        this IEventActorContext context)
    {
        var entityId = new GetNormalCurveTableParameter();
        GetNormalCurveTableQuery query = new()
        {
            Subject = new(
                ActorType.Query,
                GetNormalCurveTableQuery.Actor,
                GetNormalCurveTableQuery.Verb,
                entityId.Format()),
            EntityId = entityId,
            ErrorCode = GetNormalCurveTableQuery.ErrorId
        };
        var result = await context.RequestAsync<
            NormalCurveTableReadModel,
            GetNormalCurveTableQuery>(query).ConfigureAwait(false);
        return result?.Success == true ? result.Value : null;
    }
}
