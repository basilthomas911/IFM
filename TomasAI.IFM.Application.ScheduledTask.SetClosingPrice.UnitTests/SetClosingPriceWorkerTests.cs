using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using NSubstitute;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using TomasAI.IFM.Application.ScheduledTask.SetClosingPrice;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Queries;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Application.ScheduledTask.SetClosingPrice.UnitTests
{
    public class SetClosingPriceWorkerTests
    {
        [Fact]
        public async Task ExecuteOk()
        {
            var infoLogs = new List<string>();
            var errorLogs = new List<string>();
            var host = Substitute.For<IHost>();
            host.StopAsync().Returns(Task.CompletedTask);
            var logger = Substitute.For<MockLogger<Worker>>();
            logger.Log(LogLevel.Information, Arg.Do<string>(s => infoLogs.Add(s)));
            logger.Log(LogLevel.Error, Arg.Do<string>(s => errorLogs.Add(s)));
            var marketDataFeedCommandApi = Substitute.For<IMarketDataFeedCommandApi>();
            marketDataFeedCommandApi
                .InsertFuturesClosingPriceAsync(Arg.Any<FuturesClosingPriceId>(), Arg.Any<double>())
                .Returns( Task.FromResult( new ServiceResult<Guid>(Guid.NewGuid())));
            var marketDataFeedQueryApi = Substitute.For<IMarketDataFeedQueryApi>();
            marketDataFeedQueryApi
                .GetLastFuturesTickDataAsync(Arg.Any<string>())
                .Returns( Task.FromResult(new ServiceResult<FuturesTickDataViewModel>(SampleData.FuturesTickData)));
            var marketDataQueryApi = Substitute.For<IMarketDataQueryApi>();
            marketDataQueryApi
                .GetCurrentlyTradedFuturesContractsAsync()
                .Returns( Task.FromResult(new ServiceResult<FuturesContractViewModel[]>( new FuturesContractViewModel[] { SampleData.FuturesContract})));
            marketDataQueryApi
                .GetValueDateAsync()
                .Returns(Task.FromResult(new ServiceResult<ScalarReadModel<DateTime>>(new ScalarReadModel<DateTime> { Value = new DateTime(2022, 10, 10) })));
            var setClosingPriceWorker = new Worker(
                host,
                logger,
                marketDataFeedCommandApi,
                marketDataFeedQueryApi,
                marketDataQueryApi);
            setClosingPriceWorker.StartAsync(cancellationToken: default).Wait();
            infoLogs.Should().HaveCount(5);
            infoLogs.Should().Contain("loading value date...");
            infoLogs.Should().Contain($"loaded value date for {new DateTime(2022, 10, 10):yyyy-MM-dd}");
            infoLogs.Should().Contain("loading currently traded futures contracts...");
            infoLogs.Should().Contain($"loaded {1} futures contracts");
            infoLogs.Should().Contain($"futures {SampleData.FuturesContract.ContractId} closing price inserted");
            await Task.CompletedTask;

        }

        [Fact]
        public async Task Execute_InvalidValueDate()
        {
            var infoLogs = new List<string>();
            var errorLogs = new List<string>();
            var host = Substitute.For<IHost>();
            host.StopAsync().Returns(Task.CompletedTask);
            var logger = Substitute.For<MockLogger<Worker>>();
            logger.Log(LogLevel.Information, Arg.Do<string>(s => infoLogs.Add(s)));
            logger.Log(LogLevel.Error, Arg.Do<string>(s => errorLogs.Add(s)));
            var marketDataFeedCommandApi = Substitute.For<IMarketDataFeedCommandApi>();
            marketDataFeedCommandApi
                .InsertFuturesClosingPriceAsync(Arg.Any<FuturesClosingPriceId>(), Arg.Any<double>())
                .Returns(Task.FromResult(new ServiceResult<Guid>(Guid.NewGuid())));
            var marketDataFeedQueryApi = Substitute.For<IMarketDataFeedQueryApi>();
            marketDataFeedQueryApi
                .GetLastFuturesTickDataAsync(Arg.Any<string>())
                .Returns(Task.FromResult(new ServiceResult<FuturesTickDataViewModel>(SampleData.FuturesTickData)));
            var marketDataQueryApi = Substitute.For<IMarketDataQueryApi>();
            marketDataQueryApi
                .GetCurrentlyTradedFuturesContractsAsync()
                .Returns(Task.FromResult(new ServiceResult<FuturesContractViewModel[]>(new FuturesContractViewModel[] { SampleData.FuturesContract })));
            marketDataQueryApi
                .GetValueDateAsync()
                .Returns(Task.FromResult(new ServiceResult<ScalarReadModel<DateTime>>(1234, "Invalid value date")));
            var setClosingPriceWorker = new Worker(
                host,
                logger,
                marketDataFeedCommandApi,
                marketDataFeedQueryApi,
                marketDataQueryApi);
            setClosingPriceWorker.StartAsync(cancellationToken: default).Wait();
            infoLogs.Should().HaveCount(1);
            infoLogs.Should().Contain("loading value date...");
            errorLogs.Should().HaveCount(1);
            errorLogs.Should().Contain("unable to load value date due to Invalid value date");
            await Task.CompletedTask;

        }

        [Fact]
        public async Task Execute_InvalidCurrentlyTradedFuturesContracts()
        {
            var infoLogs = new List<string>();
            var errorLogs = new List<string>();
            var host = Substitute.For<IHost>();
            host.StopAsync().Returns(Task.CompletedTask);
            var logger = Substitute.For<MockLogger<Worker>>();
            logger.Log(LogLevel.Information, Arg.Do<string>(s => infoLogs.Add(s)));
            logger.Log(LogLevel.Error, Arg.Do<string>(s => errorLogs.Add(s)));
            var marketDataFeedCommandApi = Substitute.For<IMarketDataFeedCommandApi>();
            marketDataFeedCommandApi
                .InsertFuturesClosingPriceAsync(Arg.Any<FuturesClosingPriceId>(), Arg.Any<double>())
                .Returns(Task.FromResult(new ServiceResult<Guid>(Guid.NewGuid())));
            var marketDataFeedQueryApi = Substitute.For<IMarketDataFeedQueryApi>();
            marketDataFeedQueryApi
                .GetLastFuturesTickDataAsync(Arg.Any<string>())
                .Returns(Task.FromResult(new ServiceResult<FuturesTickDataViewModel>(SampleData.FuturesTickData)));
            var marketDataQueryApi = Substitute.For<IMarketDataQueryApi>();
            marketDataQueryApi
                .GetCurrentlyTradedFuturesContractsAsync()
                .Returns(Task.FromResult(new ServiceResult<FuturesContractViewModel[]>(1234,"Invalid currently traded futures contracts")));
            marketDataQueryApi
                .GetValueDateAsync()
               .Returns(Task.FromResult(new ServiceResult<ScalarReadModel<DateTime>>(new ScalarReadModel<DateTime> { Value = new DateTime(2022, 10, 10) })));
            var setClosingPriceWorker = new Worker(
                host,
                logger,
                marketDataFeedCommandApi,
                marketDataFeedQueryApi,
                marketDataQueryApi);
            setClosingPriceWorker.StartAsync(cancellationToken: default).Wait();
            infoLogs.Should().HaveCount(3);
            infoLogs.Should().Contain("loading value date...");
            infoLogs.Should().Contain($"loaded value date for {new DateTime(2022, 10, 10):yyyy-MM-dd}");
            infoLogs.Should().Contain("loading currently traded futures contracts...");
            errorLogs.Should().HaveCount(1);
            errorLogs.Should().Contain("unable to load any currently traded futures contracts due to Invalid currently traded futures contracts");
            await Task.CompletedTask;

        }
    }

    public abstract class MockLogger<T> : ILogger<T>
    {
        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) =>
            Log(logLevel, formatter(state, exception));

        public abstract void Log(LogLevel logLevel, string message);

        public virtual bool IsEnabled(LogLevel logLevel) => true;

        public abstract IDisposable BeginScope<TState>(TState state);
    }
}