using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Moq;

using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log.ViewModels;
using TomasAI.IFM.Shared.Log.Events;
using TomasAI.IFM.Shared.MarketData.Events;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketData.ServiceApi;

namespace TomasAI.IFM.Application.Event.UnitTests
{
    public class MarketDataEventsTests
    {
        [Fact]
        public async Task FuturesContractAddedEventOk()
        {
            var futuresContract = default(FuturesContractViewModel);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>( ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFuturesContractAsync(It.IsAny<FuturesContractAddedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FuturesContractAddedEvent>(e => futuresContract = e.Contract);

            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            var contratAddedEvent = new FuturesContractAddedEvent {
                Contract = new FuturesContractViewModel
                (
                    contractId: "BT20191010",
                    description: "Unit Test Contract",
                    symbol: "BT",
                    localSymbol: "BT99",
                    securityType: "FUT",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    lastTradeDate: DateTime.Now.Date,
                    currentlyTraded: false
                ),
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt"
            };
            await mdEvents.ExecuteAsync(contratAddedEvent);

            Assert.NotNull(futuresContract);
            Assert.Equal("BT20191010", futuresContract.ContractId);
            Assert.Equal("Unit Test Contract", futuresContract.Description);
            Assert.Equal("BT", futuresContract.Symbol);
            Assert.Equal("BT99", futuresContract.LocalSymbol);
            Assert.Equal("FUT", futuresContract.SecurityType);
            Assert.Equal("USD", futuresContract.Currency);
            Assert.Equal("GLOBEX", futuresContract.Exchange );
            Assert.Equal("50", futuresContract.Multiplier);
            Assert.Equal(DateTime.Now.Date.Year, futuresContract.LastTradeDate.Year);
            Assert.Equal(DateTime.Now.Date.Month, futuresContract.LastTradeDate.Month);
            Assert.Equal(DateTime.Now.Date.Day, futuresContract.LastTradeDate.Day);

            Assert.NotNull(logVM);
            Assert.Equal("FuturesContractAdded: BT20191010", logVM.Message);
            Assert.Equal(0, logVM.StatusCode);
        }

        [Fact]
        public async Task FuturesContractAddedEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;

            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                 .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                 .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                 .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFuturesContractAsync(It.IsAny<FuturesContractAddedEvent>()))
                .Throws<InvalidOperationException>();
   
            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            var contratAddedEvent = new FuturesContractAddedEvent
            {
                Contract = new FuturesContractViewModel
                (
                    contractId: "BT20191010",
                    description: "Unit Test Contract",
                    symbol: "BT",
                    localSymbol: "BT99",
                    securityType: "FUT",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    lastTradeDate: DateTime.Now.Date,
                    currentlyTraded: false
                ),
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt"
            };

            await mdEvents.ExecuteAsync(contratAddedEvent);
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(mdEvents.ErrorCode, logVM.StatusCode);
        }

         [Fact]
        public async Task FuturesContractChangedEventOk()
        {
            var futuresContract = default(FuturesContractViewModel);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFuturesContractAsync(It.IsAny<FuturesContractAddedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FuturesContractAddedEvent>(e => futuresContract = e.Contract);

            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);

            // added test contract...
            var contratAddedEvent = new FuturesContractAddedEvent
            {
                Contract = new FuturesContractViewModel
                (
                    contractId: "BT20191010",
                    description: "Unit Test Contract",
                    symbol: "BT",
                    localSymbol: "BT99",
                    securityType: "FUT",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    lastTradeDate: DateTime.Now.Date,
                    currentlyTraded: false
                ),
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt"
            };
            await mdEvents.ExecuteAsync(contratAddedEvent);

            // check that test contratc was added...
            Assert.NotNull(futuresContract);

            Assert.Equal("BT20191010", futuresContract.ContractId);
            Assert.Equal("Unit Test Contract", futuresContract.Description);
            Assert.Equal("BT", futuresContract.Symbol);
            Assert.Equal("BT99", futuresContract.LocalSymbol);
            Assert.Equal("FUT", futuresContract.SecurityType );
            Assert.Equal("USD", futuresContract.Currency);
            Assert.Equal("GLOBEX", futuresContract.Exchange);
            Assert.Equal("50", futuresContract.Multiplier);
            Assert.Equal(DateTime.Now.Date.Year, futuresContract.LastTradeDate.Year);
            Assert.Equal(DateTime.Now.Date.Month, futuresContract.LastTradeDate.Month);
            Assert.Equal(DateTime.Now.Date.Day, futuresContract.LastTradeDate.Day);

            futuresContract = null;
            mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.UpdateFuturesContractAsync(It.IsAny<FuturesContractChangedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FuturesContractChangedEvent>(e => futuresContract = e.Contract);
            mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);

            // change single value...
            var contractChangedEvent = new FuturesContractChangedEvent
            {
                OriginalContractId = "BT20191010",
                Contract = new FuturesContractViewModel
                (
                    contractId: "BT20191010",
                    description: "Unit Test Contract",
                    symbol: "BT",
                    localSymbol: "BT99",
                    securityType: "FUT",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    lastTradeDate: DateTime.Now.Date,
                    currentlyTraded: false
                ),
                UpdatedOn = DateTime.Now,
                UpdatedBy = "basilt"
            };
            await mdEvents.ExecuteAsync(contractChangedEvent);

            // check changed value and all other values should have stayed the same...
            Assert.NotNull(futuresContract);

            Assert.Equal("BT20191010", futuresContract.ContractId);
            Assert.Equal("Unit Test Contract", futuresContract.Description);
            Assert.Equal("BT", futuresContract.Symbol);
            Assert.Equal("BT45", futuresContract.LocalSymbol);
            Assert.Equal("FUT", futuresContract.SecurityType);
            Assert.Equal("USD", futuresContract.Currency);
            Assert.Equal("GLOBEX", futuresContract.Exchange);
            Assert.Equal("50", futuresContract.Multiplier);
            Assert.Equal(DateTime.Now.Date.Year, futuresContract.LastTradeDate.Year);
            Assert.Equal(DateTime.Now.Date.Month, futuresContract.LastTradeDate.Month);
            Assert.Equal(DateTime.Now.Date.Day, futuresContract.LastTradeDate.Day);

            Assert.NotNull(logVM);
            Assert.Equal("FuturesContractChanged: BT20191010", logVM.Message);

        }

