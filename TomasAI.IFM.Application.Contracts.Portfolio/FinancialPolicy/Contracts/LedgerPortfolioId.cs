using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;
[MessagePackObject]
public sealed record LedgerPortfolioId([property: Key(0)] int PortfolioId) : IActorEntityId
{
    public string Format() => PortfolioId.ToString(CultureInfo.InvariantCulture);
    public IReadOnlyList<string> Validate() => PortfolioId > 0 ? [] : ["Portfolio identity is required."];
}

