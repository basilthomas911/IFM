using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Persistence;

public sealed record PortfolioEventMetadata(Guid CorrelationId, Guid CausationId, DateTime OriginatedOnUtc)
{
    public static PortfolioEventMetadata ForCommand(Guid commandId, Guid eventId, DateTime occurredOnUtc) =>
        new(commandId, eventId, occurredOnUtc);

    public void Validate()
    {
        if (CorrelationId == Guid.Empty) throw new InvalidOperationException("CorrelationId is required.");
        if (CausationId == Guid.Empty) throw new InvalidOperationException("CausationId is required.");
        if (OriginatedOnUtc.Kind != DateTimeKind.Utc) throw new InvalidOperationException("OriginatedOnUtc must be UTC.");
    }
}
