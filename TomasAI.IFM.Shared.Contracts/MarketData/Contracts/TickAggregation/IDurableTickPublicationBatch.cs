using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
/// <summary>One ordered finite source group: frozen ITI preparation, confirmed capture, then ordered release.</summary>
public interface IDurableTickPublicationBatch
{
    ValueTask PublishBatchAsync(IReadOnlyList<IEvent> sources,bool recovered,CancellationToken token);
}
