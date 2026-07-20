using MonteCarloOptionPricer.Net;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventQueue;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.StatusConsole;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;

namespace TomasAI.IFM.Service.OptionPricer;

public class SpreadDistributionService : ISpreadDistributionServiceApi
{
    readonly IOptionPricerQueryApi _optionPricerQuery;
    readonly IOptionPricerCommandApi _optionPricerCommand;
    readonly IMarketDataQueryApi _mktDataQuery;
    readonly IMarketDataFeedQueryApi _mktDataFeedQuery;
    readonly ITradeCommandApi _tradeCommand;
    readonly ITradeQueryApi _tradeQuery;
    readonly IStatusConsoleWriter _statusConsoleWriter;
    IOptionSpreadPricer _optionSpreadPricer;
    ConcurrentEventQueue<SpreadDistributionJobReadModel> _jobQueue;

    public SpreadDistributionService(
        IOptionPricerQueryApi optionPricerQuery,
        IOptionPricerCommandApi optionPricerCommand,
        IMarketDataQueryApi mktDataQuery,
        IMarketDataFeedQueryApi mktDataFeedQuery,
        ITradeCommandApi tradeCommand,
        ITradeQueryApi tradeQuery,
        IStatusConsoleWriter statusConsoleWriter)
    {
        _optionPricerQuery = optionPricerQuery;
        _optionPricerCommand = optionPricerCommand;
        _mktDataQuery = mktDataQuery;
        _mktDataFeedQuery = mktDataFeedQuery;
        _tradeCommand = tradeCommand;
        _tradeQuery = tradeQuery;
        _statusConsoleWriter = statusConsoleWriter;
        _jobQueue = new ConcurrentEventQueue<SpreadDistributionJobReadModel>(RunSpreadDistributionJobAsync);
        _jobQueue.Start();
    }

    public async Task<ServiceResult<SpreadDistributionJobReadModel>> ExecuteAsync(SpreadDistributionJobReadModel e)
    {
        if (_jobQueue.IsEmpty)
            _jobQueue.EnqueueAndSignal(e);
        return await Task.FromResult(new ServiceOk<SpreadDistributionJobReadModel>(e));
    }

    async Task RunSpreadDistributionJobAsync(SpreadDistributionJobReadModel e)
    {
        ServiceResult<SpreadDistributionJobReadModel> serviceResult;
        try
        {
            LoadOptionSpreadPricer();
            var spreadDistService = GetSpreadDistributionService();
            if (spreadDistService != null)
                serviceResult = await spreadDistService.ExecuteAsync(e);
            else
                serviceResult = new ServiceFailed<SpreadDistributionJobReadModel>(-101, $"SpreadDistributionJobFailed: Invalid Spread Distribution Job Trade Type '{e.TradeType}'");
        }
        catch (Exception ex)
        {
            serviceResult = new ServiceFailed<SpreadDistributionJobReadModel>(-102, ex.GetErrorMessage());
        }
        if (serviceResult.Success && serviceResult.Value != null)
        {
            var spreadJob = serviceResult.Value;
            await _optionPricerCommand.CompleteSpreadDistributionJobAsync(spreadJob.EntityId, DateTime.Now, SpreadDistributionJobStatus.Completed);
            await WriteConsoleAsync($"OptionPricerJobCompleted: {spreadJob.JobSubmitted:hh:mm:ss} Duration {spreadJob.Duration:F0} milliseconds");
        }
        else
        {
            await _optionPricerCommand.FailSpreadDistributionJobAsync(e.EntityId, DateTime.Now, SpreadDistributionJobStatus.Failed, serviceResult.ErrorMessage);
            await WriteConsoleAsync(serviceResult.ErrorCode, serviceResult.ErrorMessage);
        }
        return;

        void LoadOptionSpreadPricer()
        {
            if (_optionSpreadPricer is null)
            {
                var optionPricerDevices = new OptionPricerDeviceCollection(_optionPricerQuery);
                var optionPricerFactory = new OptionPricerFactory(optionPricerDevices);
                _optionSpreadPricer = new OptionSpreadPricer(optionPricerFactory);
            }
        }

        ISpreadDistributionServiceApi GetSpreadDistributionService()
            => default(ISpreadDistributionServiceApi) switch {
                _ when e.TradeType == TradeType.ShortIronCondor || e.TradeType == TradeType.LongIronCondor 
                    => new IronCondorSpreadDistribution(_tradeQuery, _tradeCommand, _optionSpreadPricer, _mktDataFeedQuery, _mktDataQuery),
                _ => default(ISpreadDistributionServiceApi)
            };
        
    }

    public async Task CreateOptionSpreadPricerAsync()
        => await Task.CompletedTask;

    public async Task DestroyOptionSpreadPriceAsync()
    {
        try
        {
            if (_optionSpreadPricer != null)
            {
                _optionSpreadPricer.Dispose();
                _optionSpreadPricer = null;
            }
        }
        catch { }
        await Task.CompletedTask;
    }

    private async Task WriteConsoleAsync(string message) => await _statusConsoleWriter.WriteConsoleAsync(LogSourceType.OptionPricer, message);
    
    private async Task WriteConsoleAsync(int statusCode, string message) => await _statusConsoleWriter.WriteConsoleAsync(LogSourceType.OptionPricer, statusCode, message);
    
}