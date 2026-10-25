using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record FinancialPageCursor([property: Key(0)] int PortfolioId, [property: Key(1)] int FundId,
    [property: Key(2)] long AsOfRevision, [property: Key(3)] long AfterRevision, [property: Key(4)] int AfterOrdinal,
    [property: Key(5)] string Kind);
