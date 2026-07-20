using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the current balance for a specific fund.
/// </summary>
[MessagePackObject(false)]
public record GetFundBalanceParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int FundId { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFundBalanceParameter() { }

    [SerializationConstructor]
    public GetFundBalanceParameter(int fundId)
    {
        FundId = fundId;
        QueryParams = $"fundId={FundId}";
    }

    public string Format()
        => $"{FundId}";
}
