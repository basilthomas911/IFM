using MessagePack;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public sealed record VolatilityWorkflowGateResult(bool AllowsNewEntry, string ReasonCode);
