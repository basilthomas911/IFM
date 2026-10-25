using MessagePack;

namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record AccountingAccountMapping([property: Key(0)] int AccountId, [property: Key(1)] long AccountVersion,
    [property: Key(2)] string ExternalAccountReference);
