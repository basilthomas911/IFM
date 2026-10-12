using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

namespace TomasAI.IFM.Application.MarketData.Subscriptions;

/// <summary>Worker acknowledgement of one complete committed intent revision, including all selected legs.</summary>
public sealed record DurableRealization(long Revision, Guid GenerationId, bool AllRoutesReady);
