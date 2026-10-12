using TomasAI.IFM.Framework.TradeBroker.Contracts;

namespace TomasAI.IFM.Framework.TradeBroker.InteractiveBrokers.Emulator.Engine;

/// <summary>Deterministic fault switches used only by isolated emulator qualification scenarios.</summary>
public sealed record EmulatorFaultProfile(
    bool PlaceDispatchOutcomeUnknown = false,
    bool SuppressAcknowledgement = false,
    bool PublishCommissionBeforeExecution = false,
    bool DuplicateExecutionCallbacks = false,
    bool AllowFillsAfterCancel = false,
    bool DisconnectMarketMatching = false);
