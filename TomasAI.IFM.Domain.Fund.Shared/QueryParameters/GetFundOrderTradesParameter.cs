using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Fund.Shared.QueryParameters;

/// <summary>
/// Represents the parameters required to retrieve all fund order trades.
/// </summary>
[MessagePackObject(false)]
public record GetFundOrderTradesParameter : IActorEntityId, IQueryParameter
{
    [IgnoreMember]
    public string? QueryParams { get; private set; }

    public GetFundOrderTradesParameter()
    {
        QueryParams = string.Empty;
    }

    public string Format()
        => "all";
}
