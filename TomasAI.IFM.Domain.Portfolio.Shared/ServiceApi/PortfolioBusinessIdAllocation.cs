using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;

[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioBusinessIdAllocation
{
    [Key(0)] public PortfolioBusinessIdentityKind Kind { get; init; }
    [Key(1)] public int Value { get; init; }
    [Key(2)] public Guid CorrelationId { get; init; }
}