        [Fact]
        public async Task FuturesContractChangedEventWithError()
        {
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
   
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.UpdateFuturesContractAsync(It.IsAny<FuturesContractChangedEvent>()))
                .Throws<InvalidOperationException>();

            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);

            // added test contract...
            var contractChangedEvent = new FuturesContractChangedEvent
            {
                Contract = new FuturesContractViewModel
                (
                    contractId: "BT20191010",
                    description: "Unit Test Contract",
                    symbol: "BT",
                    localSymbol: "BT99",
                    securityType: "FUT",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    lastTradeDate: DateTime.Now.Date,
                    currentlyTraded: false
                ),
                UpdatedOn = DateTime.Now,
                UpdatedBy = "basilt"
            };
            await mdEvents.ExecuteAsync(contractChangedEvent);
            Assert.NotNull(logVM);
            Assert.Equal(logVM.Message, errorMsg);
        }

        [Fact]
        public async Task FuturesContractRemovedEventOk()
        {
            var futuresContract = default(FuturesContractViewModel);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                 .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                 .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                 .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFuturesContractAsync(It.IsAny<FuturesContractAddedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FuturesContractAddedEvent>(e => futuresContract = e.Contract);

            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            var contratAddedEvent = new FuturesContractAddedEvent
            {
                Contract = new FuturesContractViewModel
                (
                    contractId: "BT20191010",
                    description: "Unit Test Contract",
                    symbol: "BT",
                    localSymbol: "BT99",
                    securityType: "FUT",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    lastTradeDate: DateTime.Now.Date,
                    currentlyTraded: false
                ),
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt"
            };
            await mdEvents.ExecuteAsync(contratAddedEvent);

            Assert.NotNull(futuresContract);
            Assert.Equal("BT20191010", futuresContract.ContractId);
            Assert.Equal("Unit Test Contract", futuresContract.Description);
            Assert.Equal("BT", futuresContract.Symbol);
            Assert.Equal("BT99", futuresContract.LocalSymbol);
            Assert.Equal("FUT", futuresContract.SecurityType);
            Assert.Equal("USD", futuresContract.Currency);
            Assert.Equal("GLOBEX", futuresContract.Exchange);
            Assert.Equal("50", futuresContract.Multiplier);
            Assert.Equal(DateTime.Now.Date.Year, futuresContract.LastTradeDate.Year);
            Assert.Equal(DateTime.Now.Date.Month, futuresContract.LastTradeDate.Month);
            Assert.Equal(DateTime.Now.Date.Day, futuresContract.LastTradeDate.Day);

            var contractRemovedEvent = new FuturesContractRemovedEvent
            {
                ContractId = "BT20191010",
                DeletedOn = DateTime.Now,
                DeletedBy = "basilt"
            };

            var contractId = default(string);
            mockEventDenormalizer
                .Setup(e => e.DeleteFuturesContractAsync(It.IsAny<FuturesContractRemovedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FuturesContractRemovedEvent>(e => contractId = e.ContractId);

            mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            await mdEvents.ExecuteAsync(contractRemovedEvent);

            Assert.NotNull(contractId);
            Assert.Equal("BT20191010", contractId);
            Assert.NotNull(logVM);
            Assert.Equal("FuturesContractRemoved: BT20191010", logVM.Message);
        }

        
        [Fact]
        public async Task FuturesContractRemovedEventWithError()
        {
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);

            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                 .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                 .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                 .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.DeleteFuturesContractAsync(It.IsAny<FuturesContractRemovedEvent>()))
                .Throws<InvalidOperationException>();

            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            var contractRemovedEvent = new FuturesContractRemovedEvent
            {
                ContractId = "BT20191010",
                DeletedOn = DateTime.Now,
                DeletedBy = "basilt"
            };
            await mdEvents.ExecuteAsync(contractRemovedEvent);

            Assert.NotNull(logVM);
            Assert.Equal(logVM.Message, errorMsg);
        }

        [Fact]
        public async Task FuturesOptionContractAddedEventOk()
        {
            var futuresOptionContract = default(FuturesOptionContractReadModel);
            var logVM = default(StatusConsoleLogReadModel);

            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                 .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                 .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                 .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFuturesOptionContractAsync(It.IsAny<FuturesOptionContractAddedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FuturesOptionContractAddedEvent>(e => futuresOptionContract = e.Contract);

            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            var contractMonth = DateTime.Now.Date;
            var contractId = $"BT{contractMonth.ToString("yyyyMMdd")}P2740";
            var contractAddedEvent = new FuturesOptionContractAddedEvent
            {
                Contract = new FuturesOptionContractReadModel
                (
                    contractId: contractId,
                    description: "Unit Test Futures Option Contract",
                    symbol: "BT",
                    localSymbol: "BT99",
                    securityType: "FOP",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    contractMonth: contractMonth,
                    strikePrice: 2740,
                    optionType: "PUT"
                ),
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt"
            };
            await mdEvents.ExecuteAsync(contractAddedEvent);

            Assert.NotNull(futuresOptionContract);
            Assert.Equal(contractId, futuresOptionContract.ContractId);
            Assert.Equal("Unit Test Futures Option Contract", futuresOptionContract.Description);
            Assert.Equal("BT", futuresOptionContract.Symbol);
            Assert.Equal("BT99", futuresOptionContract.LocalSymbol);
            Assert.Equal("FOP", futuresOptionContract.SecurityType);
            Assert.Equal("USD", futuresOptionContract.Currency);
            Assert.Equal("GLOBEX", futuresOptionContract.Exchange);
            Assert.Equal("50", futuresOptionContract.Multiplier );
            Assert.Equal(DateTime.Now.Date.Year, futuresOptionContract.ContractMonth.Year);
            Assert.Equal(DateTime.Now.Date.Month, futuresOptionContract.ContractMonth.Month);
            Assert.Equal(DateTime.Now.Date.Day, futuresOptionContract.ContractMonth.Day);
            Assert.Equal(2740, futuresOptionContract.StrikePrice);
            Assert.Equal("PUT", futuresOptionContract.OptionType);

            Assert.NotNull(logVM);
            Assert.Equal(logVM.Message, $"FuturesOptionContractAdded: {contractId}");
        }

  
        [Fact]
        public async Task FuturesOptionContractAddedEventWithError()
        {
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);

            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFuturesOptionContractAsync(It.IsAny<FuturesOptionContractAddedEvent>()))
                .Throws<InvalidOperationException>();

            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            var contractMonth = DateTime.Now.Date;
            var contractId = $"BT{contractMonth.ToString("yyyyMMdd")}P2740";
            var contractAddedEvent = new FuturesOptionContractAddedEvent
            {
                Contract = new FuturesOptionContractReadModel
                (
                    contractId: contractId,
                    description: "Unit Test Futures Option Contract",
                    symbol: "BT",
                    localSymbol: "BT99",
                    securityType: "FOP",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    contractMonth: contractMonth,
                    strikePrice: 2740,
                    optionType: "PUT"
                ),
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt"
            };
            await mdEvents.ExecuteAsync(contractAddedEvent);

            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
        }

        [Fact]
        public async Task FuturesOptionContractChangedEventOk()
        {
            var futuresOptionContract = default(FuturesOptionContractReadModel);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                 .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                 .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                 .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFuturesOptionContractAsync(It.IsAny<FuturesOptionContractAddedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FuturesOptionContractAddedEvent>(e => futuresOptionContract = e.Contract);

            var mdEvents = new MarketDataEvents( mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            var contractMonth = DateTime.Now.Date;
            var contractId = $"BT{contractMonth.ToString("yyyyMMdd")}P2740";

            // added test contract...
            var contratAddedEvent = new FuturesOptionContractAddedEvent
            {
                Contract = new FuturesOptionContractReadModel
                (
                    contractId: contractId,
                    description: "Unit Test Futures Option Contract",
                    symbol: "BT",
                    localSymbol: "BT99",
                    securityType: "FOP",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    contractMonth: contractMonth,
                    strikePrice: 2740,
                    optionType: "PUT"
                ),
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt"
            };
            await mdEvents.ExecuteAsync(contratAddedEvent);

            Assert.NotNull(futuresOptionContract);
            Assert.Equal(contractId, futuresOptionContract.ContractId);
            Assert.Equal("Unit Test Futures Option Contract", futuresOptionContract.Description);
            Assert.Equal("BT", futuresOptionContract.Symbol );
            Assert.Equal("BT99", futuresOptionContract.LocalSymbol);
            Assert.Equal("FOP", futuresOptionContract.SecurityType);
            Assert.Equal("USD", futuresOptionContract.Currency);
            Assert.Equal("GLOBEX", futuresOptionContract.Exchange);
            Assert.Equal("50", futuresOptionContract.Multiplier);
            Assert.Equal(contractMonth.Year, futuresOptionContract.ContractMonth.Year);
            Assert.Equal(contractMonth.Month, futuresOptionContract.ContractMonth.Month);
            Assert.Equal(contractMonth.Day, futuresOptionContract.ContractMonth.Day);
            Assert.Equal(2740, futuresOptionContract.StrikePrice);
            Assert.Equal("PUT", futuresOptionContract.OptionType);

            // change single value...
            var contractChangedEvent = new FuturesOptionContractChangedEvent
            {
                OriginalContractId = contractId,
                Contract = new FuturesOptionContractReadModel
                (
                    contractId: contractId,
                    description: "Unit Test Futures Option Contract",
                    symbol: "BT",
                    localSymbol: "BT99",
                    securityType: "FOP",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    contractMonth: contractMonth,
                    strikePrice: 2740,
                    optionType: "PUT"
                ),
                UpdatedOn = DateTime.Now,
                UpdatedBy = "basilt"
            };

            mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.UpdateFuturesOptionContractAsync(It.IsAny<FuturesOptionContractChangedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FuturesOptionContractChangedEvent>(e => futuresOptionContract = e.Contract);

            mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            await mdEvents.ExecuteAsync(contractChangedEvent);

            // check changed value and all other values should have stayed the same...
            Assert.NotNull(futuresOptionContract);
            Assert.Equal(contractId, futuresOptionContract.ContractId);
            Assert.Equal("Changed Futures Option Test Contract", futuresOptionContract.Description);
            Assert.Equal("BT", futuresOptionContract.Symbol);
            Assert.Equal("BT99", futuresOptionContract.LocalSymbol);
            Assert.Equal("FOP", futuresOptionContract.SecurityType);
            Assert.Equal("USD", futuresOptionContract.Currency);
            Assert.Equal("GLOBEX", futuresOptionContract.Exchange);
            Assert.Equal("50", futuresOptionContract.Multiplier);
            Assert.Equal(DateTime.Now.Date.Year, futuresOptionContract.ContractMonth.Year);
            Assert.Equal(DateTime.Now.Date.Month, futuresOptionContract.ContractMonth.Month);
            Assert.Equal(DateTime.Now.Date.Day, futuresOptionContract.ContractMonth.Day);
            Assert.Equal(2740, futuresOptionContract.StrikePrice);
            Assert.Equal("PUT", futuresOptionContract.OptionType);

            Assert.NotNull(logVM);
            Assert.Equal($"FuturesOptionContractChanged: {contractId}", logVM.Message);
        }

        [Fact]
        public async Task FuturesOptionContractChangedEventWithError()
        {
            var futuresOptionContract = default(FuturesOptionContractReadModel);
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;

            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFuturesOptionContractAsync(It.IsAny<FuturesOptionContractAddedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FuturesOptionContractAddedEvent>(e => futuresOptionContract = e.Contract);

            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            var contractMonth = DateTime.Now.Date;
            var contractId = $"BT{contractMonth.ToString("yyyyMMdd")}P2740";

            // added test contract...
            var contratAddedEvent = new FuturesOptionContractAddedEvent
            {
                Contract = new FuturesOptionContractReadModel
                (
                    contractId: contractId,
                    description: "Unit Test Futures Option Contract",
                    symbol: "BT",
                    localSymbol: "BT99",
                    securityType: "FOP",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    contractMonth: contractMonth,
                    strikePrice: 2740,
                    optionType: "PUT"
                ),
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt"
            };
            await mdEvents.ExecuteAsync(contratAddedEvent);

            Assert.NotNull(futuresOptionContract);
            Assert.Equal(contractId, futuresOptionContract.ContractId);
            Assert.Equal("Unit Test Futures Option Contract", futuresOptionContract.Description);
            Assert.Equal("BT", futuresOptionContract.Symbol);
            Assert.Equal("BT99", futuresOptionContract.LocalSymbol);
            Assert.Equal("FOP", futuresOptionContract.SecurityType);
            Assert.Equal("USD", futuresOptionContract.Currency);
            Assert.Equal("GLOBEX", futuresOptionContract.Exchange);
            Assert.Equal("50", futuresOptionContract.Multiplier);
            Assert.Equal(contractMonth.Year, futuresOptionContract.ContractMonth.Year);
            Assert.Equal(contractMonth.Month, futuresOptionContract.ContractMonth.Month );
            Assert.Equal(contractMonth.Day, futuresOptionContract.ContractMonth.Day);
            Assert.Equal(2740, futuresOptionContract.StrikePrice);
            Assert.Equal("PUT", futuresOptionContract.OptionType);

            // change single value...
            var contractChangedEvent = new FuturesOptionContractChangedEvent
            {
                OriginalContractId = contractId,
                Contract = new FuturesOptionContractReadModel
                (
                    contractId: contractId,
                    description: "Unit Test Futures Option Contract",
                    symbol: "BT",
                    localSymbol: "BT99",
                    securityType: "FOP",
                    currency: "USD",
                    exchange: "GLOBEX",
                    multiplier: "50",
                    contractMonth: contractMonth,
                    strikePrice: 2740,
                    optionType: "PUT"
                ),
                UpdatedOn = DateTime.Now,
                UpdatedBy = "basilt"
            };

            mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.UpdateFuturesOptionContractAsync(It.IsAny<FuturesOptionContractChangedEvent>()))
                   .Throws<InvalidOperationException>();

            mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            await mdEvents.ExecuteAsync(contractChangedEvent);
            
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
        }

         [Fact]
        public async Task FuturesOptionContractRemovedEventOk()
        {
            var futuresOptionContractId = default(string);
            var logVM = default(StatusConsoleLogReadModel);

            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.DeleteFuturesOptionContractAsync(It.IsAny<FuturesOptionContractRemovedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FuturesOptionContractRemovedEvent>(e => futuresOptionContractId = e.ContractId);

            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            var contractMonth = DateTime.Now.Date;
            var contractId = $"BT{contractMonth.ToString("yyyyMMdd")}P2740";

            var contractRemovedEvent = new FuturesOptionContractRemovedEvent
            {
                ContractId = contractId,
                DeletedOn = DateTime.Now,
                DeletedBy = "basilt"
            };

            await mdEvents.ExecuteAsync(contractRemovedEvent);
            Assert.NotNull(futuresOptionContractId);
            Assert.Equal(futuresOptionContractId, contractId);
            Assert.NotNull(logVM);
            Assert.Equal(logVM.Message, $"FuturesOptionContractRemoved: {contractId}");
        }

        [Fact]
        public async Task FuturesOptionContractRemovedEventWithError()
        {
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
            var contractMonth = DateTime.Now.Date;
            var contractId = $"BT{contractMonth.ToString("yyyyMMdd")}P2740";

            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                 .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                 .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                 .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.DeleteFuturesOptionContractAsync(It.IsAny<FuturesOptionContractRemovedEvent>()))
                   .Throws<InvalidOperationException>();

            var contractRemovedEvent = new FuturesOptionContractRemovedEvent
            {
                ContractId = contractId,
                DeletedOn = DateTime.Now,
                DeletedBy = "basilt"
            };

            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            await mdEvents.ExecuteAsync(contractRemovedEvent);

            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
        }

        [Fact]
        public async Task YieldCurveRateAddedEventOk()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var addedYCRate = default(YieldCurveRateReadModel);

            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
               .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
               .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
               .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertYieldCurveRateAsync(It.IsAny<YieldCurveRateAddedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<YieldCurveRateAddedEvent>(e => addedYCRate = e.YieldCurveRate);


            var mdEvents = new MarketDataEvents( mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);

            var valueDate = DateTime.Now.Date;
            var yieldCurveRate = new YieldCurveRateReadModel
            (
                valueDate: DateTime.Now.Date,
                oneMonth: 2.45,
                twoMonth: 2.55,
                threeMonth: 2.66,
                sixMonth: 2.77,
                oneYear: 2.78,
                twoYear: 2.88,
                threeYear: 2.99,
                fiveYear: 3.11,
                sevenYear: 3.22,
                tenYear: 3.33,
                twentyYear: 3.44,
                thirtyYear: 3.55
            );
            var yieldCurveRateAddedEvent = new YieldCurveRateAddedEvent
            {
                YieldCurveRate = yieldCurveRate,
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt"
            };

            await mdEvents.ExecuteAsync(yieldCurveRateAddedEvent);
            Assert.NotNull(addedYCRate);
            Assert.Equal(yieldCurveRate.ValueDate, addedYCRate.ValueDate);
            Assert.Equal(addedYCRate.OneMonth.ToString("F2"), yieldCurveRate.OneMonth.ToString("F2"));
            Assert.Equal(addedYCRate.TwoMonth.ToString("F2"), yieldCurveRate.TwoMonth.ToString("F2"));
            Assert.Equal(addedYCRate.ThreeMonth.ToString("F2"), yieldCurveRate.ThreeMonth.ToString("F2"));
            Assert.Equal(addedYCRate.SixMonth.ToString("F2"), yieldCurveRate.SixMonth.ToString("F2"));
            Assert.Equal(addedYCRate.OneYear.ToString("F2"), yieldCurveRate.OneYear.ToString("F2"));
            Assert.Equal(addedYCRate.TwoYear.ToString("F2"), yieldCurveRate.TwoYear.ToString("F2"));
            Assert.Equal(addedYCRate.ThreeYear.ToString("F2"), yieldCurveRate.ThreeYear.ToString("F2"));
            Assert.Equal(addedYCRate.FiveYear.ToString("F2"), yieldCurveRate.FiveYear.ToString("F2"));
            Assert.Equal(addedYCRate.SevenYear.ToString("F2"), yieldCurveRate.SevenYear.ToString("F2"));
            Assert.Equal(addedYCRate.TenYear.ToString("F2"), yieldCurveRate.TenYear.ToString("F2"));
            Assert.Equal(addedYCRate.TwentyYear.ToString("F2"), yieldCurveRate.TwentyYear.ToString("F2"));
            Assert.Equal(addedYCRate.ThirtyYear.ToString("F2"), yieldCurveRate.ThirtyYear.ToString("F2"));

            Assert.NotNull(logVM);
            Assert.Equal(logVM.Message, $"YieldCurveRateAdded: {yieldCurveRate.ValueDate.ToString("yyyy-MM-dd")}");
        }

        [Fact]
        public async Task YieldCurveRateAddedEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;

            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertYieldCurveRateAsync(It.IsAny<YieldCurveRateAddedEvent>()))
                .Throws<InvalidOperationException>();
  
            var mdEvents = new MarketDataEvents( mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            var valueDate = DateTime.Now.Date;
            var yieldCurveRate = new YieldCurveRateReadModel
            (
                valueDate: DateTime.Now.Date,
                oneMonth: 2.45,
                twoMonth: 2.55,
                threeMonth: 2.66,
                sixMonth: 2.77,
                oneYear: 2.78,
                twoYear: 2.88,
                threeYear: 2.99,
                fiveYear: 3.11,
                sevenYear: 3.22,
                tenYear: 3.33,
                twentyYear: 3.44,
                thirtyYear: 3.55
            );
            var yieldCurveRateAddedEvent = new YieldCurveRateAddedEvent
            {
                YieldCurveRate = yieldCurveRate,
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt"
            };

            await mdEvents.ExecuteAsync(yieldCurveRateAddedEvent);

            Assert.NotNull(logVM);
            Assert.Equal(logVM.Message, errorMsg);
        }

        [Fact]
        public async Task YieldCurveRateChangedEventOk()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var changedYCRate = default(YieldCurveRateReadModel);

            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
               .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
               .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
               .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.UpdateYieldCurveRateAsync(It.IsAny<YieldCurveRateChangedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<YieldCurveRateChangedEvent>(e => changedYCRate = e.YieldCurveRate);


            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);

            var valueDate = DateTime.Now.Date;
            var yieldCurveRate = new YieldCurveRateReadModel
            (
                valueDate: DateTime.Now.Date,
                oneMonth: 2.45,
                twoMonth: 2.55,
                threeMonth: 2.66,
                sixMonth: 2.77,
                oneYear: 2.78,
                twoYear: 2.88,
                threeYear: 2.99,
                fiveYear: 3.11,
                sevenYear: 3.22,
                tenYear: 3.33,
                twentyYear: 3.44,
                thirtyYear: 3.55
            );
            var yieldCurveRateChangedEvent = new YieldCurveRateChangedEvent
            {
                YieldCurveRate = yieldCurveRate,
                UpdatedOn = DateTime.Now,
                UpdatedBy = "basilt"
            };

            await mdEvents.ExecuteAsync(yieldCurveRateChangedEvent);
            Assert.NotNull(changedYCRate);
            Assert.Equal(yieldCurveRate.ValueDate, changedYCRate.ValueDate);
            Assert.Equal(changedYCRate.OneMonth.ToString("F2"), yieldCurveRate.OneMonth.ToString("F2"));
            Assert.Equal(changedYCRate.TwoMonth.ToString("F2"), yieldCurveRate.TwoMonth.ToString("F2"));
            Assert.Equal(changedYCRate.ThreeMonth.ToString("F2"), yieldCurveRate.ThreeMonth.ToString("F2"));
            Assert.Equal(changedYCRate.SixMonth.ToString("F2"), yieldCurveRate.SixMonth.ToString("F2"));
            Assert.Equal(changedYCRate.OneYear.ToString("F2"), yieldCurveRate.OneYear.ToString("F2"));
            Assert.Equal(changedYCRate.TwoYear.ToString("F2"), yieldCurveRate.TwoYear.ToString("F2"));
            Assert.Equal(changedYCRate.ThreeYear.ToString("F2"), yieldCurveRate.ThreeYear.ToString("F2"));
            Assert.Equal(changedYCRate.FiveYear.ToString("F2"), yieldCurveRate.FiveYear.ToString("F2"));
            Assert.Equal(changedYCRate.SevenYear.ToString("F2"), yieldCurveRate.SevenYear.ToString("F2"));
            Assert.Equal(changedYCRate.TenYear.ToString("F2"), yieldCurveRate.TenYear.ToString("F2"));
            Assert.Equal(changedYCRate.TwentyYear.ToString("F2"), yieldCurveRate.TwentyYear.ToString("F2"));
            Assert.Equal(changedYCRate.ThirtyYear.ToString("F2"), yieldCurveRate.ThirtyYear.ToString("F2"));

            Assert.NotNull(logVM);
            Assert.Equal(logVM.Message, $"YieldCurveRateChanged: {yieldCurveRate.ValueDate.ToString("yyyy-MM-dd")}");
        }

        [Fact]
        public async Task YieldCurveRateChangedEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;

            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.UpdateYieldCurveRateAsync(It.IsAny<YieldCurveRateChangedEvent>()))
                .Throws<InvalidOperationException>();

            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            var valueDate = DateTime.Now.Date;
            var yieldCurveRate = new YieldCurveRateReadModel
            (
                valueDate: DateTime.Now.Date,
                oneMonth: 2.45,
                twoMonth: 2.55,
                threeMonth: 2.66,
                sixMonth: 2.77,
                oneYear: 2.78,
                twoYear: 2.88,
                threeYear: 2.99,
                fiveYear: 3.11,
                sevenYear: 3.22,
                tenYear: 3.33,
                twentyYear: 3.44,
                thirtyYear: 3.55
            );
            var yieldCurveRateChangedEvent = new YieldCurveRateChangedEvent
            {
                YieldCurveRate = yieldCurveRate,
                UpdatedOn = DateTime.Now,
                UpdatedBy = "basilt"
            };
            await mdEvents.ExecuteAsync(yieldCurveRateChangedEvent);

            Assert.NotNull(logVM);
            Assert.Equal(logVM.Message, errorMsg);
        }

        [Fact]
        public async Task YieldCurveRateRemovedEventOk()
        {
            var ycrValueDate = default(DateTime);
            var logVM = default(StatusConsoleLogReadModel);

            // given...
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.DeleteYieldCurveRateAsync(It.IsAny<YieldCurveRateRemovedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<YieldCurveRateRemovedEvent>(e => ycrValueDate = e.ValueDate);

            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            var valueDate = DateTime.Now.Date;
            var yieldCurveRateRemoved = new YieldCurveRateRemovedEvent
            {
                ValueDate = valueDate,
                DeletedOn = DateTime.Now,
                DeletedBy = "basilt"
            };

            // when...
            await mdEvents.ExecuteAsync(yieldCurveRateRemoved);

            // then...
            Assert.Equal(ycrValueDate.Year, valueDate.Year);
            Assert.Equal(ycrValueDate.Month, valueDate.Month);
            Assert.Equal(ycrValueDate.Day, valueDate.Day);
            Assert.NotNull(logVM);
            Assert.Equal(logVM.Message, $"YieldCurveRateRemoved: {ycrValueDate.ToString("yyyy-MM-dd")}");
        }

        [Fact]
        public async Task YieldCurveRateRemovedEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;

            // given...
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.DeleteYieldCurveRateAsync(It.IsAny<YieldCurveRateRemovedEvent>()))
                .Throws<InvalidOperationException>();
 
            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            var valueDate = DateTime.Now.Date;
            var yieldCurveRateRemoved = new YieldCurveRateRemovedEvent
            {
                ValueDate = valueDate,
                DeletedOn = DateTime.Now,
                DeletedBy = "basilt"
            };

            // when...
            await mdEvents.ExecuteAsync(yieldCurveRateRemoved);

            // then...
            Assert.NotNull(logVM);
            Assert.Equal(mdEvents.ErrorCode, logVM.StatusCode);
            Assert.Equal(errorMsg, logVM.Message);
        }

        [Fact]
        public async Task YieldCurveRatesImportedEventOk()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var importedYCRates = default(YieldCurveRateReadModel[]);

            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
               .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
               .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
               .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertYieldCurveRatesAsync(It.IsAny<YieldCurveRatesImportedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<YieldCurveRatesImportedEvent>(e => importedYCRates = e.YieldCurveRates);

            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            var valueDate = DateTime.Now.Date;
            var yieldCurveRate = new YieldCurveRateReadModel
            (
                valueDate: DateTime.Now.Date,
                oneMonth: 2.45,
                twoMonth: 2.55,
                threeMonth: 2.66,
                sixMonth: 2.77,
                oneYear: 2.78,
                twoYear: 2.88,
                threeYear: 2.99,
                fiveYear: 3.11,
                sevenYear: 3.22,
                tenYear: 3.33,
                twentyYear: 3.44,
                thirtyYear: 3.55
            );
            var yieldCurveRatesImported = new YieldCurveRatesImportedEvent
            {
                ImportDate = DateTime.Now,
                YieldCurveRates = new YieldCurveRateReadModel[] { yieldCurveRate },
                ImportedOn = DateTime.Now,
                ImportedBy = "basilt"
            };

            await mdEvents.ExecuteAsync(yieldCurveRatesImported);
            Assert.NotNull(importedYCRates);
            Assert.True(importedYCRates.Length == 1);
            Assert.Equal(importedYCRates[0].OneMonth.ToString("F2"), yieldCurveRate.OneMonth.ToString("F2"));
            Assert.Equal(importedYCRates[0].TwoMonth.ToString("F2"), yieldCurveRate.TwoMonth.ToString("F2"));
            Assert.Equal(importedYCRates[0].ThreeMonth.ToString("F2"), yieldCurveRate.ThreeMonth.ToString("F2"));
            Assert.Equal(importedYCRates[0].SixMonth.ToString("F2"), yieldCurveRate.SixMonth.ToString("F2"));
            Assert.Equal(importedYCRates[0].OneYear.ToString("F2"), yieldCurveRate.OneYear.ToString("F2"));
            Assert.Equal(importedYCRates[0].TwoYear.ToString("F2"), yieldCurveRate.TwoYear.ToString("F2"));
            Assert.Equal(importedYCRates[0].ThreeYear.ToString("F2"), yieldCurveRate.ThreeYear.ToString("F2"));
            Assert.Equal(importedYCRates[0].FiveYear.ToString("F2"), yieldCurveRate.FiveYear.ToString("F2"));
            Assert.Equal(importedYCRates[0].SevenYear.ToString("F2"), yieldCurveRate.SevenYear.ToString("F2"));
            Assert.Equal(importedYCRates[0].TenYear.ToString("F2"), yieldCurveRate.TenYear.ToString("F2"));
            Assert.Equal(importedYCRates[0].TwentyYear.ToString("F2"), yieldCurveRate.TwentyYear.ToString("F2"));
            Assert.Equal(importedYCRates[0].ThirtyYear.ToString("F2"), yieldCurveRate.ThirtyYear.ToString("F2"));

            Assert.NotNull(logVM);
            Assert.Equal(0, logVM.StatusCode);
        }

        [Fact]
        public async Task YieldCurveRatesImportedEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;

            // given...
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IMarketDataEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertYieldCurveRatesAsync(It.IsAny<YieldCurveRatesImportedEvent>()))
                .Throws<InvalidOperationException>();

            var mdEvents = new MarketDataEvents(mockStatusConsoleServiceApi.Object, mockEventDenormalizer.Object);
            var valueDate = DateTime.Now.Date;
            var yieldCurveRate = new YieldCurveRateReadModel
            (
                valueDate: DateTime.Now.Date,
                oneMonth: 2.45,
                twoMonth: 2.55,
                threeMonth: 2.66,
                sixMonth: 2.77,
                oneYear: 2.78,
                twoYear: 2.88,
                threeYear: 2.99,
                fiveYear: 3.11,
                sevenYear: 3.22,
                tenYear: 3.33,
                twentyYear: 3.44,
                thirtyYear: 3.55
            );
            var yieldCurveRatesImported = new YieldCurveRatesImportedEvent
            {
                ImportDate = DateTime.Now,
                YieldCurveRates = new YieldCurveRateReadModel[] { yieldCurveRate },
                ImportedOn = DateTime.Now,
                ImportedBy = "basilt"
            };

            // when...
            await mdEvents.ExecuteAsync(yieldCurveRatesImported);

            // then...
            Assert.NotNull(logVM);
            Assert.Equal(mdEvents.ErrorCode, logVM.StatusCode);
            Assert.Equal(errorMsg, logVM.Message);
        }
    }
}
