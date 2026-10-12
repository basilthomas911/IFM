using MessagePack;
namespace TomasAI.IFM.Domain.Portfolio.Shared.Financial;

[MessagePackObject]
public sealed record GetFinancialPostingConfigurationRequest([property: Key(0)] DateOnly AccountingDate);
