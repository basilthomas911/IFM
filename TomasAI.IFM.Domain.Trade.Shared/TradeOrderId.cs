using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Shared;

/// <summary>Identifies one broker-neutral order within its Portfolio and Fund authority.</summary>
[MessagePackObject]
public readonly record struct TradeOrderId(
    [property: Key(0)] int PortfolioId,
    [property: Key(1)] int FundId,
    [property: Key(2)] int OrderId) : IActorEntityId
{
    public string Format() => string.Create(CultureInfo.InvariantCulture,
        $"{PortfolioId}.{FundId}.{OrderId}");

    [IgnoreMember] public bool IsValid => PortfolioId > 0 && FundId > 0 && OrderId > 0;
}

/// <summary>Intrinsic TradeOrderId validation used by command actors.</summary>
public static class TradeOrderIdValidationExtensions
{
    /// <summary>Checks the intrinsic business identity without accessing state.</summary>
    public static List<TomasAI.IFM.Shared.Validation.ValidationError> ValidateTradeOrderId(this List<TomasAI.IFM.Shared.Validation.ValidationError> errors, TradeOrderId entityId, string commandName)
    {
        if (!entityId.IsValid) errors.Add(new($"{commandName}.EntityId is invalid."));
        return errors;
    }

}
