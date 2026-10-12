namespace TomasAI.IFM.Domain.Trade.Order.Broker.Model;
/// <summary>Selects one action from an already constrained decision input.</summary>
public interface IMicroExecutionPolicy
{
    /// <summary>Returns one deterministic material action.</summary>
    MicroExecutionDecision Decide(MicroExecutionInput input);
}
