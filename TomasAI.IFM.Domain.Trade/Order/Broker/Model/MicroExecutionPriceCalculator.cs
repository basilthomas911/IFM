namespace TomasAI.IFM.Domain.Trade.Order.Broker.Model;
/// <summary>Default price model that moves one tick toward executable evidence.</summary>
public sealed class MicroExecutionPriceCalculator : IMicroExecutionPriceCalculator
{
    /// <inheritdoc/>
    public decimal? Calculate(MicroExecutionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Bid <= 0m || input.Ask < input.Bid || input.TickIncrement <= 0m)
            return null;
        var target = input.CurrentLimit + input.TickIncrement;
        if (input.Profile.UrgentClosing)
            target = Math.Max(target, input.Ask);
        target = Math.Clamp(target, input.MinimumLimit, input.MaximumLimit);
        return decimal.Remainder(target, input.TickIncrement) == 0m ? target : null;
    }
}
