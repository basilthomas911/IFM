namespace TomasAI.IFM.Domain.Trade.Order.Broker.Model;
/// <summary>Default generic policy for durable place/update/cancel/reconcile decisions.</summary>
public sealed class MicroExecutionPolicy(IMicroExecutionConstraintEvaluator constraints, IMicroExecutionPriceCalculator prices) : IMicroExecutionPolicy
{
    /// <inheritdoc/>
    public MicroExecutionDecision Decide(MicroExecutionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var allowed = constraints.Evaluate(input);
        if ((allowed & MicroExecutionActionMask.Reconcile) != MicroExecutionActionMask.None && input.OutcomeUnknown)
            return new(MicroExecutionAction.Reconcile, allowed, null, "OutcomeUnknown");
        if ((allowed & MicroExecutionActionMask.Escalate) != MicroExecutionActionMask.None && (allowed & MicroExecutionActionMask.Cancel) == MicroExecutionActionMask.None)
            return new(MicroExecutionAction.Escalate, allowed, null, "DeadlineWithoutKnownOrder");
        if ((allowed & MicroExecutionActionMask.Cancel) != MicroExecutionActionMask.None && input.NowUtc >= input.DeadlineUtc)
            return new(MicroExecutionAction.Cancel, allowed, null, "Deadline");
        if ((allowed & MicroExecutionActionMask.Place) != MicroExecutionActionMask.None)
            return new(MicroExecutionAction.Place, allowed, input.CurrentLimit, "InitialPlace");
        if ((allowed & MicroExecutionActionMask.UpdateLimit) != MicroExecutionActionMask.None)
        {
            var limit = prices.Calculate(input);
            if (limit is not null && limit != input.CurrentLimit)
                return new(MicroExecutionAction.UpdateLimit, allowed, limit, "MaterialPriceChange");
        }

        return new(MicroExecutionAction.Wait, allowed, null, "NoMaterialAction");
    }
}
