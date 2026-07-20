using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the opening balance for a specific fund on a given date.
/// </summary>
[MessagePackObject(false)]
public record GetOpeningFundBalanceParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int FundId { get; init; }
    [Key(1)] public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetOpeningFundBalanceParameter() { }

    [SerializationConstructor]
    public GetOpeningFundBalanceParameter(int fundId, DateOnly valueDate)
    {
        FundId = fundId;
        ValueDate = valueDate;
        QueryParams = $"fundId={FundId}&valueDate={ValueDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{FundId}.{ValueDate:yyyy-MM-dd}";
}
