namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;

/// <summary>Signals a completed episode that did not fully restore downstream service.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This classified exception requires the failed contract, dates, capacity or recovery result; generic constructors would invent or omit essential diagnostic facts.")]
public sealed class DatabentoRecoveryEpisodeStatusException : Exception
{
    public DatabentoRecoveryRequestResult Result { get; }

    public DatabentoRecoveryEpisodeStatusException(DatabentoRecoveryRequestResult result)
        : base($"Recovery episode {(result ?? throw new ArgumentNullException(nameof(result))).CorrelationId} ended {result.Outcome}: {result.Detail}")
        => Result = result;
}
