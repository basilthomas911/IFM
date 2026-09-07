using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog.Command.State;

public sealed class DownloadLogCommandState : BaseEventSourceActorState<DownloadLogCommandState>, IEventSourceActorState<DownloadLogCommandState>
{
    public override ActorThreadId Id { get; set; } = default!;
    public MarketDataDownloadOutcome? Outcome { get; private set; }
    public string? PayloadSha256 { get; private set; }

    public bool VerifyDuplicate(InsertMarketDataDownloadLogCommand command)
    {
        command.Validate();
        if (Outcome is null) return false;
        if (PayloadSha256 != command.PayloadSha256 || Outcome != command.Outcome)
            throw new InvalidOperationException("A different terminal outcome is already committed for this import attempt.");
        return true;
    }

    protected override bool Apply(IEvent domainEvent)
    {
        if (domainEvent is not MarketDataDownloadLogInsertedEvent inserted) return false;
        var command = new InsertMarketDataDownloadLogCommand(inserted.Outcome);
        if (inserted.PayloadSha256 != command.PayloadSha256) throw new InvalidOperationException("Corrupt DownloadLog event hash.");
        if (VerifyDuplicate(command)) return true;
        Outcome = inserted.Outcome;
        PayloadSha256 = inserted.PayloadSha256;
        return true;
    }
}
