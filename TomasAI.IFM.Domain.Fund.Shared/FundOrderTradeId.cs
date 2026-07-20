using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Fund.Shared;

/// <summary>
/// Represents the unique identifier of a trade within a specific fund order.
/// </summary>
/// <remarks>
/// This type is MessagePack-serializable using stable numeric keys for compact and version-tolerant payloads.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderTradeId : IActorEntityId
{
    /// <summary>
    /// Gets the fund identifier.
    /// </summary>
    [Key(0)]
    public int FundId { get; init; }

    /// <summary>
    /// Gets the order identifier within the fund.
    /// </summary>
    [Key(1)]
    public int OrderId { get; init; }

    /// <summary>
    /// Gets the trade identifier within the order.
    /// </summary>
    [Key(2)]
    public int TradeId { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FundOrderTradeId"/> record.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    public FundOrderTradeId(int fundId, int orderId, int tradeId)
    {
        FundId = fundId;
        OrderId = orderId;
        TradeId = tradeId;
    }

    /// <summary>
    /// Factory method to create a new <see cref="FundOrderTradeId"/>.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="orderId">The order identifier.</param>
    /// <param name="tradeId">The trade identifier.</param>
    /// <returns>A new <see cref="FundOrderTradeId"/> instance.</returns>
    public static FundOrderTradeId Create(int fundId, int orderId, int tradeId)
        => new(fundId, orderId, tradeId);

    /// <summary>
    /// Returns a JSON string representation of this identifier.
    /// </summary>
    public override string ToString() 
        => JsonConvert.SerializeObject(this, Formatting.None);

    /// <summary>
    /// Formats and returns a string representation of the current object, combining the Fund ID, Order ID, and Trade
    /// ID.
    /// </summary>
    /// <returns>A string in the format "FundId.OrderId.TradeId" representing the combined identifiers.</returns>
    public string Format()
        => string.Create(null, stackalloc char[64], $"{FundId}.{OrderId}.{TradeId}");
}

public static class FundOrderTradeIdValidationExtension
{
    /// <summary>
    /// Validates the properties of a <see cref="FundOrderTradeId"/> instance and adds any validation errors to the
    /// provided list.
    /// </summary>
    /// <remarks>This method checks that the <c>FundId</c>, <c>OrderId</c>, and <c>TradeId</c> properties of
    /// the <paramref name="fundOrderTradeId"/> are greater than 0. If any of these properties are invalid, a
    /// corresponding <see cref="ValidationError"/> is added to the <paramref name="validationErrors"/> list.</remarks>
    /// <param name="validationErrors">A list of <see cref="ValidationError"/> objects to which any validation errors will be added.</param>
    /// <param name="fundOrderTradeId">The <see cref="FundOrderTradeId"/> instance to validate. Its <c>FundId</c>, <c>OrderId</c>, and <c>TradeId</c>
    /// properties must all be greater than 0.</param>
    /// <param name="commandName">The name of the command being validated, used to prefix error messages for context.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any new validation errors found.</returns>
    public static List<ValidationError> ValidateFundOrderTradeId(this List<ValidationError> validationErrors, FundOrderTradeId fundOrderTradeId, string commandName)
    {
        if (fundOrderTradeId.FundId < 1)
            validationErrors.Add(new($"{commandName}.FundId must be > 0"));
        if (fundOrderTradeId.OrderId < 1)
            validationErrors.Add(new($"{commandName}.OrderId must be > 0"));
        if (fundOrderTradeId.TradeId < 1)
            validationErrors.Add(new($"{commandName}.TradeId must be > 0"));
        return validationErrors;
    }

}