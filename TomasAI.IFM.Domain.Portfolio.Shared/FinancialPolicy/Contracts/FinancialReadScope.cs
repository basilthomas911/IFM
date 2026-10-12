using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject]
public sealed record FinancialReadScope
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int? FundId { get; init; }
    [Key(2)] public FinancialAccess Access { get; init; } = new(string.Empty, []);
}
