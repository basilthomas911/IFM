using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
[MessagePackObject]
public sealed record RiskQuantityCheck([property: Key(0)] int Units, [property: Key(1)] decimal Cash,
    [property: Key(2)] decimal Loss, [property: Key(3)] bool CashFits, [property: Key(4)] bool LossFits,
    [property: Key(5)] ImmutableArray<RiskLimitCheck> Limits);
