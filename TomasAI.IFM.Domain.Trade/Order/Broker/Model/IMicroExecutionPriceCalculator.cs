namespace TomasAI.IFM.Domain.Trade.Order.Broker.Model;
/// <summary>Calculates an exact tick-aligned price inside the approved envelope.</summary>
public interface IMicroExecutionPriceCalculator
{
    /// <summary>Returns the next approved limit, or null when no price can be proven.</summary>
    decimal? Calculate(MicroExecutionInput input);
}
