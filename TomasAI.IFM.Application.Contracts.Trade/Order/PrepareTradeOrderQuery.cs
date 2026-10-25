using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Order;

/// <summary>Operator intent only. Derived economics on Draft are ignored by preparation.</summary>
[MessagePackObject]
public sealed record TradeOrderPreparationRequest(
    [property: Key(0)] int PortfolioId, [property: Key(1)] string UnderlyingContractId,
    [property: Key(2)] int Quantity, [property: Key(3)] PortfolioOrderCandidate Draft,
    [property: Key(4)] string SelectedQuoteInputs = "");

[MessagePackObject]
public sealed record TradeOrderPreparation(
    [property: Key(0)] PortfolioOrderCandidate Candidate,
    [property: Key(1)] decimal CashAmount, [property: Key(2)] decimal TotalCommission,
    [property: Key(3)] decimal TotalAmount, [property: Key(4)] decimal[] LegCommissions,
    [property: Key(5)] string FeeBasis, [property: Key(6)] DateTime PreparedAtUtc);

[MessagePackObject(AllowPrivate = true)]
public sealed record PrepareTradeOrderQuery : IQuery<TradeOrderPreparation>
{
    public const string Actor = TradeOrderActorNames.Query;
    public const string Verb = "PrepareTradeOrder";
    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; } = ActorEntityId.Default;
    [Key(2)] public TradeOrderPreparationRequest Request { get; init; } = null!;
    [IgnoreMember] public int ErrorCode => 25213;
    [IgnoreMember] public string? QueryParams => null;
}
