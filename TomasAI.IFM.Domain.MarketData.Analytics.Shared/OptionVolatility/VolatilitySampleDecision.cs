using System.Collections.Immutable;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.OptionVolatility;

public sealed record VolatilitySampleDecision(
    bool Accepted,
    bool ReplacedCoalescedCheckpoint,
    string ReasonCode);
