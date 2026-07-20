using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Command.SignalR.Trade;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.UnitTests.Application.Trade
{
    [TestClass]
    public class SpreadTradeAggregateRootTests
    {
        [TestMethod]
        public void CreateTradeOk()
        {
            var shortOptionLeg = new OptionLegReadModel
            {
                ContractId = "ES20180720P2665",
                StrikePrice = 2665m,
                Quantity = 7,
                OptionType = OptionType.Put,
                OptionLegAction = OptionLegAction.Short
            };
            var longOptionLeg = new OptionLegReadModel
            {
                ContractId = "ES20180720P2565",
                StrikePrice = 2565m,
                Quantity = 7,
                OptionType = OptionType.Put,
                OptionLegAction = OptionLegAction.Long
            };
            var state = new SpreadTradeAggregateState(1, new DomainEventCollection());
            var root = new SpreadTradeAggregate(state);
            root.ExecuteCommand(new CreateSpreadTradeCommand
            {
                OrderId = 1000,
                TradeId = 1001,
                TradeStrategy = "Put Credit Spread @ 1.79",
                TradeDate = new DateTime(2018, 6, 4),
                MaturityDate = new DateTime(2018, 7, 20),
                TradeType = TradeType.CallCreditSpread,
                TradeState = TradeState.PendingTradeOpen,
                TradeAction = TradeAction.Sell,
                UnderlyingContractId = "ES20180921",
                ShortOptionLeg = shortOptionLeg,
                LongOptionLeg = longOptionLeg,
                OpeningFundBalance = 25146.24m
            });
            var events = state.ToEvents();
            Assert.IsTrue(events.Count == 1);
            Assert.IsInstanceOfType(events[0], typeof(SpreadTradeCreatedEvent));
            var tradeEvent = events[0] as SpreadTradeCreatedEvent;
            Assert.AreEqual(1000, tradeEvent.OrderId);
            Assert.AreEqual(1001, tradeEvent.TradeId);
            Assert.AreEqual("Put Credit Spread @ 1.79", tradeEvent.TradeStrategy);
            Assert.AreEqual(new DateTime(2018, 6, 4), tradeEvent.TradeDate);
            Assert.AreEqual(new DateTime(2018, 7,20), tradeEvent.MaturityDate);
            Assert.AreEqual(TradeType.CallCreditSpread, tradeEvent.TradeType);
            Assert.AreEqual(TradeState.PendingTradeOpen, tradeEvent.TradeState);
            Assert.AreEqual(TradeAction.Sell, tradeEvent.TradeAction);
            Assert.AreEqual("ES20180921", tradeEvent.UnderlyingContractId);
            Assert.IsNotNull(tradeEvent.ShortOptionLeg);
            Assert.IsNotNull(tradeEvent.LongOptionLeg);
            Assert.AreEqual("ES20180720P2665", tradeEvent.ShortOptionLeg.ContractId);
            Assert.AreEqual(2665m, tradeEvent.ShortOptionLeg.StrikePrice);
            Assert.AreEqual(7, tradeEvent.ShortOptionLeg.Quantity);
            Assert.AreEqual(OptionType.Put, tradeEvent.ShortOptionLeg.OptionType);
            Assert.AreEqual(OptionLegAction.Short, tradeEvent.ShortOptionLeg.OptionLegAction);
            Assert.AreEqual("ES20180720P2565", tradeEvent.LongOptionLeg.ContractId);
            Assert.AreEqual(2565m, tradeEvent.LongOptionLeg.StrikePrice);
            Assert.AreEqual(7, tradeEvent.LongOptionLeg.Quantity);
            Assert.AreEqual(OptionType.Put, tradeEvent.LongOptionLeg.OptionType);
            Assert.AreEqual(OptionLegAction.Long, tradeEvent.LongOptionLeg.OptionLegAction);
        }

        [TestMethod]
        public void CreateTradeWithTradeThatAlreadyExists()
        {
            var shortOptionLeg = new OptionLegReadModel {
                ContractId = "ES20180720P2665",
                StrikePrice = 2665m,
                Quantity = 7,
                OptionType = OptionType.Put,
                OptionLegAction = OptionLegAction.Short
            };
            var longOptionLeg = new OptionLegReadModel {
                ContractId = "ES20180720P2565",
                StrikePrice = 2565m,
                Quantity = 7,
                OptionType = OptionType.Put,
                OptionLegAction = OptionLegAction.Long
            };
            var state = new SpreadTradeAggregateState(1, new DomainEventCollection());
            var root = new SpreadTradeAggregate(state);
            root.ExecuteCommand(new CreateSpreadTradeCommand
            {
                OrderId = 1000,
                TradeId = 1001,
                TradeStrategy = "Put Credit Spread @ 1.79",
                TradeDate = new DateTime(2018, 6, 4),
                MaturityDate = new DateTime(2018, 7, 20),
                TradeType = TradeType.CallCreditSpread,
                TradeState = TradeState.PendingTradeOpen,
                TradeAction = TradeAction.Sell,
                UnderlyingContractId = "ES20180921",
                ShortOptionLeg = shortOptionLeg,
                LongOptionLeg = longOptionLeg,
                OpeningFundBalance = 25146.24m
            });
            var oldEvents = state.ToEvents();
            state = new SpreadTradeAggregateState(2, oldEvents);
            try
            {
                root.ExecuteCommand(new CreateSpreadTradeCommand
                {
                    OrderId = 1000,
                    TradeId = 1001,
                    TradeStrategy = "Put Credit Spread @ 1.79",
                    TradeDate = new DateTime(2018, 6, 4),
                    MaturityDate = new DateTime(2018, 7, 20),
                    TradeType = TradeType.CallCreditSpread,
                    TradeState = TradeState.PendingTradeOpen,
                    TradeAction = TradeAction.Sell,
                    UnderlyingContractId = "ES20180921",
                    ShortOptionLeg = shortOptionLeg,
                    LongOptionLeg = longOptionLeg,
                    OpeningFundBalance = 25146.24m
                });
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual($"CreateTrade tradeId: {1001} already exists", ex.Message);
                return;
            }
            Assert.Fail("Create trade failed due to trade that already exists");

        }

        [TestMethod]
        public void CreateTradeWithNonPendingTradeState()
        {
            var shortOptionLeg = new OptionLegReadModel
            {
                ContractId = "ES20180720P2665",
                StrikePrice = 2665m,
                Quantity = 7,
                OptionType = OptionType.Put,
                OptionLegAction = OptionLegAction.Short
            };
            var longOptionLeg = new OptionLegReadModel
            {
                ContractId = "ES20180720P2565",
                StrikePrice = 2565m,
                Quantity = 7,
                OptionType = OptionType.Put,
                OptionLegAction = OptionLegAction.Long
            };
            var state = new SpreadTradeAggregateState(1, new DomainEventCollection());
            var root = new SpreadTradeAggregate(state);
            try
            {
                root.ExecuteCommand(new CreateSpreadTradeCommand
                {
                    OrderId = 1000,
                    TradeId = 1001,
                    TradeStrategy = "Put Credit Spread @ 1.79",
                    TradeDate = new DateTime(2018, 6, 4),
                    MaturityDate = new DateTime(2018, 7, 20),
                    TradeType = TradeType.CallCreditSpread,
                    TradeState = TradeState.PendingTradeOpen,
                    TradeAction = TradeAction.Sell,
                    UnderlyingContractId = "ES20180921",
                    ShortOptionLeg = shortOptionLeg,
                    LongOptionLeg = longOptionLeg,
                    OpeningFundBalance = 25146.24m
                });
            }
            catch (InvalidOperationException ex)
            {
                Assert.AreEqual($"CreateTrade tradeState: {TradeState.TradeToOpen} must be in PendingTradeOpen state", ex.Message);
                return;
            }
            Assert.Fail("Create trade failed due to trade state not in PendingOpen state");
        }

        [TestMethod]
        public void OpenTradeOk()
        {
            var shortOptionLeg = new OptionLegReadModel
            {
                ContractId = "ES20180720P2665",
                StrikePrice = 2665m,
                Quantity = 7,
                OptionType = OptionType.Put,
                OptionLegAction = OptionLegAction.Short
            };
            var longOptionLeg = new OptionLegReadModel
            {
                ContractId = "ES20180720P2565",
                StrikePrice = 2565m,
                Quantity = 7,
                OptionType = OptionType.Put,
                OptionLegAction = OptionLegAction.Long
            };
            var state = new SpreadTradeAggregateState(1, new DomainEventCollection());
            var root = new SpreadTradeAggregate(state);
            root.ExecuteCommand(new CreateSpreadTradeCommand
            {
                OrderId = 1000,
                TradeId = 1001,
                TradeStrategy = "Put Credit Spread @ 1.79",
                TradeDate = new DateTime(2018, 6, 4),
                MaturityDate = new DateTime(2018, 7, 20),
                TradeType = TradeType.CallCreditSpread,
                TradeState = TradeState.PendingTradeOpen,
                TradeAction = TradeAction.Sell,
                UnderlyingContractId = "ES20180921",
                ShortOptionLeg = shortOptionLeg,
                LongOptionLeg = longOptionLeg,
                OpeningFundBalance = 25146.24m
            });
            state = new SpreadTradeAggregateState(2, state.ToEvents());
            root = new SpreadTradeAggregate(state);
            var shortOptionData = new OptionLegDataReadModel
            {
                OptionLeg = shortOptionLeg,
                Bid = 20.50m,
                Ask = 20.50m,
                Delta = -0.2423,
                Gamma = 0.0024,
                Theta = -150.1757,
                ImpliedVolatility = 0.1340,
                Rho = 0.0,
                Vega = 0.0
            };
            var longOptionData = new OptionLegDataReadModel
            {
                OptionLeg = longOptionLeg,
                Bid = 10.00m,
                Ask = 10.00m,
                Delta = -0.1132,
                Gamma = 0.0012,
                Theta = -119.3162,
                ImpliedVolatility = 0.1680,
                Rho = 0.0,
                Vega = 0.0
            };
            root.ExecuteCommand(new OpenSpreadTradeCommand
            {
                OrderId = 1000,
                TradeId = 1001,
                ValueDate = new DateTime(2018, 6, 4),
                TradeStatus = TradeStatus.Open,
                Commission = 19.88m,
                AssetPrice = 2745.75m,
                RiskFreeRate = 0.0174,
                ShortOptionData = shortOptionData,
                LongOptionData = longOptionData
            });
            var events = state.ToEvents();
            Assert.IsNotNull(events);
            Assert.IsTrue(events.Count == 2);
            var tradeOpened = events[0] as SpreadTradeOpeningEvent;
            Assert.IsNotNull(tradeOpened);
            Assert.AreEqual(TradeState.TradeToOpen, tradeOpened.TradeState);
            Assert.AreEqual(1001, tradeOpened.TradeDataKey.TradeId);
            Assert.AreEqual(TradeType.PutCreditSpread, tradeOpened.TradeDataKey.TradeType);
            Assert.AreEqual(TradeStatus.Open, tradeOpened.TradeDataKey.TradeStatus);
            Assert.AreEqual(new DateTime(2018, 6, 4), tradeOpened.TradeDataKey.ValueDate);
            Assert.AreEqual(46, tradeOpened.TradeDataKey.DaysToExpiry);
            Assert.AreEqual(shortOptionData.Bid, tradeOpened.ShortOptionData.Bid);
            Assert.AreEqual(shortOptionData.Ask, tradeOpened.ShortOptionData.Ask);
            Assert.AreEqual(shortOptionData.Delta, tradeOpened.ShortOptionData.Delta);
            Assert.AreEqual(shortOptionData.Gamma, tradeOpened.ShortOptionData.Gamma);
            Assert.AreEqual(shortOptionData.Theta, tradeOpened.ShortOptionData.Theta);
            Assert.AreEqual(shortOptionData.ImpliedVolatility, tradeOpened.ShortOptionData.ImpliedVolatility);
            Assert.AreEqual(shortOptionData.Vega, tradeOpened.ShortOptionData.Vega);
            Assert.AreEqual(shortOptionData.Rho, tradeOpened.ShortOptionData.Rho);
            Assert.AreEqual(longOptionData.Bid, tradeOpened.LongOptionData.Bid);
            Assert.AreEqual(longOptionData.Ask, tradeOpened.LongOptionData.Ask);
            Assert.AreEqual(longOptionData.Delta, tradeOpened.LongOptionData.Delta);
            Assert.AreEqual(longOptionData.Gamma, tradeOpened.LongOptionData.Gamma);
            Assert.AreEqual(longOptionData.Theta, tradeOpened.LongOptionData.Theta);
            Assert.AreEqual(longOptionData.ImpliedVolatility, tradeOpened.LongOptionData.ImpliedVolatility);
            Assert.AreEqual(longOptionData.Vega, tradeOpened.LongOptionData.Vega);
            Assert.AreEqual(longOptionData.Rho, tradeOpened.LongOptionData.Rho);
            var tradeDataOpened = events[1] as SpreadTradeOpenedEvent;
            Assert.IsNotNull(tradeDataOpened);
            Assert.AreEqual(1001, tradeDataOpened.TradeDataKey.TradeId);
            Assert.AreEqual(TradeType.PutCreditSpread, tradeDataOpened.TradeDataKey.TradeType);
            Assert.AreEqual(TradeStatus.Open, tradeDataOpened.TradeDataKey.TradeStatus);
            Assert.AreEqual(new DateTime(2018, 6, 4), tradeDataOpened.TradeDataKey.ValueDate);
            Assert.AreEqual(46, tradeDataOpened.TradeDataKey.DaysToExpiry);
            Assert.AreEqual(19.88m, tradeDataOpened.Commission);
            Assert.AreEqual(2745.75m, tradeDataOpened.AssetPrice);
            Assert.AreEqual(0.0174, tradeDataOpened.RiskFreeRate);
            Assert.AreEqual(10.50m, tradeDataOpened.NetSpread);
            Assert.AreEqual(3675.00m, tradeDataOpened.TradeValue);
            Assert.AreEqual(0.0m, tradeDataOpened.TradePnl);
        }

        [TestMethod]
        public void ChangeShortOptionLegOk()
        {
            var shortOptionLeg = new OptionLegReadModel
            {
                ContractId = "ES20180720P2665",
                StrikePrice = 2665m,
                Quantity = 7,
                OptionType = OptionType.Put,
                OptionLegAction = OptionLegAction.Short
            };
            var longOptionLeg = new OptionLegReadModel
            {
                ContractId = "ES20180720P2565",
                StrikePrice = 2565m,
                Quantity = 7,
                OptionType = OptionType.Put,
                OptionLegAction = OptionLegAction.Long
            };
            var events = new DomainEventCollection();
            var state = new SpreadTradeAggregateState(1, events);
            var root = new SpreadTradeAggregate(state);
            root.ExecuteCommand(new CreateSpreadTradeCommand
            {
                OrderId = 1000,
                TradeId = 1001,
                TradeStrategy = "Put Credit Spread @ 1.79",
                TradeDate = new DateTime(2018, 6, 4),
                MaturityDate = new DateTime(2018, 7, 20),
                TradeType = TradeType.CallCreditSpread,
                TradeState = TradeState.PendingTradeOpen,
                TradeAction = TradeAction.Sell,
                UnderlyingContractId = "ES20180921",
                ShortOptionLeg = shortOptionLeg,
                LongOptionLeg = longOptionLeg,
                OpeningFundBalance = 25146.24m
            });
            events.AddRange(state.ToEvents());
            state = new SpreadTradeAggregateState(2, events);
            root = new SpreadTradeAggregate(state);
            var shortOptionData = new OptionLegDataReadModel
            {
                OptionLeg = shortOptionLeg,
                Bid = 20.50m,
                Ask = 20.50m,
                Delta = -0.2423,
                Gamma = 0.0024,
                Theta = -150.1757,
                ImpliedVolatility = 0.1340,
                Rho = 0.0,
                Vega = 0.0
            };
            var longOptionData = new OptionLegDataReadModel
            {
                OptionLeg = longOptionLeg,
                Bid = 10.00m,
                Ask = 10.00m,
                Delta = -0.1132,
                Gamma = 0.0012,
                Theta = -119.3162,
                ImpliedVolatility = 0.1680,
                Rho = 0.0,
                Vega = 0.0
            };
            root.ExecuteCommand(new OpenSpreadTradeCommand
            {
                OrderId = 1000,
                TradeId = 1001,
                ValueDate = new DateTime(2018, 6, 4),
                TradeStatus = TradeStatus.Open,
                Commission = 19.88m,
                AssetPrice = 2745.75m,
                RiskFreeRate = 0.0174,
                ShortOptionData = shortOptionData,
                LongOptionData = longOptionData
            });
            shortOptionData = new OptionLegDataReadModel
            {
                OptionLeg = shortOptionLeg,
                Bid = 22.50m,
                Ask = 21.50m,
                Delta = -0.2420,
                Gamma = 0.0024,
                Theta = -150.1757,
                ImpliedVolatility = 0.1347,
                Rho = 0.0,
                Vega = 0.0
            };
            events.AddRange(state.ToEvents());
            state = new SpreadTradeAggregateState(3, events);
            root = new SpreadTradeAggregate(state);
            root.ExecuteCommand(new ChangeSpreadTradeShortOptionDataCommand {
                OrderId = 1000,
                TradeId = 1001,
                TradeType = TradeType.PutCreditSpread,
                ValueDate = new DateTime(2018, 6, 4),
                TradeStatus = TradeStatus.IntraDay,
                ShortOptionData = shortOptionData,
                AssetPrice = 2767.56m,
                RiskFreeRate = 0.0186
            });
            var resultEvents = state.ToEvents();
        }
    }

}
