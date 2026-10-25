using MessagePack;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;

[MessagePackObject]
public sealed record RiskLimitCheck([property: Key(0)] int LimitIndex, [property: Key(1)] decimal Proposed,
    [property: Key(2)] decimal Existing, [property: Key(3)] bool Fits);
