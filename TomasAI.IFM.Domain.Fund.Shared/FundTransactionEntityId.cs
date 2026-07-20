using MessagePack;
using Newtonsoft.Json;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

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