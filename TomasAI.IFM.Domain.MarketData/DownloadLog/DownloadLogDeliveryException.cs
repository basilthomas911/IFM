using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.MarketData.Shared.DownloadLog;
using TomasAI.IFM.Domain.MarketData.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.DownloadLog;

/// <summary>Carries the original immutable outcome for redelivery without rerunning acquisition.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032", Justification = "This typed failure requires immutable outcome or reason-code evidence; generic constructors would create an invalid failure contract.")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1194", Justification = "This failure requires its typed immutable evidence; generic exception constructors cannot represent a valid domain outcome.")]
public sealed class DownloadLogDeliveryException(MarketDataDownloadOutcome outcome, Exception inner)
    : Exception($"DownloadLog delivery failed for import {outcome.ImportCommandId}; redeliver its terminal outcome, not the import.", inner)
{
    public MarketDataDownloadOutcome Outcome { get; } = outcome;
}
