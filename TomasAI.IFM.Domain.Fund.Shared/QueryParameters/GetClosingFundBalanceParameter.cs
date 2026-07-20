using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve the closing balance for a specific fund on a given date.
/// </summary>
/// <remarks>Use this type to specify the fund identifier and the value date when requesting the closing fund
/// balance. This record is typically used as a data transfer object in service or actor-based APIs.</remarks>
[MessagePackObject(false)]
public record GetClosingFundBalanceParameter : IActorEntityId, IQueryParameter
{
    [Key(0)] public int FundId { get; init; }
    [Key(1)] public DateOnly ValueDate { get; init; }

    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetClosingFundBalanceParameter() { }

    [SerializationConstructor]
    public GetClosingFundBalanceParameter(int fundId, DateOnly valueDate)
    {
        FundId = fundId;
        ValueDate = valueDate;
        QueryParams = $"fundId={FundId}&valueDate={ValueDate:yyyy-MM-dd}";
    }

    public string Format()
        => $"{FundId}.{ValueDate:yyyy-MM-dd}";
}
