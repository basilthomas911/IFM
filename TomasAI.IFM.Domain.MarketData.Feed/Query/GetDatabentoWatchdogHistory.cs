using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Model;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;
/// <summary>Handles <see cref="GetDatabentoWatchdogHistoryQuery"/>.</summary>
public static class GetDatabentoWatchdogHistory
{
    /// <summary>Returns filtered Databento watchdog observations.</summary>
    public static async ValueTask ExecuteAsync(this GetDatabentoWatchdogHistoryQuery query, IMarketDataFeedQueryContext context, MarketDataFeedQueryParameters parameters)
    {
        var parameter = query.EntityId as TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters.GetDatabentoWatchdogHistoryParameter
            ?? throw new InvalidOperationException("Databento watchdog history parameters are invalid.");
        DatabentoMajorStatus? status = string.IsNullOrWhiteSpace(parameter.MajorStatus)
            ? null
            : Enum.Parse<DatabentoMajorStatus>(parameter.MajorStatus, true);
        var values = await parameters.MarketDataServiceStore.ListObservationsAsync(parameter.ValueDate, status, parameter.PageSize);
        await context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<DatabentoWatchdogObservationReadModel[]>([.. values.Select(DatabentoQueryModel.MapObservation)]));
    }
}