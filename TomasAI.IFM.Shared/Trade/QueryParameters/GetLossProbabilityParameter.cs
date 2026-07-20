using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.Trade.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve loss probability based on a forward loss ratio and value date.
/// </summary>
[MessagePackObject(false)]
public record GetLossProbabilityParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public double ForwardLossRatio { get; init; }
    [Key(1)] public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetLossProbabilityParameter() { }

    [SerializationConstructor]
    public GetLossProbabilityParameter(double forwardLossRatio, DateOnly valueDate)
    {
        ForwardLossRatio = forwardLossRatio;
        ValueDate = valueDate;
        QueryParams = $"forwardLossRatio={ForwardLossRatio:F4}&valueDate={ValueDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{ForwardLossRatio:F4}.{ValueDate:yyyy-MM-dd}";
}
