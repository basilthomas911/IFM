using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;

[MessagePackObject]
public sealed record PortfolioAggregateRevision
{
    [Key(0)] public int PortfolioId { get; init; }
    [Key(1)] public int? FundId { get; init; }
    [Key(2)] public long Revision { get; init; }
    [Key(3)] public long SourceEventId { get; init; }
}
