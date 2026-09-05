using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

/// <summary>A persistent product identity, never an expiring provider instrument ID.</summary>
[MessagePackObject]
public sealed record TradeStrategySymbolReadModel
{
    [Key(0)] public int Id { get; init; }
    [Key(1)] public string Symbol { get; init; } = string.Empty;
    [Key(2)] public string Currency { get; init; } = string.Empty;
    [Key(3)] public string Exchange { get; init; } = string.Empty;
    [Key(4)] public string Description { get; init; } = string.Empty;

    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];
        if (Id <= 0) errors.Add("A positive product ID is required.");
        if (string.IsNullOrWhiteSpace(Symbol) || Symbol != Symbol.Trim()) errors.Add("Symbol is required and must be trimmed.");
        if (Currency is null || Currency.Length != 3 || Currency.Any(c => c is < 'A' or > 'Z')) errors.Add("Currency must be three uppercase letters.");
        if (string.IsNullOrWhiteSpace(Exchange) || Exchange != Exchange.Trim()) errors.Add("Exchange is required and must be trimmed.");
        if (string.IsNullOrWhiteSpace(Description)) errors.Add("Description is required.");
        return errors;
    }
}
