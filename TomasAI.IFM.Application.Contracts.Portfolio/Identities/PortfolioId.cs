using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Identities;

/// <summary>Identifies one Portfolio aggregate.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record PortfolioId : IActorEntityId
{
    [Key(0)] public int Id { get; init; }

    public PortfolioId() { }

    public PortfolioId(int id) => Id = id;

    public string Format() => Id.ToString(CultureInfo.InvariantCulture);

    public IReadOnlyList<string> Validate() =>
        Id > 0 ? [] : ["PortfolioId.Id must be greater than zero."];

    public override string ToString() => Format();
}
