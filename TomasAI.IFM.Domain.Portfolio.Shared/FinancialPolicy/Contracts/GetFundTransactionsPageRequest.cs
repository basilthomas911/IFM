using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject] public sealed record GetFundTransactionsPageRequest([property: Key(0)] int PageSize, [property: Key(1)] FinancialPageCursor? Cursor = null);
