using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

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

