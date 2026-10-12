namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1028", Justification = "Preserve the existing serialized byte enum representation.")]
public enum MarketConditionFailureCategory : byte
{
    Undefined = 0, ContractInvalid = 1, ConfigurationUnavailable = 2, RequiredInputInvalid = 3,
    CalculationFailed = 4, InvariantViolation = 5, ProjectionFailed = 6, PersistenceFailed = 7, Timeout = 8
}
