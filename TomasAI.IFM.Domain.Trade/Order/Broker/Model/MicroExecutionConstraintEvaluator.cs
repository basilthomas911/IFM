namespace TomasAI.IFM.Domain.Trade.Order.Broker.Model;
/// <summary>Default safety constraints shared by opening and closing policies.</summary>
public sealed class MicroExecutionConstraintEvaluator : IMicroExecutionConstraintEvaluator
{
    /// <inheritdoc/>
    public MicroExecutionActionMask Evaluate(MicroExecutionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.NowUtc.Kind != DateTimeKind.Utc || input.QuoteAtUtc.Kind != DateTimeKind.Utc || input.DeadlineUtc.Kind != DateTimeKind.Utc || input.TickIncrement <= 0m || input.MinimumLimit > input.MaximumLimit)
            return MicroExecutionActionMask.None;
        if (input.OutcomeUnknown)
            return MicroExecutionActionMask.Reconcile | MicroExecutionActionMask.Wait;
        if (input.MutationInFlight)
            return MicroExecutionActionMask.Wait | MicroExecutionActionMask.Reconcile;
        if (input.Opening && !input.GateAllowsNewRisk)
            return input.BrokerAcknowledged ? MicroExecutionActionMask.Cancel | MicroExecutionActionMask.Wait : MicroExecutionActionMask.Wait;
        if (input.NowUtc >= input.DeadlineUtc)
            return input.BrokerAcknowledged ? MicroExecutionActionMask.Cancel | MicroExecutionActionMask.Escalate : MicroExecutionActionMask.Escalate;
        var mask = MicroExecutionActionMask.Wait;
        if (!input.BrokerAcknowledged)
            mask |= MicroExecutionActionMask.Place;
        var fresh = input.QuoteAtUtc <= input.NowUtc && input.NowUtc - input.QuoteAtUtc <= input.Profile.MaximumQuoteAge;
        var cadenceMet = input.LastMutationAtUtc is null || input.NowUtc - input.LastMutationAtUtc >= input.Profile.MinimumUpdateCadence;
        if (input.BrokerAcknowledged && fresh && cadenceMet && input.PriceChangeCount < input.Profile.MaximumPriceChanges)
            mask |= MicroExecutionActionMask.UpdateLimit | MicroExecutionActionMask.Cancel;
        return mask;
    }
}
