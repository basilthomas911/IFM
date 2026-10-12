namespace TomasAI.IFM.Domain.Trade.Order.Broker.Model;
/// <summary>Builds the action safety envelope before policy choice.</summary>
public interface IMicroExecutionConstraintEvaluator
{
    /// <summary>Returns all actions currently proven safe.</summary>
    MicroExecutionActionMask Evaluate(MicroExecutionInput input);
}
