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

    extension(GetDatabentoWatchdogHistoryQuery query)
    {
        /// <summary>Returns filtered Databento watchdog observations.</summary>
        public async ValueTask<ServiceResult<DatabentoWatchdogObservationReadModel[]>> ExecuteAsync(IMarketDataFeedQueryContext context, MarketDataFeedQueryParameters parameters)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentNullException.ThrowIfNull(query);
            var parameter = query.EntityId as TomasAI.IFM.Domain.MarketData.Feed.Shared.QueryParameters.GetDatabentoWatchdogHistoryParameter
                ?? throw new InvalidOperationException("Databento watchdog history parameters are invalid.");
            DatabentoMajorStatus? status = string.IsNullOrWhiteSpace(parameter.MajorStatus)
                ? null
                : Enum.Parse<DatabentoMajorStatus>(parameter.MajorStatus, true);
            var values = await (parameters.MarketDataServiceStore.ListObservationsAsync(parameter.ValueDate, status, parameter.PageSize)).ConfigureAwait(false);
            return new ServiceResult<DatabentoWatchdogObservationReadModel[]>([.. values.Select(DatabentoQueryModel.MapObservation)]);
        }
    }
}