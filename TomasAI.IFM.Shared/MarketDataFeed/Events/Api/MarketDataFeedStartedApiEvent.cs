using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Events.Api;

public record MarketDataFeedStartedApiEvent : ServiceApiEvent
{
    public const int ErrorCode = 5010;

    public FuturesContractV2ReadModel[] FuturesContracts { get; init; }
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

    public IErrorEvent ToFailedEvent(Exception ex) => new MarketDataFeedStartedFailApiEvent
    {
        CommandId = this.CommandId,
        ErrorMessage = ex.Message,
        ErrorType = ErrorType.Command,
        ErrorCode = ErrorCode
    };

   
}

public record MarketDataFeedStartedCompleteApiEvent : CompleteEvent
{
    public FuturesContractV2ReadModel[] FuturesContracts { get; init; }
    public DateOnly ValueDate { get; init; }
    public bool ResetStream { get; init; }
    public DateTime StartedOn { get; init; }
    public string StartedBy { get; init; }
}

public record MarketDataFeedStartedFailApiEvent : ErrorEvent
{
}
