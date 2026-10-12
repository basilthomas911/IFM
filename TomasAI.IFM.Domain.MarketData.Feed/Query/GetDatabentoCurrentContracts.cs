using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.Query.Model;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Query;
/// <summary>Handles <see cref="GetDatabentoCurrentContractsQuery"/>.</summary>
public static class GetDatabentoCurrentContracts
{
    /// <summary>Returns all current Databento contract assignments.</summary>
    public static async ValueTask ExecuteAsync(this GetDatabentoCurrentContractsQuery query, IMarketDataFeedQueryContext context, MarketDataFeedQueryParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(query);
        var values = await (parameters.MarketDataServiceStore.ListAssignmentsAsync()).ConfigureAwait(false);
        await (context.ReplyAsync(query.Subject.ThreadId, query.Subject.Verb,
            new ServiceResult<DatabentoContractAssignmentReadModel[]>([.. values.Select(DatabentoQueryModel.MapAssignment)]))).ConfigureAwait(false);
    }
}