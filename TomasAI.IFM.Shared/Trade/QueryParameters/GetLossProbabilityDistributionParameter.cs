using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve loss probability distribution data for a specific value date.
/// </summary>
[MessagePackObject(false)]
public record GetLossProbabilityDistributionParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetLossProbabilityDistributionParameter() { }

    [SerializationConstructor]
    public GetLossProbabilityDistributionParameter(DateOnly valueDate)
    {
        ValueDate = valueDate;
        QueryParams = $"valueDate={ValueDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{ValueDate:yyyy-MM-dd}";
}
