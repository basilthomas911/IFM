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

    /// <summary>Mutates terminal outcome data only from a hash-verified source event.</summary>
    /// <param name="domainEvent">The source event to apply or reconstruct.</param>
    /// <returns>True when the terminal evidence is accepted.</returns>
    /// <exception cref="InvalidOperationException">The source hash is corrupt or conflicts with committed evidence.</exception>
    protected override bool Apply(IEvent domainEvent)
    {
        switch (domainEvent)
        {
            case MarketDataDownloadLogInsertedEvent inserted:
                var command = new InsertMarketDataDownloadLogCommand(inserted.Outcome);
                if (inserted.PayloadSha256 != command.PayloadSha256) throw new InvalidOperationException("Corrupt DownloadLog event hash.");
                if (VerifyDuplicate(command)) return true;
                Outcome = inserted.Outcome;
                PayloadSha256 = inserted.PayloadSha256;
                return true;
            default:
                return false;
        }
    }
}
