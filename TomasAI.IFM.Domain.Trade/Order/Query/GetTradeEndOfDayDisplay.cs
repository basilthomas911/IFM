using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Order.Query.Actor;
using TomasAI.IFM.Domain.Trade.Order.Query.Model;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.Trade.Order.Query;
public static class GetTradeEndOfDayDisplay{
 /// <summary>Calculates display amounts only; EOD commands remain position-actor owned.</summary>
 public static async ValueTask ExecuteAsync(this GetTradeEndOfDayDisplayQuery query,ITradeOrderQueryContext context,CancellationToken token){
  token.ThrowIfCancellationRequested();ServiceResult<TradeEndOfDayDisplay> response;
  try{response=new ServiceOk<TradeEndOfDayDisplay>(TradeEndOfDayDisplayCompute.Calculate(query.Request));}
  catch(Exception error)when(error is ArgumentException or OverflowException){response=new ServiceFailed<TradeEndOfDayDisplay>(400,error.Message);}
  await context.ReplyAsync(query.Subject.ThreadId,query.Subject.Verb,response).ConfigureAwait(false);
 }
}
