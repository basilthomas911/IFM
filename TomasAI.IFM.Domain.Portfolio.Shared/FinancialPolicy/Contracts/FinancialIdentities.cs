using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record FinancialExecutionId([property: Key(0)] int PortfolioId, [property: Key(1)] Guid OperationId) : IActorEntityId
{
    public string Format() => FormattableString.Invariant($"{PortfolioId}.{OperationId:N}");
    public IReadOnlyList<string> Validate() => PortfolioId > 0 && OperationId != Guid.Empty ? [] : ["Portfolio and operation identities are required."];
}
[MessagePackObject]
public sealed record LedgerPortfolioId([property: Key(0)] int PortfolioId) : IActorEntityId
{
    public string Format() => PortfolioId.ToString(CultureInfo.InvariantCulture);
    public IReadOnlyList<string> Validate() => PortfolioId > 0 ? [] : ["Portfolio identity is required."];
}
[MessagePackObject]
public sealed record CapacityReservationEntityId([property: Key(0)] int PortfolioId, [property: Key(1)] Guid ReservationId) : IActorEntityId
{
    public string Format() => FormattableString.Invariant($"{PortfolioId}.{ReservationId:N}");
    public IReadOnlyList<string> Validate() => PortfolioId > 0 && ReservationId != Guid.Empty ? [] : ["Portfolio and reservation identities are required."];
}

