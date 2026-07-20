using System;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.QueryParameters;

/// <summary>
/// Represents the parameters required to check whether a yield curve rate exists for a specific value date.
/// </summary>
/// <remarks>Use this type to specify the value date when querying for the existence of a yield curve rate.
/// This record is typically used as a data transfer object in service or actor-based APIs.</remarks>
[MessagePackObject(false)]
public record GetYieldCurveRateExistsParameter : IActorEntityId, IQueryParameter
{
    /// <summary>
    /// The value (as-of) date to check for an existing yield curve rate.
    /// </summary>
    [Key(0)] 
    public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetYieldCurveRateExistsParameter() { }

    /// <summary>
    /// Creates a new parameter instance for the specified value date.
    /// </summary>
    /// <param name="valueDate">The date to check for an existing yield curve rate.</param>
    [SerializationConstructor]
    public GetYieldCurveRateExistsParameter(DateOnly valueDate)
    {
        ValueDate = valueDate;
        QueryParams = $"valueDate={ValueDate:yyyy-MM-dd}";
    }

    /// <summary>
    /// Formats the parameter as a string identifier.
    /// </summary>
    /// <returns>A formatted string representation of the value date.</returns>
    public string Format()
        => $"{ValueDate:yyyy-MM-dd}";
}
