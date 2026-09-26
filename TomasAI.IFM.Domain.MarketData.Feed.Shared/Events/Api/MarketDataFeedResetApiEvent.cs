using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.Api;

public record MarketDataFeedResetApiEvent : ServiceApiEvent
{
    const int ErrorCode = 5012;

    public FuturesContractV3ReadModel[] FuturesContracts { get; init; }
    public DateOnly ValueDate { get; init; }
    public string ResetBy { get; init; }
    public DateTime ResetOn { get; init; }

    public ICompleteEvent ToCompletedEvent() => new MarketDataFeedResetCompleteApiEvent
    {
        CommandId = this.CommandId,
        FuturesContracts = this.FuturesContracts,
        ValueDate = this.ValueDate,
        ResetOn = this.ResetOn,
        ResetBy = this.ResetBy
    };

    public IErrorEvent ToFailedEvent(Exception ex) => new MarketDataFeedResetFailApiEvent
    {
        CommandId = this.CommandId,
        ErrorMessage = ex.Message,
        ErrorType = ErrorType.Command,
        ErrorCode = ErrorCode
    };
}

public record MarketDataFeedResetCompleteApiEvent : CompleteEvent
{
    public FuturesContractV3ReadModel[] FuturesContracts { get; init; }
    public DateOnly ValueDate { get; init; }
    public DateTime ResetOn { get; init; }
    public string ResetBy { get; init; }
}

public record MarketDataFeedResetFailApiEvent : ErrorEvent
{
}
