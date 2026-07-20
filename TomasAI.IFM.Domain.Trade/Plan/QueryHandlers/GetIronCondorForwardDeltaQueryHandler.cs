using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Trade.Queries;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Application.Storage.ScyllaDb.MarketDataDb;

namespace TomasAI.IFM.Domain.Trade.Plan.QueryHandlers;

public class GetIronCondorForwardDeltaQueryHandler(IMarketDataDbContext db)
    : BaseQueryHandler,
    IAsyncQueryHandler<GetIronCondorForwardDeltaQuery, IronCondorForwardDeltaDataModel>
{
    /// <summary>
    /// Executes the specified query to retrieve Iron Condor forward delta data.
    /// </summary>
    /// <param name="q">The query containing the parameters required to fetch the Iron Condor forward delta data. Cannot be null.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an 
    /// <IronCondorForwardDeltaDataModel> with the forward delta data.</returns>
    public async Task<IronCondorForwardDeltaDataModel> ExecuteAsync(GetIronCondorForwardDeltaQuery q)
        => await ExecuteQueryAsync(q, () => GetIronCondorForwardDeltaAsync(q));

    async Task<IronCondorForwardDeltaDataModel> GetIronCondorForwardDeltaAsync(GetIronCondorForwardDeltaQuery q)
    {
        var forwardDeltaValue = 0.0;
        var vixFuturesEodData = await db.GetLastVixFuturesEodDataAsync(q.VixContractId, q.ValueDate);
        if (vixFuturesEodData is not null)
        {
            forwardDeltaValue = q.TradeType switch
            {
                TradeType.ShortIronCondor => GetShortIronCondorForwardDeltaValue(Convert.ToDouble(vixFuturesEodData.ClosePrice)),
                TradeType.LongIronCondor => GetLongIronCondorForwardDeltaValue(Convert.ToDouble(vixFuturesEodData.ClosePrice)),
                _ => throw new NotImplementedException("Invalid Vix Futures Eod Data Close Price")
            };
        }
        var forwardDelta = new IronCondorForwardDeltaDataModel(forwardDeltaValue);
        return forwardDelta;

        double GetShortIronCondorForwardDeltaValue(double vix)
        {
            var baseDeltaValue = Math.Round((30 - vix) / 1000, 3);
            return vix switch
            {
                _ when vix >= 30.0 => 0.026,
                _ when vix < 30 && vix > 20.0 => 0.026 + baseDeltaValue,
                _ when vix <= 20.0 => 0.026 + 0.011,
                _ => throw new NotImplementedException("Invalid Vix Price")
            };
        }

        double GetLongIronCondorForwardDeltaValue(double vix)
        {
            var baseDeltaValue = Math.Round((30 - vix) / 1000, 3);
            return vix switch
            {
                _ when vix >= 30.0 => 0.044,
                _ when vix < 30 && vix > 20.0 => 0.044 - baseDeltaValue,
                _ when vix <= 20.0 => 0.044 - 0.011,
                _ => throw new NotImplementedException("Invalid Vix Price")
            };
        }
    }
}
