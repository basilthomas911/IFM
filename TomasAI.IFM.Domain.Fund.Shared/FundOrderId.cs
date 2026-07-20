using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Fund.Shared;

/// <summary>
/// Represents the unique identifier of a fund order.
/// </summary>
/// <remarks>
/// MessagePack-serializable with stable numeric keys. Implements <see cref="IActorEntityId"/> and formats as
/// a dot-separated string: <c>FundId.OrderId</c>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundOrderId : IActorEntityId
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
    /// Initializes a new instance of the <see cref="FundOrderId"/> record.
    /// </summary>
    /// <param name="fundId">The fund identifier.</param>
    /// <param name="orderId">The order identifier.</param>
    public FundOrderId(int fundId, int orderId)
    {
        FundId = fundId;
        OrderId = orderId;
    }

    /// <summary>
    /// Creates a new <see cref="FundOrderId"/> instance.
    /// </summary>
    public static FundOrderId Create(int fundId, int orderId) => new(fundId, orderId);

    /// <summary>
    /// Formats the identifier as a dot-separated string: <c>FundId.OrderId</c>.
    /// </summary>
    public string Format() => string.Create(null, stackalloc char[24], $"{FundId}.{OrderId}");

    /// <summary>
    /// Returns a JSON string representation of this identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.None);
}

public static class FundOrderIdValidationExtension
{
    /// <summary>
    /// Validates the <see cref="FundOrderId"/> object and adds any validation errors to the provided list.
    /// </summary>
    /// <remarks>This method checks that the <c>FundId</c> and <c>OrderId</c> properties of the <paramref
    /// name="fundOrderId"/> are greater than 0. If either property is invalid, a corresponding <see
    /// cref="ValidationError"/> is added to the <paramref name="validationErrors"/> list with a message prefixed by the
    /// <paramref name="commandName"/>.</remarks>
    /// <param name="validationErrors">A list of <see cref="ValidationError"/> objects to which any validation errors will be added.</param>
    /// <param name="fundOrderId">The <see cref="FundOrderId"/> object to validate. The <c>FundId</c> and <c>OrderId</c> properties must be
    /// greater than 0.</param>
    /// <param name="commandName">The name of the command being validated, used to prefix error messages for context.</param>
    /// <returns>The updated list of <see cref="ValidationError"/> objects, including any new validation errors found.</returns>
    public static List<ValidationError> ValidateFundOrderId(this List<ValidationError> validationErrors, FundOrderId fundOrderId, string commandName)
    {
        if (fundOrderId.FundId < 1)
            validationErrors.Add(new($"{commandName}.FundId must be > 0"));
        if (fundOrderId.OrderId < 1)
            validationErrors.Add(new($"{commandName}.OrderId must be > 0"));
        return validationErrors;
    }
}