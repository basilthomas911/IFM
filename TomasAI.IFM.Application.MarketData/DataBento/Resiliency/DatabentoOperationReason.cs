using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.DataBento.TickAggregation.Contracts;

namespace TomasAI.IFM.Application.MarketData.Databento.Resiliency;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1008", Justification = "Zero has existing contract semantics; adding an alias would change validation and serialization names.")]
public enum DatabentoOperationReason
{
    InitialStartup = 1, ScheduledSessionStart = 2, WatchdogPoll = 3,
    AutomaticRecovery = 4, ManualReset = 5, ValueDateRollover = 6,
    RequestedStop = 7, ApplicationShutdown = 8
}
