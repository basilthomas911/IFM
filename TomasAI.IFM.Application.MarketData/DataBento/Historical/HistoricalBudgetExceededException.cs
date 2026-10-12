using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;

namespace TomasAI.IFM.Application.MarketData.Databento.Historical;

/// <summary>Thrown before provider submission when an unapproved estimate exceeds request limits.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
public sealed class HistoricalBudgetExceededException : InvalidOperationException
{
    /// <summary>Initializes the budget failure.</summary>
    public HistoricalBudgetExceededException(
        MarketDataHistoricalEstimate estimate,
        decimal maximumCostUsd,
        long maximumBytes)
        : base($"Historical estimate {(estimate ?? throw new ArgumentNullException(nameof(estimate))).EstimatedCostUsd:C} / {estimate.EstimatedBytes} bytes exceeds "
               + $"the approved {maximumCostUsd:C} / {maximumBytes} byte budget.")
    {
        ArgumentNullException.ThrowIfNull(estimate);
        Estimate = estimate;
    }

    /// <summary>Gets the rejected estimate.</summary>
    public MarketDataHistoricalEstimate Estimate { get; }
}
