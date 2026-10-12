using MessagePack;

namespace TomasAI.IFM.Domain.Reference.Shared.ViewModels;

[MessagePackObject]
public sealed record ChangeTradeStrategyFamilyRequest
{
    [Key(0)] public Guid OperationId { get; init; }
    [Key(1)] public TradeStrategyFamilyReference Target { get; init; } = new(0, 0);
    [Key(2)] public CreateTradeStrategyFamilyRequest Definition { get; init; } = new();
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (OperationId == Guid.Empty || Target?.IsValid != true) errors.Add("An operation ID and exact family reference are required.");
        if (Definition is null) errors.Add("A definition is required.");
        else { errors.AddRange(Definition.Validate()); if (Definition.OperationId != OperationId) errors.Add("Definition OperationId must match the change."); }
        return errors;
    }
}
