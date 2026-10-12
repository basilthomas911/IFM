using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject]
public sealed record CapacityReservationEntityId([property: Key(0)] int PortfolioId, [property: Key(1)] Guid ReservationId) : IActorEntityId
{
    public string Format() => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{PortfolioId}.{ReservationId:N}");
    public IReadOnlyList<string> Validate() => PortfolioId > 0 && ReservationId != Guid.Empty ? [] : ["Portfolio and reservation identities are required."];
}

