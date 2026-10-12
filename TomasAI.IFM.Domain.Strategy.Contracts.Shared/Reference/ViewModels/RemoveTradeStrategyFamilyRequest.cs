using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

[MessagePackObject]
public sealed record RemoveTradeStrategyFamilyRequest
{
    [Key(0)] public Guid OperationId { get; init; }
    [Key(1)] public TradeStrategyFamilyReference Target { get; init; } = new(0, 0);
    public IReadOnlyList<string> Validate() => OperationId == Guid.Empty || Target?.IsValid != true
        ? ["An operation ID and exact family reference are required."] : [];
}
