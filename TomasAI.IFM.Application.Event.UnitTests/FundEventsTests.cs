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
using TomasAI.IFM.Shared.Fund;
using TomasAI.IFM.Shared.Fund.Events;
using TomasAI.IFM.Shared.Fund.ViewModels;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Application.Event.UnitTests
{
    public class FundEventsTests
    {
        [Fact]
        public async Task FundCreatedEventOk()
        {
            var fund = default(FundReadModel);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundAsync(It.IsAny<FundCreatedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FundCreatedEvent>(e => fund = new FundReadModel (
                    fundId: e.FundId,
                    name: e.Name,
                    description: e.Description,
                    balance: e.InitialBalance,
                    createdBy: e.CreatedBy,
                    createdOn: e.CreatedOn
                ));

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object );
            var fundCreated = new FundCreatedEvent
            {
                FundId = 1001,
                Name = "Big Fund",
                Description = "Big Fund Description",
                InitialBalance = 1234.56m,
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt"
            };
            await fundEvents.ExecuteAsync(fundCreated);

            Assert.NotNull(fund);
            Assert.Equal(1001, fund.FundId);
            Assert.Equal("Big Fund", fund.Name);
            Assert.Equal("Big Fund Description", fund.Description);
            Assert.Equal(1234.56m, fund.Balance);

        }

        [Fact]
        public async Task FundCreatedEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;

            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundAsync(It.IsAny<FundCreatedEvent>()))
                .Throws<InvalidOperationException>();

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var fundCreated = new FundCreatedEvent
            {
                FundId = 1001,
                Name = "Big Fund",
                Description = "Big Fund Description",
                InitialBalance = 1234.56m,
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt"
            };
            await fundEvents.ExecuteAsync(fundCreated);

            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(fundEvents.ErrorCode, logVM.StatusCode);

        }

        [Fact]
        public async Task OrderAddedToFundEventOk()
        {
            var fundOrder = default(FundOrderReadModel);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundOrderAsync(It.IsAny<OrderAddedToFundEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<OrderAddedToFundEvent>(e => fundOrder = new FundOrderReadModel(
                    fundId: e.FundId,
                    orderId: e.OrderId,
                    orderDate: e.OrderDate,
                    orderStatus: e.OrderStatus,
                    reference: e.Reference,
                    createdBy: e.CreatedBy,
                    createdOn: e.CreatedOn,
                    updatedBy: e.UpdatedBy,
                    updatedOn: e.UpdatedOn
                ));

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var fundCreated = new OrderAddedToFundEvent
            {
                FundId = 1001,
                OrderId = 2002,
                OrderStatus = OrderStatus.Open,
                Reference = "Reference Field",
                OrderDate = new DateTime(2019,10,10),
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt",
                UpdatedOn = DateTime.Now,
                UpdatedBy = "basilt"
            };
            await fundEvents.ExecuteAsync(fundCreated);

            Assert.NotNull(fundOrder);
            Assert.Equal(1001, fundOrder.FundId);
            Assert.Equal(OrderStatus.Open, fundOrder.OrderStatus);
            Assert.Equal("Reference Field", fundOrder.Reference);
            Assert.Equal(2019, fundOrder.OrderDate.Year);
            Assert.Equal(10, fundOrder.OrderDate.Month);
            Assert.Equal(10, fundOrder.OrderDate.Day);

        }

        [Fact]
        public async Task OrderAddedToFundEventWithError()
        {
            var logVM = default(StatusConsoleLogReadModel);
            var errorMsg = (new InvalidOperationException()).Message;

            // given...
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundOrderAsync(It.IsAny<OrderAddedToFundEvent>()))
                .Throws<InvalidOperationException>();

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var fundCreated = new OrderAddedToFundEvent
            {
                FundId = 1001,
                OrderId = 2002,
                OrderStatus = OrderStatus.Open,
                Reference = "Reference Field",
                OrderDate = new DateTime(2019, 10, 10),
                CreatedOn = DateTime.Now,
                CreatedBy = "basilt",
                UpdatedOn = DateTime.Now,
                UpdatedBy = "basilt"
            };

            // when...
            await fundEvents.ExecuteAsync(fundCreated);

            // then...
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(fundEvents.ErrorCode, logVM.StatusCode);
        }

        [Fact]
        public async Task OrderRemovedFromFundEventOk()
        {
            var orderId = default(int);
            var fundId = default(int);
            var removedOn = default(DateTime);
            var removedBy = default(string);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.DeleteFundOrderAsync(It.IsAny<OrderRemovedFromFundEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<OrderRemovedFromFundEvent>(e => {
                    fundId = e.FundId;
                    orderId = e.OrderId;
                    removedOn = e.RemovedOn;
                    removedBy = e.RemovedBy;
                });

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var testDate = DateTime.Now;
            var fundOrderRemoved = new OrderRemovedFromFundEvent
            {
                FundId = 1001,
                OrderId = 2002,
                RemovedOn = testDate,
                RemovedBy = "basilt"
            };
            await fundEvents.ExecuteAsync(fundOrderRemoved);

            Assert.Equal(1001, fundId);
            Assert.Equal(2002, orderId);
            Assert.Equal("basilt", removedBy);
            Assert.Equal(testDate.Year, removedOn.Year);
            Assert.Equal(testDate.Month, removedOn.Month);
            Assert.Equal(testDate.Day, removedOn.Day);

        }

        [Fact]
        public async Task OrderRemovedFromFundEventWithError()
        {
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.DeleteFundOrderAsync(It.IsAny<OrderRemovedFromFundEvent>()))
                .Throws<InvalidOperationException>();

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var testDate = DateTime.Now;
            var fundOrderRemoved = new OrderRemovedFromFundEvent
            {
                FundId = 1001,
                OrderId = 2002,
                RemovedOn = testDate,
                RemovedBy = "basilt"
            };
            await fundEvents.ExecuteAsync(fundOrderRemoved);

            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(fundEvents.ErrorCode, logVM.StatusCode);

        }

        [Fact]
        public async Task TradeAddedToFundOrderEventOk()
        {
            var fundId = default(int);
            var fundOrderTrade = default(FundOrderTradeReadModel);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundOrderTradeAsync(It.IsAny<TradeAddedToFundOrderEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<TradeAddedToFundOrderEvent>(e => {
                    fundId = e.FundId;
                    fundOrderTrade = e.FundOrderTrade;
                });

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var fundOrderTradeAdded = new TradeAddedToFundOrderEvent
            {
                FundId = 1001,
                FundOrderTrade = new FundOrderTradeReadModel
                (
                    orderId: 2002,
                    tradeId: 3003,
                    tradeType: TradeType.IronCondor,
                    tradeDate: new DateTime(2019,10,10),
                    maturityDate: new DateTime(2019,10,20),
                    tradeState: TradeState.NewTrade,
                    tradeAction: TradeAction.Buy,
                    reference: "FundOrderTrade Reference",
                    primaryTrade: true,
                    createdOn: DateTime.Now,
                    createdBy: "basilt"
                )
            };
            await fundEvents.ExecuteAsync(fundOrderTradeAdded);

            Assert.Equal(1001, fundId);
            Assert.Equal(2002, fundOrderTrade.OrderId);
            Assert.Equal(3003, fundOrderTrade.TradeId);
            Assert.Equal(TradeType.IronCondor, fundOrderTrade.TradeType);
            Assert.Equal(2019, fundOrderTrade.TradeDate.Year);
            Assert.Equal(10, fundOrderTrade.TradeDate.Month);
            Assert.Equal(10, fundOrderTrade.TradeDate.Day);
            Assert.Equal(2019, fundOrderTrade.MaturityDate.Year);
            Assert.Equal(10, fundOrderTrade.MaturityDate.Month);
            Assert.Equal(20, fundOrderTrade.MaturityDate.Day);
            Assert.Equal(TradeState.NewTrade, fundOrderTrade.TradeState);
            Assert.Equal(TradeAction.Buy, fundOrderTrade.TradeAction);
            Assert.Equal("FundOrderTrade Reference", fundOrderTrade.Reference);
            Assert.True(fundOrderTrade.PrimaryTrade);

        }

        [Fact]
        public async Task TradeAddedToFundOrderEventWithError()
        {
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundOrderTradeAsync(It.IsAny<TradeAddedToFundOrderEvent>()))
                .Throws<InvalidOperationException>();

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var fundOrderTradeAdded = new TradeAddedToFundOrderEvent
            {
                FundId = 1001,
                FundOrderTrade = TestDataDefaults.FundOrderTrade
            };
            await fundEvents.ExecuteAsync(fundOrderTradeAdded);

            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(fundEvents.ErrorCode, logVM.StatusCode);
        }

        [Fact]
        public async Task TradeRemovedFromFundOrderEventOk()
        {
            var fundId = default(int);
            var orderId = default(int);
            var tradeId = default(int);
            var removedBy = default(string);
            var removedOn = default(DateTime);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.DeleteFundOrderTradeAsync(It.IsAny<TradeRemovedFromFundOrderEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<TradeRemovedFromFundOrderEvent>(e => {
                    fundId = e.FundId;
                    orderId = e.OrderId;
                    tradeId = e.TradeId;
                    removedBy = e.RemovedBy;
                    removedOn = e.RemovedOn;
                });

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var fundOrderTradeRemoved = new TradeRemovedFromFundOrderEvent
            {
                FundId = 1001,
                OrderId = 2002,
                TradeId = 3003,
                RemovedBy = "basilt",
                RemovedOn = new DateTime(2019,10,10)
            };
            await fundEvents.ExecuteAsync(fundOrderTradeRemoved);

            Assert.Equal(1001, fundId);
            Assert.Equal(2002, orderId);
            Assert.Equal(3003, tradeId);
            Assert.Equal("basilt", removedBy);
            Assert.Equal(2019, removedOn.Year);
            Assert.Equal(10, removedOn.Month);
            Assert.Equal(10, removedOn.Day);

        }

        [Fact]
        public async Task TradeRemovedFromFundOrderEventWithError()
        {
            // given...
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.DeleteFundOrderTradeAsync(It.IsAny<TradeRemovedFromFundOrderEvent>()))
                .Throws<InvalidOperationException>();

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var fundOrderTradeRemoved = new TradeRemovedFromFundOrderEvent
            {
                FundId = 1001,
                OrderId = 2002,
                TradeId = 3003,
                RemovedBy = "basilt",
                RemovedOn = new DateTime(2019, 10, 10)
            };

            // when...
            await fundEvents.ExecuteAsync(fundOrderTradeRemoved);

            // then...
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(fundEvents.ErrorCode, logVM.StatusCode);

        }

        [Fact]
        public async Task FundOrderTradeStateChangedEventOk()
        {
            var fundId = default(int);
            var orderId = default(int);
            var tradeId = default(int);
            var tradeState = TradeState.NewTrade;
            var updatedBy = default(string);
            var updatedOn = default(DateTime);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.UpdateFundOrderTradeStateAsync(It.IsAny<FundOrderTradeStateChangedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FundOrderTradeStateChangedEvent>(e => {
                    fundId = e.FundId;
                    orderId = e.OrderId;
                    tradeId = e.TradeId;
                    tradeState = e.TradeState;
                    updatedBy = e.UpdatedBy;
                    updatedOn = e.UpdatedOn;
                });

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var tradeStateChanged = new FundOrderTradeStateChangedEvent
            {
                FundId = 1001,
                OrderId = 2002,
                TradeId = 3003,
                TradeState = TradeState.OrderCancelled,
                UpdatedBy = "basilt",
                UpdatedOn = new DateTime(2019, 10, 10)
            };
            await fundEvents.ExecuteAsync(tradeStateChanged);

            Assert.Equal(1001, fundId);
            Assert.Equal(2002, orderId);
            Assert.Equal(3003, tradeId);
            Assert.Equal("basilt", updatedBy);
            Assert.Equal(2019, updatedOn.Year);
            Assert.Equal(10, updatedOn.Month);
            Assert.Equal(10, updatedOn.Day);
            Assert.Equal(TradeState.OrderCancelled, tradeState);

        }

        [Fact]
        public async Task FundOrderTradeStateChangedEventWithError()
        {
            // given...
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.UpdateFundOrderTradeStateAsync(It.IsAny<FundOrderTradeStateChangedEvent>()))
                .Throws<InvalidOperationException>();

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var tradeStateChanged = new FundOrderTradeStateChangedEvent
            {
                FundId = 1001,
                OrderId = 2002,
                TradeId = 3003,
                TradeState = TradeState.OrderCancelled,
                UpdatedBy = "basilt",
                UpdatedOn = new DateTime(2019, 10, 10)
            };

            // when...
            await fundEvents.ExecuteAsync(tradeStateChanged);

            // then...
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(fundEvents.ErrorCode, logVM.StatusCode);

        }

        [Fact]
        public async Task OpeningTradeFundTransactionCreatedEventOk()
        {
            var fundTransaction = default(FundTransactionReadModel);
            var createdBy = default(string);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<OpeningTradeFundTransactionCreatedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<OpeningTradeFundTransactionCreatedEvent>(e => {
                    fundTransaction = e.FundTransaction;
                    createdBy = e.CreatedBy;
                });

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var openingTradeFundTransactionCreated = new OpeningTradeFundTransactionCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.OpeningTrade),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 12, 12)
            };
            await fundEvents.ExecuteAsync(openingTradeFundTransactionCreated);
            Assert.NotNull(fundTransaction);
            Assert.Equal(1001, fundTransaction.TransactionId);
            Assert.Equal(2019, fundTransaction.TransactionDate.Year);
            Assert.Equal(10, fundTransaction.TransactionDate.Month);
            Assert.Equal(10, fundTransaction.TransactionDate.Day);
            Assert.Equal(FundTransactionType.OpeningTrade, fundTransaction.TransactionType);
            Assert.Equal(1004, fundTransaction.FundId);
            Assert.Equal(2002, fundTransaction.OrderId);
            Assert.Equal(3003, fundTransaction.TradeId);
            Assert.Equal(TradeType.IronCondor, fundTransaction.TradeType);
            Assert.Equal(2019, fundTransaction.ValueDate.Year);
            Assert.Equal(10, fundTransaction.ValueDate.Month);
            Assert.Equal(10, fundTransaction.ValueDate.Day);
            Assert.Equal(TradeStatus.IntraDay, fundTransaction.TradeStatus);

        }

        [Fact]
        public async Task OpeningTradeFundTransactionCreatedEventWithError()
        {
            // given...
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<OpeningTradeFundTransactionCreatedEvent>()))
                .Throws<InvalidOperationException>();

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var openingTradeFundTransactionCreated = new OpeningTradeFundTransactionCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.OpeningTrade),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019,12,12)
            };

            // when...
            await fundEvents.ExecuteAsync(openingTradeFundTransactionCreated);

            // then...
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(fundEvents.ErrorCode, logVM.StatusCode);

        }

        [Fact]
        public async Task OpeningTradeFundTransactionAdjustmentCreatedEventOk()
        {
            var fundTransaction = default(FundTransactionReadModel);
            var createdBy = default(string);
            var createdOn = default(DateTime);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<OpeningTradeFundTransactionAdjustmentCreatedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<OpeningTradeFundTransactionAdjustmentCreatedEvent>(e => {
                    fundTransaction = e.FundTransaction;
                    createdBy = e.CreatedBy;
                    createdOn = e.CreatedOn;
                });

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var openingTradeFundTransactionAdjustmentCreated = new OpeningTradeFundTransactionAdjustmentCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.OpeningTradeAdjustment),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019,12,12)
            };
            await fundEvents.ExecuteAsync(openingTradeFundTransactionAdjustmentCreated);
            Assert.NotNull(fundTransaction);
            Assert.Equal(1001, fundTransaction.TransactionId);
            Assert.Equal(2019, fundTransaction.TransactionDate.Year);
            Assert.Equal(10, fundTransaction.TransactionDate.Month);
            Assert.Equal(10, fundTransaction.TransactionDate.Day);
            Assert.Equal(FundTransactionType.OpeningTradeAdjustment, fundTransaction.TransactionType);
            Assert.Equal(1004, fundTransaction.FundId);
            Assert.Equal(2002, fundTransaction.OrderId);
            Assert.Equal(3003, fundTransaction.TradeId);
            Assert.Equal(TradeType.IronCondor, fundTransaction.TradeType);
            Assert.Equal(2019, fundTransaction.ValueDate.Year);
            Assert.Equal(10, fundTransaction.ValueDate.Month);
            Assert.Equal(10, fundTransaction.ValueDate.Day);
            Assert.Equal(TradeStatus.IntraDay, fundTransaction.TradeStatus);
            Assert.Equal("basilt", createdBy);
            Assert.Equal(2019, createdOn.Year);
            Assert.Equal(12, createdOn.Month);
            Assert.Equal(12, createdOn.Day);

        }

        [Fact]
        public async Task OpeningTradeFundTransactionAdjustmentCreatedEventWithError()
        {
            // given...
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<OpeningTradeFundTransactionAdjustmentCreatedEvent>()))
                .Throws<InvalidOperationException>();

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var openingTradeFundTransactionAdjustmentCreated = new OpeningTradeFundTransactionAdjustmentCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.OpeningTradeAdjustment),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 12, 12)
            };

            // when...
            await fundEvents.ExecuteAsync(openingTradeFundTransactionAdjustmentCreated);

            // then...
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(fundEvents.ErrorCode, logVM.StatusCode);

        }

        [Fact]
        public async Task TradeCommissionFundTransactionCreatedEventOk()
        {
            var fundTransaction = default(FundTransactionReadModel);
            var createdBy = default(string);
            var createdOn = default(DateTime);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<TradeCommissionFundTransactionCreatedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<TradeCommissionFundTransactionCreatedEvent>(e => {
                    fundTransaction = e.FundTransaction;
                    createdBy = e.CreatedBy;
                    createdOn = e.CreatedOn;
                });

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var tradeCommissionFundTransactionCreated = new TradeCommissionFundTransactionCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.TradeCommission),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 12, 12)
            };
            await fundEvents.ExecuteAsync(tradeCommissionFundTransactionCreated);
            Assert.NotNull(fundTransaction);
            Assert.Equal(1001, fundTransaction.TransactionId);
            Assert.Equal(2019, fundTransaction.TransactionDate.Year);
            Assert.Equal(10, fundTransaction.TransactionDate.Month);
            Assert.Equal(10, fundTransaction.TransactionDate.Day);
            Assert.Equal(FundTransactionType.TradeCommission, fundTransaction.TransactionType);
            Assert.Equal(1004, fundTransaction.FundId);
            Assert.Equal(2002, fundTransaction.OrderId);
            Assert.Equal(3003, fundTransaction.TradeId);
            Assert.Equal(TradeType.IronCondor, fundTransaction.TradeType);
            Assert.Equal(2019, fundTransaction.ValueDate.Year);
            Assert.Equal(10, fundTransaction.ValueDate.Month);
            Assert.Equal(10, fundTransaction.ValueDate.Day);
            Assert.Equal(TradeStatus.IntraDay, fundTransaction.TradeStatus);
            Assert.Equal("basilt", createdBy);
            Assert.Equal(2019, createdOn.Year);
            Assert.Equal(12, createdOn.Month);
            Assert.Equal(12, createdOn.Day);

        }

        [Fact]
        public async Task TradeCommissionFundTransactionCreatedEventWithError()
        {
            // given...
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<TradeCommissionFundTransactionCreatedEvent>()))
                .Throws<InvalidOperationException>();

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var openingTradeFundTransactionAdjustmentCreated = new TradeCommissionFundTransactionCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.TradeCommission),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 12, 12)
            };

            // when...
            await fundEvents.ExecuteAsync(openingTradeFundTransactionAdjustmentCreated);

            // then...
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(fundEvents.ErrorCode, logVM.StatusCode);

        }

        [Fact]
        public async Task TradeCommissionFundTransactionAdjustmentCreatedEventOk()
        {
            var fundTransaction = default(FundTransactionReadModel);
            var createdBy = default(string);
            var createdOn = default(DateTime);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<TradeCommissionFundTransactionAdjustmentCreatedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<TradeCommissionFundTransactionAdjustmentCreatedEvent>(e => {
                    fundTransaction = e.FundTransaction;
                    createdBy = e.CreatedBy;
                    createdOn = e.CreatedOn;
                });

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var tradeCommissionFundTransactionAdjustmentCreated = new TradeCommissionFundTransactionAdjustmentCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.TradeCommissionAdjustment),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 12, 12)
            };
            await fundEvents.ExecuteAsync(tradeCommissionFundTransactionAdjustmentCreated);
            Assert.NotNull(fundTransaction);
            Assert.Equal(1001, fundTransaction.TransactionId);
            Assert.Equal(2019, fundTransaction.TransactionDate.Year);
            Assert.Equal(10, fundTransaction.TransactionDate.Month);
            Assert.Equal(10, fundTransaction.TransactionDate.Day);
            Assert.Equal(FundTransactionType.TradeCommissionAdjustment, fundTransaction.TransactionType);
            Assert.Equal(1004, fundTransaction.FundId);
            Assert.Equal(2002, fundTransaction.OrderId);
            Assert.Equal(3003, fundTransaction.TradeId);
            Assert.Equal(TradeType.IronCondor, fundTransaction.TradeType);
            Assert.Equal(2019, fundTransaction.ValueDate.Year);
            Assert.Equal(10, fundTransaction.ValueDate.Month);
            Assert.Equal(10, fundTransaction.ValueDate.Day);
            Assert.Equal(TradeStatus.IntraDay, fundTransaction.TradeStatus);
            Assert.Equal("basilt", createdBy);
            Assert.Equal(2019, createdOn.Year);
            Assert.Equal(12, createdOn.Month);
            Assert.Equal(12, createdOn.Day);

        }

        [Fact]
        public async Task TradeCommissionFundTransactionAdjustmentCreatedEventWithError()
        {
            // given...
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<TradeCommissionFundTransactionAdjustmentCreatedEvent>()))
                .Throws<InvalidOperationException>();

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var openingTradeFundTransactionAdjustmentCreated = new TradeCommissionFundTransactionAdjustmentCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.TradeCommissionAdjustment),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 12, 12)
            };

            // when...
            await fundEvents.ExecuteAsync(openingTradeFundTransactionAdjustmentCreated);

            // then...
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(fundEvents.ErrorCode, logVM.StatusCode);

        }

        [Fact]
        public async Task UnrealizedTradePnlFundTransactionCreatedEventOk()
        {
            var fundTransaction = default(FundTransactionReadModel);
            var createdBy = default(string);
            var createdOn = default(DateTime);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<UnrealizedTradePnlFundTransactionCreatedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<UnrealizedTradePnlFundTransactionCreatedEvent>(e => {
                    fundTransaction = e.FundTransaction;
                    createdBy = e.CreatedBy;
                    createdOn = e.CreatedOn;
                });

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var unrealizedTradePnlFundTransactionCreated = new UnrealizedTradePnlFundTransactionCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.UnrealizedTradePnl),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 12, 12)
            };
            await fundEvents.ExecuteAsync(unrealizedTradePnlFundTransactionCreated);
            Assert.NotNull(fundTransaction);
            Assert.Equal(1001, fundTransaction.TransactionId);
            Assert.Equal(2019, fundTransaction.TransactionDate.Year);
            Assert.Equal(10, fundTransaction.TransactionDate.Month);
            Assert.Equal(10, fundTransaction.TransactionDate.Day);
            Assert.Equal(FundTransactionType.UnrealizedTradePnl, fundTransaction.TransactionType);
            Assert.Equal(1004, fundTransaction.FundId);
            Assert.Equal(2002, fundTransaction.OrderId);
            Assert.Equal(3003, fundTransaction.TradeId);
            Assert.Equal(TradeType.IronCondor, fundTransaction.TradeType);
            Assert.Equal(2019, fundTransaction.ValueDate.Year);
            Assert.Equal(10, fundTransaction.ValueDate.Month);
            Assert.Equal(10, fundTransaction.ValueDate.Day);
            Assert.Equal(TradeStatus.IntraDay, fundTransaction.TradeStatus);
            Assert.Equal("basilt", createdBy);
            Assert.Equal(2019, createdOn.Year);
            Assert.Equal(12, createdOn.Month);
            Assert.Equal(12, createdOn.Day);

        }

        [Fact]
        public async Task UnrealizedTradePnlFundTransactionCreatedEventWithError()
        {
            // given...
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<UnrealizedTradePnlFundTransactionCreatedEvent>()))
                .Throws<InvalidOperationException>();

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var unrealizedTradePnlFundTransactionCreated = new UnrealizedTradePnlFundTransactionCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.UnrealizedTradePnl),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 12, 12)
            };

            // when...
            await fundEvents.ExecuteAsync(unrealizedTradePnlFundTransactionCreated);

            // then...
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(fundEvents.ErrorCode, logVM.StatusCode);

        }

        [Fact]
        public async Task UnrealizedTradePnlFundTransactionAdjustmentCreatedEventOk()
        {
            var fundTransaction = default(FundTransactionReadModel);
            var createdBy = default(string);
            var createdOn = default(DateTime);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<UnrealizedTradePnlFundTransactionAdjustmentCreatedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<UnrealizedTradePnlFundTransactionAdjustmentCreatedEvent>(e => {
                    fundTransaction = e.FundTransaction;
                    createdBy = e.CreatedBy;
                    createdOn = e.CreatedOn;
                });

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var unrealizedTradePnlFundTransactionAdjustmentCreated = new UnrealizedTradePnlFundTransactionAdjustmentCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.UnrealizedTradePnlAdjustment),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 12, 12)
            };
            await fundEvents.ExecuteAsync(unrealizedTradePnlFundTransactionAdjustmentCreated);
            Assert.NotNull(fundTransaction);
            Assert.Equal(1001, fundTransaction.TransactionId);
            Assert.Equal(2019, fundTransaction.TransactionDate.Year);
            Assert.Equal(10, fundTransaction.TransactionDate.Month);
            Assert.Equal(10, fundTransaction.TransactionDate.Day);
            Assert.Equal(FundTransactionType.UnrealizedTradePnlAdjustment, fundTransaction.TransactionType);
            Assert.Equal(1004, fundTransaction.FundId);
            Assert.Equal(2002, fundTransaction.OrderId);
            Assert.Equal(3003, fundTransaction.TradeId);
            Assert.Equal(TradeType.IronCondor, fundTransaction.TradeType);
            Assert.Equal(2019, fundTransaction.ValueDate.Year);
            Assert.Equal(10, fundTransaction.ValueDate.Month);
            Assert.Equal(10, fundTransaction.ValueDate.Day);
            Assert.Equal(TradeStatus.IntraDay, fundTransaction.TradeStatus);
            Assert.Equal("basilt", createdBy);
            Assert.Equal(2019, createdOn.Year);
            Assert.Equal(12, createdOn.Month);
            Assert.Equal(12, createdOn.Day);

        }

        [Fact]
        public async Task UnrealizedTradePnlFundTransactionAdjustmentCreatedEventWithError()
        {
            // given...
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<UnrealizedTradePnlFundTransactionAdjustmentCreatedEvent>()))
                .Throws<InvalidOperationException>();

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var unrealizedTradePnlFundTransactionAdjustmentCreated = new UnrealizedTradePnlFundTransactionAdjustmentCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.UnrealizedTradePnlAdjustment),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 12, 12)
            };

            // when...
            await fundEvents.ExecuteAsync(unrealizedTradePnlFundTransactionAdjustmentCreated);

            // then...
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(fundEvents.ErrorCode, logVM.StatusCode);

        }


        [Fact]
        public async Task RealizedTradePnlFundTransactionCreatedEventOk()
        {
            var fundTransaction = default(FundTransactionReadModel);
            var createdBy = default(string);
            var createdOn = default(DateTime);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<RealizedTradePnlFundTransactionCreatedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<RealizedTradePnlFundTransactionCreatedEvent>(e => {
                    fundTransaction = e.FundTransaction;
                    createdBy = e.CreatedBy;
                    createdOn = e.CreatedOn;
                });

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var realizedTradePnlFundTransactionCreated = new RealizedTradePnlFundTransactionCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.RealizedTradePnl),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 12, 12)
            };
            await fundEvents.ExecuteAsync(realizedTradePnlFundTransactionCreated);
            Assert.NotNull(fundTransaction);
            Assert.Equal(1001, fundTransaction.TransactionId);
            Assert.Equal(2019, fundTransaction.TransactionDate.Year);
            Assert.Equal(10, fundTransaction.TransactionDate.Month);
            Assert.Equal(10, fundTransaction.TransactionDate.Day);
            Assert.Equal(FundTransactionType.RealizedTradePnl, fundTransaction.TransactionType);
            Assert.Equal(1004, fundTransaction.FundId);
            Assert.Equal(2002, fundTransaction.OrderId);
            Assert.Equal(3003, fundTransaction.TradeId);
            Assert.Equal(TradeType.IronCondor, fundTransaction.TradeType);
            Assert.Equal(2019, fundTransaction.ValueDate.Year);
            Assert.Equal(10, fundTransaction.ValueDate.Month);
            Assert.Equal(10, fundTransaction.ValueDate.Day);
            Assert.Equal(TradeStatus.IntraDay, fundTransaction.TradeStatus);
            Assert.Equal("basilt", createdBy);
            Assert.Equal(2019, createdOn.Year);
            Assert.Equal(12, createdOn.Month);
            Assert.Equal(12, createdOn.Day);

        }

        [Fact]
        public async Task RealizedTradePnlFundTransactionCreatedEventWithError()
        {
            // given...
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<RealizedTradePnlFundTransactionCreatedEvent>()))
                .Throws<InvalidOperationException>();

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var realizedTradePnlFundTransactionCreated = new RealizedTradePnlFundTransactionCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.RealizedTradePnl),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 12, 12)
            };

            // when...
            await fundEvents.ExecuteAsync(realizedTradePnlFundTransactionCreated);

            // then...
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(fundEvents.ErrorCode, logVM.StatusCode);

        }

        [Fact]
        public async Task RealizedTradePnlFundTransactionAdjustmentCreatedEventOk()
        {
            var fundTransaction = default(FundTransactionReadModel);
            var createdBy = default(string);
            var createdOn = default(DateTime);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<RealizedTradePnlFundTransactionAdjustmentCreatedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<RealizedTradePnlFundTransactionAdjustmentCreatedEvent>(e => {
                    fundTransaction = e.FundTransaction;
                    createdBy = e.CreatedBy;
                    createdOn = e.CreatedOn;
                });

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var realizedTradePnlFundTransactionAdjustmentCreatedEvent = new RealizedTradePnlFundTransactionAdjustmentCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.RealizedTradePnlAdjustment),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 12, 12)
            };
            await fundEvents.ExecuteAsync(realizedTradePnlFundTransactionAdjustmentCreatedEvent);
            Assert.NotNull(fundTransaction);
            Assert.Equal(1001, fundTransaction.TransactionId);
            Assert.Equal(2019, fundTransaction.TransactionDate.Year);
            Assert.Equal(10, fundTransaction.TransactionDate.Month);
            Assert.Equal(10, fundTransaction.TransactionDate.Day);
            Assert.Equal(FundTransactionType.RealizedTradePnlAdjustment, fundTransaction.TransactionType);
            Assert.Equal(1004, fundTransaction.FundId);
            Assert.Equal(2002, fundTransaction.OrderId);
            Assert.Equal(3003, fundTransaction.TradeId);
            Assert.Equal(TradeType.IronCondor, fundTransaction.TradeType);
            Assert.Equal(2019, fundTransaction.ValueDate.Year);
            Assert.Equal(10, fundTransaction.ValueDate.Month);
            Assert.Equal(10, fundTransaction.ValueDate.Day);
            Assert.Equal(TradeStatus.IntraDay, fundTransaction.TradeStatus);
            Assert.Equal("basilt", createdBy);
            Assert.Equal(2019, createdOn.Year);
            Assert.Equal(12, createdOn.Month);
            Assert.Equal(12, createdOn.Day);

        }

        [Fact]
        public async Task RealizedTradePnlFundTransactionAdjustmentCreatedEventWithError()
        {
            // given...
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.InsertFundTransactionAsync(It.IsAny<RealizedTradePnlFundTransactionAdjustmentCreatedEvent>()))
                .Throws<InvalidOperationException>();

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var realizedTradePnlFundTransactionAdjustmentCreated = new RealizedTradePnlFundTransactionAdjustmentCreatedEvent
            {
                FundTransaction = TestDataDefaults.FundTransaction(FundTransactionType.RealizedTradePnlAdjustment),
                CreatedBy = "basilt",
                CreatedOn = new DateTime(2019, 12, 12)
            };

            // when...
            await fundEvents.ExecuteAsync(realizedTradePnlFundTransactionAdjustmentCreated);

            // then...
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(fundEvents.ErrorCode, logVM.StatusCode);

        }

        [Fact]
        public async Task FundOrderCompletedEventOk()
        {
            var fundId = default(int);
            var orderId = default(int);
            var orderStatus = OrderStatus.Cancelled;
            var completedOn = default(DateTime);
            var completedBy = default(string);
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.UpdateFundOrderStatusAsync(It.IsAny<FundOrderCompletedEvent>()))
                .Returns(Task.CompletedTask)
                .Callback<FundOrderCompletedEvent>(e => {
                    fundId = e.FundId;
                    orderId = e.OrderId;
                    orderStatus = e.OrderStatus;
                    completedOn = e.CompletedOn;
                    completedBy = e.CompletedBy;
                });

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var fundOrderCompleted = new FundOrderCompletedEvent
            {
                FundId = 1004,
                OrderId = 2002,
                OrderStatus = OrderStatus.Completed,
                CompletedBy = "basilt",
                CompletedOn = new DateTime(2019,10,10)
            };
            await fundEvents.ExecuteAsync(fundOrderCompleted);
            Assert.Equal(1004, fundId);
            Assert.Equal(2002, orderId);
            Assert.Equal(OrderStatus.Completed, orderStatus);
            Assert.Equal(2019, completedOn.Year);
            Assert.Equal(10, completedOn.Month);
            Assert.Equal(10, completedOn.Day);
            Assert.Equal("basilt", completedBy);

        }

        [Fact]
        public async Task FundOrderCompletedEventWithError()
        {
            // given...
            var errorMsg = (new InvalidOperationException()).Message;
            var logVM = default(StatusConsoleLogReadModel);
            var mockStatusConsoleServiceApi = new Mock<IStatusConsoleServiceApi>(MockBehavior.Strict);
            mockStatusConsoleServiceApi
                .Setup(e => e.StatusConsoleLogUpdatedAsync(It.IsAny<StatusConsoleLoggedEvent>()))
                .ReturnsAsync((StatusConsoleLoggedEvent o) => new ServiceOk<StatusConsoleLoggedEvent>(o))
                .Callback<StatusConsoleLoggedEvent>(ev => logVM = ev.StatusConsoleLog);

            var mockEventDenormalizer = new Mock<IFundEventDenormalizerApi>(MockBehavior.Strict);
            mockEventDenormalizer
                .Setup(e => e.UpdateFundOrderStatusAsync(It.IsAny<FundOrderCompletedEvent>()))
                .Throws<InvalidOperationException>();

            var fundEvents = new FundEvents(mockEventDenormalizer.Object, mockStatusConsoleServiceApi.Object);
            var fundOrderCompleted = new FundOrderCompletedEvent
            {
                FundId = 1004,
                OrderId = 2002,
                OrderStatus = OrderStatus.Completed,
                CompletedBy = "basilt",
                CompletedOn = new DateTime(2019, 10, 10)
            };

            // when...
            await fundEvents.ExecuteAsync(fundOrderCompleted);

            // then...
            Assert.NotNull(logVM);
            Assert.Equal(errorMsg, logVM.Message);
            Assert.Equal(fundEvents.ErrorCode, logVM.StatusCode);

        }
    }
}
