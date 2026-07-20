using MessagePack;
using Newtonsoft.Json;

namespace TomasAI.IFM.Shared.Reference.ViewModels;

/// <summary>
/// MessagePack-serializable view model representing a lookup type short code with an ordering value.
/// </summary>
/// <remarks>
/// Pattern mirrors FundOrderReadModel: explicit properties with sequential MessagePack keys
/// and a parameterless constructor for serializers.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record LookupTypeShortCodeReadModel
{
    /// <summary>Short code for the lookup type.</summary>
    [Key(0)]
    public string ShortCode { get; init; } = string.Empty;

    /// <summary>Order or sort position for this short code.</summary>
    [Key(1)]
    public int OrderId { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public LookupTypeShortCodeReadModel() { }

    /// <summary>
    /// Creates a new lookup type short code view model.
    /// </summary>
    /// <param name="shortCode">The short code.</param>
    /// <param name="orderId">The order or sort position.</param>
    public LookupTypeShortCodeReadModel(string shortCode, int orderId)
    {
        ShortCode = shortCode ?? string.Empty;
        OrderId = orderId;
    }

    /// <summary>Returns a compact JSON representation.</summary>
    public override string ToString() => JsonConvert.SerializeObject(this);
}