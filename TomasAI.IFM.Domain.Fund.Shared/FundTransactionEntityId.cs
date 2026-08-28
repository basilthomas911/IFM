using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Fund.Shared;

/// <summary>
/// MessagePack-serializable identifier for a fund transaction, composed of FundId, OrderId, and ValueDate.
/// </summary>
/// <remarks>
/// Implements <see cref="IActorEntityId"/>. The formatted key uses dot notation:
/// "FundId.OrderId.ValueDate" where ValueDate is yyyyMMdd.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record FundTransactionEntityId(
    /// <summary>The unique identifier of the fund.</summary>
    [property: Key(0)] int FundId,
    /// <summary>The related order identifier.</summary>
    [property: Key(1)] int OrderId) : IActorEntityId
{
    /// <summary>
    /// Parameterless constructor for serializers; initializes to defaults.
    /// </summary>
    public FundTransactionEntityId() : this(0, 0) { }

    /// <summary>
    /// Formats the identifier as a dot-separated string: "FundId.OrderId.ValueDate".
    /// </summary>
    public string Format()
        => $"{FundId}.{OrderId}";

    /// <summary>
    /// Returns a compact JSON representation of the identifier.
    /// </summary>
    public override string ToString() => JsonConvert.SerializeObject(this);
}

public static class FundTransactionEntityIdValidationExtensions
{
    public static List<ValidationError> ValidateFundTransactionEntityId(
        this List<ValidationError> validationErrors,
        FundTransactionEntityId? entityId,
        string commandName)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (entityId is null)
        {
            validationErrors.Add(new($"{commandName}.EntityId is null"));
            return validationErrors;
        }
        if (entityId.FundId < 1)
            validationErrors.Add(new($"{commandName}.EntityId.FundId must be > 0"));
        if (entityId.OrderId < 0)
            validationErrors.Add(new($"{commandName}.EntityId.OrderId must be >= 0"));
        return validationErrors;
    }
}
