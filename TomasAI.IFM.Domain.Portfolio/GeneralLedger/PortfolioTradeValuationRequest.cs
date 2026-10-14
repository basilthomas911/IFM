using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger;

/// <summary>Describes an absolute end-of-day valuation observed for one canonical Portfolio trade.</summary>
public sealed record PortfolioTradeValuationRequest(int FundId, int OrderId, int TradeId, DateOnly ValueDate,
    decimal AbsoluteUnrealizedPnl, string Description, Guid SourceEventId, long SourceSequence, DateTime ObservedAtUtc);
