using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve all fund orders.
/// </summary>
[MessagePackObject(false)]
public record GetFundOrdersParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFundOrdersParameter()
    {
        QueryParams = string.Empty;
    }

    public string Format()
        => "all";
}
