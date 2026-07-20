using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketDataFeed.Events;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesTickData.Event;

public static class FuturesTickDataInserted
{
    static FuturesTickDataInserted()
    {
        ServiceId = $"{LogSourceType.FuturesTickDataEvent}";
    }

    static string ServiceId { get; }

    public static async ValueTask<bool> ExecuteAsync(this FuturesTickDataInsertedEvent e, IEventActorContext context, FuturesTickDataEventParameters p)
    {
        var source = $"FuturesTickDataInsertedEvent for EntityId: {e.EntityId}";
        try
        {
            var valueDate = e.TickData.ValueDate;
            if (!e.Contract.Id.IsVixContract)
            {
                var eodDataToday = await context.GetFuturesEodDataAsync(e.Contract.ContractId, valueDate);
                if (eodDataToday is null)
                    return false;
                var eodDataRange = await p.BlackboardService.FuturesEodDataRange.GetAsync(e.Contract.ContractId, valueDate,
                     (contractId, starteDate, endDate) => context.GetFuturesEodDataByDateRangeAsync(contractId, starteDate, endDate));
                var normCurveTbl = await p.BlackboardService.NormalCurveTable.GetAsync(valueDate,
                    () => context.GetNormalCurveTableAsync()!);
                var vixContractId = p.BlackboardService.VixFuturesContractId.Get(valueDate);
                var vixFuturesEodData = p.BlackboardService.VixFuturesEodData.Get(vixContractId!, valueDate);
                if (vixFuturesEodData.Count == 0)
                {
                    vixFuturesEodData = await context.GetVixFuturesEodDataAsync(vixContractId!, valueDate);
                    p.BlackboardService.VixFuturesEodData.Set(vixFuturesEodData.First().ContractId, valueDate, vixFuturesEodData);
                    if (string.IsNullOrEmpty(vixContractId))
                        p.BlackboardService.VixFuturesContractId.Set(valueDate, vixFuturesEodData.First().ContractId);
                }

                // save futures eod data...
                if (eodDataToday.ClosePrice != e.TickData.Price)
                    await context.InsertFuturesEodDataAsync(valueDate, e.TickData, e.Contract, eodDataToday, eodDataRange, normCurveTbl!, 20, vixFuturesEodData!);
            }
            else
            {
                await context.InsertVixFuturesEodDataAsync(e.TickData);
                p.BlackboardService.VixFuturesContractId.Set(valueDate, e.TickData.ContractId);
            }
            return true;
        }
        catch (Exception ex)
        {
            await p.StatusConsoleWriter.WriteConsoleAsync(LogSourceType.MarketDataFeedEvent, 6009, ex.GetErrorMessage());
            p.Logger.LogErrorEvent(ServiceId, ex, "{Source}: futures eod data {ContractId} insert failed", source, e.Contract.ContractId);
        }
        return false;
    }
}
