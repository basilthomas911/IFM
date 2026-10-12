namespace TomasAI.IFM.Domain.MarketData.Shared;

/// <summary>
/// Identifies the stage at which a guarded tick-projection operation failed.
/// </summary>
public enum TickProjectionGuardFailureStage
{
    RegistrationResponseUnknown,
    RegisteredBeforeDataSubmission,
    DataBatchResponseUnknown,
    AfterDataAcknowledged
}
