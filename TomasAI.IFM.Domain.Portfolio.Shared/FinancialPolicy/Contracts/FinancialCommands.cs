using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

/// <summary>Principal asserted through the authenticated NATS subject ACL boundary; domain permissions remain explicit.</summary>
[MessagePackObject]
public sealed record FinancialAccess([property: Key(0)] string Principal, [property: Key(1)] string[] Roles,
    [property: Key(2)] int[]? PortfolioIds=null);

public interface IFinancialRequest : ICommand
{
    bool PostEvents { get; }
    int SchemaVersion { get; }
    Guid OperationId { get; }
    int PortfolioId { get; }
    Guid CorrelationId { get; }
    Guid CausationId { get; }
    DateTime RequestedAtUtc { get; }
    DateTime ExpiresAtUtc { get; }
    long ExpectedFinancialRevision { get; }
    string InputSha256 { get; }
    FinancialAccess Access { get; }
}
public interface IFinancialRequest<out TBody> : IFinancialRequest { TBody Body { get; } }

