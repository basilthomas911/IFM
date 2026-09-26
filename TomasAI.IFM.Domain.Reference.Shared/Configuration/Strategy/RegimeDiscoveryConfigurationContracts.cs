using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.Shared.Configuration.Strategy;

/// <summary>Identifies one immutable Regime Discovery parameter-set version.</summary>
[MessagePackObject]
public readonly record struct RegimeDiscoveryParameterSetEntityId(
    [property: Key(0)] Guid ParameterSetId,
    [property: Key(1)] int Version) : IActorEntityId
{
    /// <summary>Formats the stable actor routing identity.</summary>
    public string Format() => $"{ParameterSetId:N}.{Version}";
    /// <inheritdoc />
    public override string ToString() => Format();
}
