using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.Api;

public record MarketDataFeedResetApiEvent : ServiceApiEvent
{
    const int ErrorCode = 5012;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
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

    public IErrorEvent ToFailedEvent(Exception ex) 
    {
        ArgumentNullException.ThrowIfNull(ex);
        return new MarketDataFeedResetFailApiEvent
    {
        CommandId = this.CommandId,
        ErrorMessage = ex.Message,
        ErrorType = ErrorType.Command,
        ErrorCode = ErrorCode
    };
    }
}
