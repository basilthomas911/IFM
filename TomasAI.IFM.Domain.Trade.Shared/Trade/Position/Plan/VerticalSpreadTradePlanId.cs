using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventProjector;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;

[MessagePackObject]
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Auto)]
public readonly record struct VerticalSpreadTradePlanId(
    [property: Key(0)] StrategyPositionId Position,
    [property: Key(1)] DateOnly ValueDate) : IActorEntityId
{
    public string Format() => string.Create(CultureInfo.InvariantCulture, $"{Position.Format()}.{ValueDate:yyyyMMdd}.VerticalSpread");
    [IgnoreMember] public bool IsValid => Position.IsValid && ValueDate != default;
}
