using System.Diagnostics.Metrics;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Commands;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;
public sealed record RiskLatencyObservation(double CandidateAgeMilliseconds, double? OldestQuoteAgeMilliseconds, bool AgeLimitEnforced);
