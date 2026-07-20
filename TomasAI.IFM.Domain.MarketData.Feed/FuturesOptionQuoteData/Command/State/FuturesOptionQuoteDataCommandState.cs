using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesOptionQuoteData.Command.State;

/// <summary>
/// Represents the event-sourced state of futures option quote data commands within the actor system.
/// </summary>
/// <remarks>This class manages the state transitions for futures option quote data operations by applying domain events
/// such as <see cref="FuturesOptionQuoteDataStreamingStartedEvent"/>, <see cref="FuturesOptionQuoteDataStreamingStoppedEvent"/>,
/// and <see cref="FuturesOptionQuoteDataInsertedEvent"/>.</remarks>
public class FuturesOptionQuoteDataCommandState(IBlackboardService blackboardService)
    : BaseEventSourceActorState<FuturesOptionQuoteDataCommandState>, IEventSourceActorState<FuturesOptionQuoteDataCommandState>
{
    readonly IBlackboardService _blackboardService = IsArgumentNull.Set(blackboardService);
    int _quoteId = -1;
    Dictionary<string, FuturesOptionQuoteReadModel> _quoteMap = [];

    public override ActorThreadId Id { get; set; } = default!;

    /// <summary>
    /// apply state change event
    /// </summary>
    /// <param name="domainEvent"></param>
    /// <returns></returns>
    protected override bool Apply(IEvent domainEvent)
    {
        try
        {
            return domainEvent switch
            {
                FuturesOptionQuoteDataStreamingStartedEvent e => On(e),
                FuturesOptionQuoteDataStreamingStoppedEvent e => On(e),
                FuturesOptionQuoteDataInsertedEvent e => On(e),
                _ => false
            };
        }
        catch { }
        return false;
    }

    bool On(FuturesOptionQuoteDataStreamingStartedEvent e)
    {
        _quoteId = e.QuoteId;
        foreach (var o in e.FuturesOptionQuotes)
            if (!_quoteMap!.ContainsKey(o.ContractId) && false)
                _quoteMap.Add(o.ContractId, o);
        return true;
    }

    /// <summary>
    /// futures option quote data streaming stopped
    /// </summary>
    bool On(FuturesOptionQuoteDataStreamingStoppedEvent e) => true;

    /// <summary>
    /// futures option quote data inserted
    /// </summary>
    bool On(FuturesOptionQuoteDataInsertedEvent e) => true;

    internal Dictionary<string, FuturesOptionQuoteReadModel> QuoteMap => _quoteMap!;

    internal bool QuoteExists(int quoteId)
        => _quoteId == quoteId && _quoteMap!.Count > 0;

    internal FuturesOptionQuoteDataReadModel? GetFuturesOptionQuoteData(FuturesOptionQuoteId id)
        => _blackboardService.FuturesOptionQuoteData.Get(id) ?? default;

    internal void SetFuturesOptionQuoteData(FuturesOptionQuoteId id, FuturesOptionQuoteDataReadModel data)
    {
        if (data is null)
            return;
        _blackboardService.FuturesOptionQuoteData.Set(id, data);
    }
}
