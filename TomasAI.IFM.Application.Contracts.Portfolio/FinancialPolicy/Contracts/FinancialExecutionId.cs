using System.Globalization;
using MessagePack;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record FinancialExecutionId([property: Key(0)] int PortfolioId, [property: Key(1)] Guid OperationId) : IActorEntityId
{
    public string Format() => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{PortfolioId}.{OperationId:N}");
    public IReadOnlyList<string> Validate() => PortfolioId > 0 && OperationId != Guid.Empty ? [] : ["Portfolio and operation identities are required."];
}

