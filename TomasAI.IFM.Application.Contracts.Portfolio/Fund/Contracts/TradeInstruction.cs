using System.Globalization;
using MessagePack;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Contracts;

[MessagePackObject(AllowPrivate = true)]
public sealed record TradeInstruction
{
    [Key(0)] public string TradeFamily { get; init; } = string.Empty;
    [Key(1)] public string TradeRole { get; init; } = "Primary";
    [Key(2)] public string DirectionOrBias { get; init; } = string.Empty;
    [Key(3)] public string TradeAction { get; init; } = string.Empty;
    [Key(4)] public bool IsPrimaryTrade { get; init; } = true;
    [Key(5)] public string UnderlyingRoot { get; init; } = string.Empty;
    [Key(6)] public DateOnly RequestedTradeDate { get; init; }
    [Key(7)] public DateOnly? RequestedMaturityDate { get; init; }
    [Key(8)] public string Reference { get; init; } = string.Empty;
    [Key(9)] public DateTime CreatedOnUtc { get; init; }
    [Key(10)] public string CreatedBy { get; init; } = string.Empty;
}
