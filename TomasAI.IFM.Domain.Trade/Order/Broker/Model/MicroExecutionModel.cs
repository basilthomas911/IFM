namespace TomasAI.IFM.Domain.Trade.Order.Broker.Model;

/// <summary>Actions available to the generic broker micro-execution policy.</summary>
public enum MicroExecutionAction : byte
{
    Unknown = 0,
    Wait = 1,
    Place = 2,
    UpdateLimit = 3,
    Cancel = 4,
    Reconcile = 5,
    Escalate = 6
}

/// <summary>Constraint-produced set of actions a policy may select.</summary>
[Flags]
public enum MicroExecutionActionMask : ushort
{
    None = 0,
    Wait = 1 << 0,
    Place = 1 << 1,
    UpdateLimit = 1 << 2,
    Cancel = 1 << 3,
    Reconcile = 1 << 4,
    Escalate = 1 << 5
}

/// <summary>Versioned deterministic controls for opening or closing execution.</summary>
public sealed record MicroExecutionProfile(
    string Id,
    int Version,
    string ContentHash,
    TimeSpan MinimumUpdateCadence,
    TimeSpan MaximumQuoteAge,
    int MaximumPriceChanges,
    bool UrgentClosing);

/// <summary>Immutable evidence consumed by one pure micro-execution decision.</summary>
public sealed record MicroExecutionInput(
    MicroExecutionProfile Profile,
    bool Opening,
    bool GateAllowsNewRisk,
    bool BrokerAcknowledged,
    bool MutationInFlight,
    bool OutcomeUnknown,
    decimal CurrentLimit,
    decimal MinimumLimit,
    decimal MaximumLimit,
    decimal TickIncrement,
    decimal Bid,
    decimal Ask,
    DateTime QuoteAtUtc,
    DateTime NowUtc,
    DateTime DeadlineUtc,
    DateTime? LastMutationAtUtc,
    int PriceChangeCount);

/// <summary>Material deterministic decision returned by the policy.</summary>
public sealed record MicroExecutionDecision(
    MicroExecutionAction Action,
    MicroExecutionActionMask AllowedActions,
    decimal? NewLimit,
    string Reason);

/// <summary>Builds the action safety envelope before policy choice.</summary>
public interface IMicroExecutionConstraintEvaluator
{
    /// <summary>Returns all actions currently proven safe.</summary>
    MicroExecutionActionMask Evaluate(MicroExecutionInput input);
}

/// <summary>Calculates an exact tick-aligned price inside the approved envelope.</summary>
public interface IMicroExecutionPriceCalculator
{
    /// <summary>Returns the next approved limit, or null when no price can be proven.</summary>
    decimal? Calculate(MicroExecutionInput input);
}

/// <summary>Selects one action from an already constrained decision input.</summary>
public interface IMicroExecutionPolicy
{
    /// <summary>Returns one deterministic material action.</summary>
    MicroExecutionDecision Decide(MicroExecutionInput input);
}

/// <summary>Default safety constraints shared by opening and closing policies.</summary>
public sealed class MicroExecutionConstraintEvaluator : IMicroExecutionConstraintEvaluator
{
    /// <inheritdoc />
    public MicroExecutionActionMask Evaluate(MicroExecutionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.NowUtc.Kind != DateTimeKind.Utc || input.QuoteAtUtc.Kind != DateTimeKind.Utc ||
            input.DeadlineUtc.Kind != DateTimeKind.Utc || input.TickIncrement <= 0m ||
            input.MinimumLimit > input.MaximumLimit)
            return MicroExecutionActionMask.None;
        if (input.OutcomeUnknown)
            return MicroExecutionActionMask.Reconcile | MicroExecutionActionMask.Wait;
        if (input.MutationInFlight)
            return MicroExecutionActionMask.Wait | MicroExecutionActionMask.Reconcile;
        if (input.Opening && !input.GateAllowsNewRisk)
            return input.BrokerAcknowledged
                ? MicroExecutionActionMask.Cancel | MicroExecutionActionMask.Wait
                : MicroExecutionActionMask.Wait;
        if (input.NowUtc >= input.DeadlineUtc)
            return input.BrokerAcknowledged
                ? MicroExecutionActionMask.Cancel | MicroExecutionActionMask.Escalate
                : MicroExecutionActionMask.Escalate;
        var mask = MicroExecutionActionMask.Wait;
        if (!input.BrokerAcknowledged) mask |= MicroExecutionActionMask.Place;
        var fresh = input.QuoteAtUtc <= input.NowUtc &&
            input.NowUtc - input.QuoteAtUtc <= input.Profile.MaximumQuoteAge;
        var cadenceMet = input.LastMutationAtUtc is null ||
            input.NowUtc - input.LastMutationAtUtc >= input.Profile.MinimumUpdateCadence;
        if (input.BrokerAcknowledged && fresh && cadenceMet &&
            input.PriceChangeCount < input.Profile.MaximumPriceChanges)
            mask |= MicroExecutionActionMask.UpdateLimit | MicroExecutionActionMask.Cancel;
        return mask;
    }
}

/// <summary>Default price model that moves one tick toward executable evidence.</summary>
public sealed class MicroExecutionPriceCalculator : IMicroExecutionPriceCalculator
{
    /// <inheritdoc />
    public decimal? Calculate(MicroExecutionInput input)
    {
        if (input.Bid <= 0m || input.Ask < input.Bid || input.TickIncrement <= 0m)
            return null;
        var target = input.CurrentLimit + input.TickIncrement;
        if (input.Profile.UrgentClosing) target = Math.Max(target, input.Ask);
        target = Math.Clamp(target, input.MinimumLimit, input.MaximumLimit);
        return decimal.Remainder(target, input.TickIncrement) == 0m ? target : null;
    }
}

/// <summary>Default generic policy for durable place/update/cancel/reconcile decisions.</summary>
public sealed class MicroExecutionPolicy(
    IMicroExecutionConstraintEvaluator constraints,
    IMicroExecutionPriceCalculator prices) : IMicroExecutionPolicy
{
    /// <inheritdoc />
    public MicroExecutionDecision Decide(MicroExecutionInput input)
    {
        var allowed = constraints.Evaluate(input);
        if ((allowed & MicroExecutionActionMask.Reconcile) != 0 && input.OutcomeUnknown)
            return new(MicroExecutionAction.Reconcile, allowed, null, "OutcomeUnknown");
        if ((allowed & MicroExecutionActionMask.Escalate) != 0 &&
            (allowed & MicroExecutionActionMask.Cancel) == 0)
            return new(MicroExecutionAction.Escalate, allowed, null, "DeadlineWithoutKnownOrder");
        if ((allowed & MicroExecutionActionMask.Cancel) != 0 &&
            (input.NowUtc >= input.DeadlineUtc || input.Opening && !input.GateAllowsNewRisk))
            return new(MicroExecutionAction.Cancel, allowed, null, "DeadlineOrGateClosed");
        if ((allowed & MicroExecutionActionMask.Place) != 0)
            return new(MicroExecutionAction.Place, allowed, input.CurrentLimit, "InitialPlace");
        if ((allowed & MicroExecutionActionMask.UpdateLimit) != 0)
        {
            var limit = prices.Calculate(input);
            if (limit is not null && limit != input.CurrentLimit)
                return new(MicroExecutionAction.UpdateLimit, allowed, limit, "MaterialPriceChange");
        }
        return new(MicroExecutionAction.Wait, allowed, null, "NoMaterialAction");
    }
}
