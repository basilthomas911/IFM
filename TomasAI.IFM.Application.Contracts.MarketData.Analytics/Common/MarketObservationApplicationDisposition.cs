namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;

/// <summary>Describes whether a closed market observation advanced durable calculation state.</summary>
public enum MarketObservationApplicationDisposition
{
    /// <summary>No application decision has been made.</summary>
    Unknown = 0,

    /// <summary>The observation advanced calculation state.</summary>
    Applied = 1,

    /// <summary>The exact observation was already applied.</summary>
    Duplicate = 2,

    /// <summary>The observation is older than the durable calculation watermark.</summary>
    Stale = 3
}
