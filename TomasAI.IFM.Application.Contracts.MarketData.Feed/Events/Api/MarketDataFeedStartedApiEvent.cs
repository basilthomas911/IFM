using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.Api;

public record MarketDataFeedStartedApiEvent : ServiceApiEvent
{
    public const int ErrorCode = 5010;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
    public FuturesContractV3ReadModel[] FuturesContracts { get; init; }
    public DateOnly ValueDate { get; init; }
    public bool ResetStream { get; init; }
    public DateTime StartedOn { get; init; }
    public string StartedBy { get; init; }

    public ICompleteEvent ToCompletedEvent() => new MarketDataFeedStartedCompleteApiEvent
    {
        CommandId = this.CommandId,
        FuturesContracts = this.FuturesContracts,
        ValueDate = this.ValueDate,
        ResetStream = this.ResetStream,
        StartedOn = this.StartedOn,
        StartedBy = this.StartedBy
    };

    public IErrorEvent ToFailedEvent(Exception ex) 
    {
        ArgumentNullException.ThrowIfNull(ex);
        return new MarketDataFeedStartedFailApiEvent
    {
        CommandId = this.CommandId,
        ErrorMessage = ex.Message,
        ErrorType = ErrorType.Command,
        ErrorCode = ErrorCode
    };
    }


}
