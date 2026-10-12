using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Principal asserted through the authenticated NATS subject ACL boundary; domain permissions remain explicit.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Preserve the existing serialized array payload and public financial model contract.")]
[MessagePackObject]
public sealed record FinancialAccess([property: Key(0)] string Principal, [property: Key(1)] string[] Roles,
    [property: Key(2)] int[]? PortfolioIds = null);

