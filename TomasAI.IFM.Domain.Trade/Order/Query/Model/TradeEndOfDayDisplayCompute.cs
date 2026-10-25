using TomasAI.IFM.Domain.Trade.Shared.Order;
namespace TomasAI.IFM.Domain.Trade.Order.Query.Model;
/// <summary>Preserves the established UI formula with exact backend decimal arithmetic.</summary>
public static class TradeEndOfDayDisplayCompute{
 public static TradeEndOfDayDisplay Calculate(TradeEndOfDayDisplayRequest request){
  ArgumentNullException.ThrowIfNull(request);
  var position=request.Position??throw new ArgumentException("Position evidence is required.");
  if(!position.Id.IsValid)throw new ArgumentException("A scoped position is required.");
  var pnl=checked(position.RealizedPnl+position.UnrealizedPnl);
  return new(position.Id,position.PositionSequence,pnl,checked(request.AvailableCash+pnl));
 }
}
