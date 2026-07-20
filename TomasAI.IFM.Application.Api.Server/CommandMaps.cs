using TomasAI.IFM.Application.Command;
using TomasAI.IFM.Shared.Application;
using TomasAI.IFM.Shared.Application.CommandParameters;
using TomasAI.IFM.Shared.Application.Commands;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.CommandParameters;
using TomasAI.IFM.Shared.MarketData.Commands;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.CommandParameters;
using TomasAI.IFM.Shared.MarketDataAnalytics.Commands;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.CommandParameters;
using TomasAI.IFM.Shared.MarketDataFeed.Commands;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.CommandParameters;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.Reference;
using TomasAI.IFM.Shared.Reference.CommandParameters;
using TomasAI.IFM.Shared.Reference.Commands;
using TomasAI.IFM.Shared.SystemAdmin;
using TomasAI.IFM.Shared.SystemAdmin.Commands;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Commands;
using TomasAI.IFM.Shared.WebService;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.CommandParameters;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Application.Api.Server;

static public class CommandMaps
{

    public static IEndpointRouteBuilder MapApiCommands(this IEndpointRouteBuilder endpoints, ILogger logger)
    {
        // Chain all command mapping methods here
        return endpoints
            .MapApplicationCommands()
            .MapFundCommands()
            .MapFundTransactionCommands()
            .MapMarketDataCommands()
            .MapMarketDataAnalyticsCommands()
            .MapMarketDataFeedCommands()
            .MapOptionPricerCommands()
            .MapTradeCommands()
            .MapTradePlanCommands()
            .MapReferenceCommands()
            .MapSystemAdminCommands();
    }

}

public static class ApplicationCommands
{
    public static IEndpointRouteBuilder MapApplicationCommands(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(ApplicationUriPath.Start, async (IActorService e, StartApplicationParameter cp)
            => {
                var entityId = new ApplicationEntityId(cp.ValueDate);
                StartApplicationCommand cmd = new(cp.ValueDate)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, StartApplicationCommand.Actor, StartApplicationCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<StartApplicationCommand, ApplicationEntityId>(cmd);
            });

        endpoints.MapPost(ApplicationUriPath.Shutdown, async (IActorService e, ShutdownApplicationParameter cp)
          => {
              var entityId = new ApplicationEntityId(cp.ValueDate);
              ShutdownApplicationCommand cmd = new(cp.ValueDate)
              {
                  CommandId = Guid.NewGuid(),
                  Subject = new ActorSubject(ActorType.Command, ShutdownApplicationCommand.Actor, ShutdownApplicationCommand.Verb, entityId.Format()),
                  EntityId = entityId
              };
              return await e.RequestAsync<ShutdownApplicationCommand, ApplicationEntityId>(cmd);
          });
        return endpoints;
    }
}

/// <summary>
/// Provides extension methods for mapping fund-related command endpoints to an ASP.NET Core routing builder.
/// </summary>
/// <remarks>Use this class to register HTTP endpoints for fund management operations, such as creating funds,
/// adding or removing orders and trades, changing trade states, closing fund orders, and generating maximum profit for
/// a fund. These endpoints are intended to be used with an actor-based service architecture and facilitate
/// command-based interactions for fund entities within the application.</remarks>
public static class FundCommands
{
    public static IEndpointRouteBuilder MapFundCommands(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(FundUriPath.Create, async (IActorService e, CreateFundParameter cmdParam)
            => {
                var entityId = new FundId(cmdParam.Fund.FundId);
                CreateFundCommand cmd = new(cmdParam.Fund)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, CreateFundCommand.Actor, CreateFundCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<CreateFundCommand, FundId>(cmd);
            });

        endpoints.MapPost(FundUriPath.AddOrderToFund, async (IActorService e, AddOrderToFundParameter cmdParam)
           => {
               var entityId = new FundId(cmdParam.FundOrder.FundId);
               AddOrderToFundCommand cmd = new(cmdParam.FundOrder)
               {
                   CommandId = Guid.NewGuid(),
                   Subject = new ActorSubject(ActorType.Command, AddOrderToFundCommand.Actor, AddOrderToFundCommand.Verb, entityId.Format()),
                   EntityId = entityId
               };
               return await e.RequestAsync<AddOrderToFundCommand, FundId>(cmd!);
           });

        endpoints.MapPost(FundUriPath.AddTradeToFundOrder, async (IActorService e, AddTradeToFundOrderParameter cmdParam)
            => {
                var entityId = new FundId(cmdParam.FundOrderTrade.FundId);
                AddTradeToFundOrderCommand cmd = new(cmdParam.FundOrderTrade)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, AddTradeToFundOrderCommand.Actor, AddTradeToFundOrderCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<AddTradeToFundOrderCommand, FundId>(cmd!);
            });

        endpoints.MapPost(FundUriPath.ChangeFundOrderTradeState, async (IActorService e, ChangeFundOrderTradeStateParameter cmdParam)
            => {
                var entityId = new FundId(cmdParam.FundOrderTradeId.FundId);
                ChangeFundOrderTradeStateCommand cmd = new(cmdParam.FundOrderTradeId, cmdParam.TradeState)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, ChangeFundOrderTradeStateCommand.Actor, ChangeFundOrderTradeStateCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<ChangeFundOrderTradeStateCommand, FundId>(cmd!);
            });

        endpoints.MapPost(FundUriPath.RemoveOrderFromFund, async (IActorService e, RemoveOrderFromFundParameter cmdParam)
            => {
                var entityId = new FundId(cmdParam.FundOrderId.FundId);
                RemoveOrderFromFundCommand cmd = new(cmdParam.FundOrderId)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, RemoveOrderFromFundCommand.Actor, RemoveOrderFromFundCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<RemoveOrderFromFundCommand, FundId>(cmd!);
            });

        endpoints.MapPost(FundUriPath.RemoveTradeFromFundOrder, async (IActorService e, RemoveTradeFromFundOrderParameter cmdParam)
            => {
                var entityId = new FundId(cmdParam.FundOrderTradeId.FundId);
                RemoveTradeFromFundOrderCommand cmd = new(cmdParam.FundOrderTradeId)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, RemoveTradeFromFundOrderCommand.Actor, RemoveTradeFromFundOrderCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<RemoveTradeFromFundOrderCommand, FundId>(cmd!);
            });

        endpoints.MapPost(FundUriPath.CloseFundOrder, async (IActorService e, CloseFundOrderParameter cmdParam)
            => {
                var entityId = new FundId(cmdParam.FundOrderId.FundId);
                CloseFundOrderCommand cmd = new(cmdParam.FundOrderId)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, CloseFundOrderCommand.Actor, CloseFundOrderCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<CloseFundOrderCommand, FundId>(cmd!);
            });

        endpoints.MapPost(FundUriPath.GenerateFundMaxProfit, async (IActorService e, GenerateFundMaxProfitParameter cp)
            => {
                var entityId = new FundId(cp.FundOrder.FundId);
                GenerateFundMaxProfitCommand cmd = new(cp.FundOrder, cp.TimePeriod)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, GenerateFundMaxProfitCommand.Actor, GenerateFundMaxProfitCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<GenerateFundMaxProfitCommand, FundId>(cmd!);
            });

        return endpoints;
    }
}

/// <summary>
/// Provides extension methods for mapping fund transaction-related command endpoints to an ASP.NET Core routing builder.
/// </summary>
/// <remarks>Use this class to register HTTP endpoints for fund transaction management operations, such as creating fund transactions,
/// creating multiple transactions, and processing end-of-day transactions. These endpoints are intended to be used with an actor-based 
/// service architecture and facilitate command-based interactions for fund transaction entities within the application.</remarks>
public static class FundTransactionCommands
{
    public static IEndpointRouteBuilder MapFundTransactionCommands(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(FundTransactionUriPath.Create, async (IActorService e, CreateFundTransactionParameter cmdParam)
            => {
                var entityId = cmdParam.FundTransaction.EntityId;
                CreateFundTransactionCommand cmd = new(cmdParam.FundTransaction)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, CreateFundTransactionCommand.Actor, CreateFundTransactionCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<CreateFundTransactionCommand, FundTransactionEntityId>(cmd!);
            });

        endpoints.MapPost(FundTransactionUriPath.CreateTransactions, async (IActorService e, CreateFundTransactionsParameter cmdParam)
            => {
                var entityId = cmdParam.TransactionsId;
                CreateFundTransactionsCommand cmd = new(cmdParam.TransactionsId, cmdParam.FundTransactions)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, CreateFundTransactionsCommand.Actor, CreateFundTransactionsCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<CreateFundTransactionsCommand, FundTransactionEntityId>(cmd!);
            });

        endpoints.MapPost(FundTransactionUriPath.ProcessEndOfDay, async (IActorService e, ProcessEndOfDayFundTransactionParameter cmdParam)
            => {
                var entityId = cmdParam.FundTransaction.EntityId;
                ProcessEndOfDayFundTransactionCommand cmd = new(cmdParam.FundTransaction)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, ProcessEndOfDayFundTransactionCommand.Actor, ProcessEndOfDayFundTransactionCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<ProcessEndOfDayFundTransactionCommand, FundTransactionEntityId>(cmd!);
            });

        return endpoints;
    }
}

/// <summary>
/// Provides extension methods for mapping reference-related command endpoints to an ASP.NET Core routing builder.
/// </summary>
/// <remarks>Use this class to register HTTP endpoints for reference management operations, such as adding, changing,
/// and removing economic calendars and lookup types, as well as importing economic calendars. These endpoints are intended
/// to be used with an actor-based service architecture and facilitate command-based interactions for reference entities
/// within the application.</remarks>
public static class ReferenceCommands
{
    public static IEndpointRouteBuilder MapReferenceCommands(this IEndpointRouteBuilder endpoints)
    {
        // Economic Calendar Commands
        endpoints.MapPost(ReferenceUriPath.AddEconomicCalendar, async (IActorService e, AddEconomicCalendarParameter cmdParam)
            => {
                var entityId = new EconomicCalendarId(cmdParam.EconomicCalendar.EventDate, cmdParam.EconomicCalendar.CountryCode, cmdParam.EconomicCalendar.EventName);
                AddEconomicCalendarCommand cmd = new(cmdParam.EconomicCalendar)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, AddEconomicCalendarCommand.Actor, AddEconomicCalendarCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<AddEconomicCalendarCommand, EconomicCalendarId>(cmd!);
            });

        endpoints.MapPost(ReferenceUriPath.ChangeEconomicCalendar, async (IActorService e, ChangeEconomicCalendarParameter cmdParam)
            => {
                var entityId = cmdParam.EconomicCalendarId;
                ChangeEconomicCalendarCommand cmd = new(cmdParam.EconomicCalendarId, cmdParam.EconomicCalendar, cmdParam.Overwrite)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, ChangeEconomicCalendarCommand.Actor, ChangeEconomicCalendarCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<ChangeEconomicCalendarCommand, EconomicCalendarId>(cmd!);
            });

        endpoints.MapPost(ReferenceUriPath.RemoveEconomicCalendar, async (IActorService e, RemoveEconomicCalendarParameter cmdParam)
            => {
                var entityId = cmdParam.EconomicCalendarId;
                RemoveEconomicCalendarCommand cmd = new(cmdParam.EconomicCalendarId, cmdParam.Overwrite)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, RemoveEconomicCalendarCommand.Actor, RemoveEconomicCalendarCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<RemoveEconomicCalendarCommand, EconomicCalendarId>(cmd!);
            });

        endpoints.MapPost(ReferenceUriPath.ImportEconomicCalendars, async (IActorService e, ImportEconomicCalendarsParameter cmdParam)
            => {
                var entityId = new EconomicCalendarId(cmdParam.ImportedDate, "ZZ", "ImportEconomicCalendars");
                ImportEconomicCalendarsCommand cmd = new(cmdParam.EconomicCalendars, cmdParam.ImportedDate)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, ImportEconomicCalendarsCommand.Actor, ImportEconomicCalendarsCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<ImportEconomicCalendarsCommand, EconomicCalendarId>(cmd!);
            });

        // Lookup Type Commands
        endpoints.MapPost(ReferenceUriPath.AddLookupType, async (IActorService e, AddLookupTypeParameter cmdParam)
            => {
                var entityId = new LookupTypeId(cmdParam.LookupType.LookupTypeName, cmdParam.LookupType.OrderId);
                AddLookupTypeCommand cmd = new(cmdParam.LookupType)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, AddLookupTypeCommand.Actor, AddLookupTypeCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<AddLookupTypeCommand, LookupTypeId>(cmd!);
            });

        endpoints.MapPost(ReferenceUriPath.ChangeLookupType, async (IActorService e, ChangeLookupTypeParameter cmdParam)
            => {
                var entityId = cmdParam.LookupTypeId;
                ChangeLookupTypeCommand cmd = new(cmdParam.LookupTypeId, cmdParam.LookupType, cmdParam.Overwrite)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, ChangeLookupTypeCommand.Actor, ChangeLookupTypeCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<ChangeLookupTypeCommand, LookupTypeId>(cmd!);
            });

        endpoints.MapPost(ReferenceUriPath.RemoveLookupType, async (IActorService e, RemoveLookupTypeParameter cmdParam)
            => {
                var entityId = cmdParam.LookupTypeId;
                RemoveLookupTypeCommand cmd = new(cmdParam.LookupTypeId, cmdParam.Overwrite)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, RemoveLookupTypeCommand.Actor, RemoveLookupTypeCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<RemoveLookupTypeCommand, LookupTypeId>(cmd!);
            });

        return endpoints;
    }
}

public static class MarketDataCommands
{
    public static IEndpointRouteBuilder MapMarketDataCommands(this IEndpointRouteBuilder endpoints)
    {
        // Futures Contract Commands
        endpoints.MapPost(MarketDataUriPath.AddFuturesContract, async (IActorService e, AddFuturesContractParameter cmdParam)
            => {
                var entityId = cmdParam.Contract.Id;
                AddFuturesContractCommand cmd = new(cmdParam.Contract, cmdParam.Overwrite)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, AddFuturesContractCommand.Actor, AddFuturesContractCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<AddFuturesContractCommand, FuturesContractId>(cmd!);
            });

        endpoints.MapPost(MarketDataUriPath.ChangeFuturesContract, async (IActorService e, ChangeFuturesContractParameter cmdParam)
            => {
                var entityId = cmdParam.ContractId;
                ChangeFuturesContractCommand cmd = new(cmdParam.ContractId, cmdParam.Contract, cmdParam.Overwrite)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, ChangeFuturesContractCommand.Actor, ChangeFuturesContractCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<ChangeFuturesContractCommand, FuturesContractId>(cmd!);
            });

        endpoints.MapPost(MarketDataUriPath.RemoveFuturesContract, async (IActorService e, RemoveFuturesContractParameter cmdParam)
            => {
                var entityId = cmdParam.ContractId;
                RemoveFuturesContractCommand cmd = new(cmdParam.ContractId, cmdParam.Overwrite)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, RemoveFuturesContractCommand.Actor, RemoveFuturesContractCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<RemoveFuturesContractCommand, FuturesContractId>(cmd!);
            });

        // Futures Option Contract Commands
        endpoints.MapPost(MarketDataUriPath.AddFuturesOptionContract, async (IActorService e, AddFuturesOptionContractParameter cp)
            => {
                IsArgumentNull.Check(cp.Contract);
                var entityId =  new FuturesOptionContractEntityId(cp.Contract.ContractId, cp.MaturityDateYear);
                AddFuturesOptionContractCommand cmd = new(cp.Contract, cp.Overwrite)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, AddFuturesOptionContractCommand.Actor, AddFuturesOptionContractCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cp.ErrorCode
                };
                return await e.RequestAsync<AddFuturesOptionContractCommand, FuturesOptionContractEntityId>(cmd!);
            });

        endpoints.MapPost(MarketDataUriPath.AddFuturesOptionContracts, async (IActorService e, AddFuturesOptionContractsParameter cp)
            => {
                var entityId = new FuturesOptionContractsEntityId(DateTime.Now.Year);
                AddFuturesOptionContractsCommand cmd = new(cp.Contracts)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, AddFuturesOptionContractsCommand.Actor, AddFuturesOptionContractsCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cp.ErrorCode
                };
                return await e.RequestAsync<AddFuturesOptionContractsCommand, FuturesOptionContractsEntityId>(cmd!);
            });

        endpoints.MapPost(MarketDataUriPath.ChangeFuturesOptionContract, async (IActorService e, ChangeFuturesOptionContractParameter cp)
            => {
                var entityId =  new FuturesOptionContractEntityId(cp.ContractId, cp.Contract.ContractMonth.Year);
                ChangeFuturesOptionContractCommand cmd = new(cp.ContractId, cp.Contract, cp.Overwrite)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, ChangeFuturesOptionContractCommand.Actor, ChangeFuturesOptionContractCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cp.ErrorCode
                };
                return await e.RequestAsync<ChangeFuturesOptionContractCommand, FuturesOptionContractEntityId>(cmd!);
            });

        endpoints.MapPost(MarketDataUriPath.RemoveFuturesOptionContract, async (IActorService e, RemoveFuturesOptionContractParameter cp)
            => {
                var foContract = new FuturesOptionContractId(cp.ContractId);
                var entityId = new FuturesOptionContractEntityId(cp.ContractId, foContract.MaturityDate.Year);
                RemoveFuturesOptionContractCommand cmd = new(cp.ContractId, cp.Overwrite)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, RemoveFuturesOptionContractCommand.Actor, RemoveFuturesOptionContractCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cp.ErrorCode
                };
                return await e.RequestAsync<RemoveFuturesOptionContractCommand, FuturesOptionContractEntityId>(cmd!);
            });

        // Yield Curve Rate Commands
        endpoints.MapPost(MarketDataUriPath.AddYieldCurveRate, async (IActorService e, AddYieldCurveRateParameter cmdParam)
            => {
                var entityId = cmdParam.YieldCurveRate.EntityId;
                AddYieldCurveRateCommand cmd = new(cmdParam.YieldCurveRate, cmdParam.Overwrite)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, AddYieldCurveRateCommand.Actor, AddYieldCurveRateCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<AddYieldCurveRateCommand, YieldCurveRateEntityId>(cmd!);
            });

        endpoints.MapPost(MarketDataUriPath.ChangeYieldCurveRate, async (IActorService e, ChangeYieldCurveRateParameter cmdParam)
            => {
                var entityId = cmdParam.YieldCurveRate.EntityId;
                ChangeYieldCurveRateCommand cmd = new(cmdParam.YieldCurveRate, cmdParam.Overwrite)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, ChangeYieldCurveRateCommand.Actor, ChangeYieldCurveRateCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<ChangeYieldCurveRateCommand, YieldCurveRateEntityId>(cmd!);
            });

        endpoints.MapPost(MarketDataUriPath.RemoveYieldCurveRate, async (IActorService e, RemoveYieldCurveRateParameter cmdParam)
            => {
                var entityId = new YieldCurveRateEntityId(cmdParam.ValueDate.Year);
                RemoveYieldCurveRateCommand cmd = new(cmdParam.ValueDate, cmdParam.Overwrite)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, RemoveYieldCurveRateCommand.Actor, RemoveYieldCurveRateCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<RemoveYieldCurveRateCommand, YieldCurveRateEntityId>(cmd!);
            });

        endpoints.MapPost(MarketDataUriPath.ImportYieldCurveRates, async (IActorService e, ImportYieldCurveRatesParameter cmdParam)
            => {
                var entityId = new YieldCurveRateEntityId(cmdParam.ImportDate.Year);
                ImportYieldCurveRatesCommand cmd = new(cmdParam.ImportDate, cmdParam.YieldCurveRates)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, ImportYieldCurveRatesCommand.Actor, ImportYieldCurveRatesCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<ImportYieldCurveRatesCommand, YieldCurveRateEntityId>(cmd!);
            });

        return endpoints;
    }
}



/// <summary>
/// Provides extension methods for mapping market data feed command endpoints to an ASP.NET Core routing builder.
/// </summary>
/// <remarks>Use this class to register HTTP endpoints for market data feed operations, such as starting,
/// stopping, and resetting feeds, adding and removing trade live feeds, and deleting streaming request identifiers.
/// These endpoints are intended to be used with an actor-based service architecture and facilitate
/// command-based interactions for market data feed entities within the application.</remarks>
public static class MarketDataFeedCommands
{
    public static IEndpointRouteBuilder MapMarketDataFeedCommands(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(MarketDataFeedUriPath.StartMarketDataFeed, async (IActorService e, StartMarketDataFeedParameter cmdParam)
            => {
                var entityId = new MarketDataFeedId(cmdParam.ValueDate);
                StartMarketDataFeedCommand cmd = new(cmdParam.FuturesContracts, cmdParam.ValueDate, cmdParam.ResetStream)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, StartMarketDataFeedCommand.Actor, StartMarketDataFeedCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<StartMarketDataFeedCommand, MarketDataFeedId>(cmd!);
            });

        endpoints.MapPost(MarketDataFeedUriPath.StopMarketDataFeed, async (IActorService e, StopMarketDataFeedParameter cmdParam)
            => {
                var entityId = new MarketDataFeedId(cmdParam.ValueDate);
                StopMarketDataFeedCommand cmd = new(cmdParam.ValueDate)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, StopMarketDataFeedCommand.Actor, StopMarketDataFeedCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<StopMarketDataFeedCommand, MarketDataFeedId>(cmd!);
            });

        endpoints.MapPost(MarketDataFeedUriPath.ResetMarketDataFeed, async (IActorService e, ResetMarketDataFeedParameter cmdParam)
            => {
                var entityId = new MarketDataFeedId(cmdParam.ValueDate);
                ResetMarketDataFeedCommand cmd = new(cmdParam.FuturesContracts, cmdParam.ValueDate)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, ResetMarketDataFeedCommand.Actor, ResetMarketDataFeedCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<ResetMarketDataFeedCommand, MarketDataFeedId>(cmd!);
            });

        endpoints.MapPost(MarketDataFeedUriPath.AddTradeLiveFeed, async (IActorService e, AddTradeLiveFeedParameter cmdParam)
            => {
                var entityId = new TradeOrderId(cmdParam.OrderId, cmdParam.TradeId);
                AddTradeLiveFeedCommand cmd = new(cmdParam.OrderId, cmdParam.TradeId, cmdParam.ValueDate)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, AddTradeLiveFeedCommand.Actor, AddTradeLiveFeedCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<AddTradeLiveFeedCommand, TradeOrderId>(cmd!);
            });

        endpoints.MapPost(MarketDataFeedUriPath.RemoveTradeLiveFeed, async (IActorService e, RemoveTradeLiveFeedParameter cmdParam)
            => {
                var entityId = new TradeOrderId(cmdParam.OrderId, cmdParam.TradeId);
                RemoveTradeLiveFeedCommand cmd = new(cmdParam.OrderId, cmdParam.TradeId, cmdParam.ValueDate)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, RemoveTradeLiveFeedCommand.Actor, RemoveTradeLiveFeedCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<RemoveTradeLiveFeedCommand, TradeOrderId>(cmd!);
            });

        endpoints.MapPost(MarketDataFeedUriPath.HaltTradeLiveFeed, async (IActorService e, HaltTradeLiveFeedParameter cmdParam)
            => {
                var entityId = new TradeOrderId(cmdParam.OrderId, cmdParam.TradeId);
                HaltTradeLiveFeedCommand cmd = new(cmdParam.OrderId, cmdParam.TradeId)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, HaltTradeLiveFeedCommand.Actor, HaltTradeLiveFeedCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<HaltTradeLiveFeedCommand, TradeOrderId>(cmd!);
            });

        endpoints.MapPost(MarketDataFeedUriPath.TurnTradeLiveFeedOn, async (IActorService e, TurnTradeLiveFeedOnCommand cmd)
            => {
                var entityId = new TradeLiveFeedId(cmd.OrderId, cmd.TradeId, cmd.EntityId.ValueDate);
                TurnTradeLiveFeedOnCommand newCmd = new(cmd.OrderId, cmd.TradeId, cmd.EntityId.ValueDate)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, TurnTradeLiveFeedOnCommand.Actor, TurnTradeLiveFeedOnCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = TurnTradeLiveFeedOnCommand.ErrorId
                };
                return await e.RequestAsync<TurnTradeLiveFeedOnCommand, TradeLiveFeedId>(newCmd!);
            });

        endpoints.MapPost(MarketDataFeedUriPath.TurnTradeLiveFeedOff, async (IActorService e, TurnTradeLiveFeedOffCommand cmd)
            => {
                var entityId = new TradeLiveFeedId(cmd.OrderId, cmd.TradeId, cmd.EntityId.ValueDate);
                TurnTradeLiveFeedOffCommand newCmd = new(cmd.OrderId, cmd.TradeId, cmd.EntityId.ValueDate)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, TurnTradeLiveFeedOffCommand.Actor, TurnTradeLiveFeedOffCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = TurnTradeLiveFeedOffCommand.ErrorId
                };
                return await e.RequestAsync<TurnTradeLiveFeedOffCommand, TradeLiveFeedId>(newCmd!);
            });

        endpoints.MapPost(MarketDataFeedUriPath.DeleteStreamingRequestId, async (IActorService e, DeleteStreamingRequestIdParameter cmdParam)
            => {
                var entityId = cmdParam.FeedId;
                DeleteStreamingRequestIdCommand cmd = new(cmdParam.FeedId)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, DeleteStreamingRequestIdCommand.Actor, DeleteStreamingRequestIdCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<DeleteStreamingRequestIdCommand, FeedId>(cmd!);
            });

        endpoints.MapPost(MarketDataFeedUriPath.StartFuturesBarDataStreaming, async (IActorService e, StartFuturesBarDataStreamingParameter cmdParam)
           => {
               var entityId = new FuturesBarDataStreamingId(cmdParam.ValueDate);
               StartFuturesBarDataStreamingCommand cmd = new(cmdParam.FuturesContracts, cmdParam.ValueDate)
               {
                   CommandId = Guid.NewGuid(),
                   Subject = new ActorSubject(ActorType.Command, StartFuturesBarDataStreamingCommand.Actor, StartFuturesBarDataStreamingCommand.Verb, entityId.Format()),
                   EntityId = entityId,
                   ErrorCode = cmdParam.ErrorCode
               };
               return await e.RequestAsync<StartFuturesBarDataStreamingCommand, FuturesBarDataStreamingId>(cmd!);
           });

        endpoints.MapPost(MarketDataFeedUriPath.StopFuturesBarDataStreaming, async (IActorService e, StopFuturesBarDataStreamingParameter cmdParam)
            => {
                var entityId = new FuturesBarDataStreamingId(cmdParam.ValueDate);
                StopFuturesBarDataStreamingCommand cmd = new(cmdParam.ValueDate)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, StopFuturesBarDataStreamingCommand.Actor, StopFuturesBarDataStreamingCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<StopFuturesBarDataStreamingCommand, FuturesBarDataStreamingId>(cmd!);
            });

        endpoints.MapPost(MarketDataFeedUriPath.InsertFuturesBarData, async (IActorService e, InsertFuturesBarDataParameter cmdParam)
            => {
                var entityId = cmdParam.FuturesBarData.Id;
                InsertFuturesBarDataCommand cmd = new(cmdParam.FuturesBarData)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, InsertFuturesBarDataCommand.Actor, InsertFuturesBarDataCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<InsertFuturesBarDataCommand, FuturesBarDataId>(cmd!);
            });

        endpoints.MapPost(MarketDataFeedUriPath.InsertFuturesClosingPrice, async (IActorService e, InsertFuturesClosingPriceParameter cmdParam)
            => {
                var entityId = cmdParam.Id;
                InsertFuturesClosingPriceCommand cmd = new(entityId, cmdParam.ClosingPrice)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, InsertFuturesClosingPriceCommand.Actor, InsertFuturesClosingPriceCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<InsertFuturesClosingPriceCommand, FuturesDataId>(cmd!);
            });

        endpoints.MapPost(MarketDataFeedUriPath.DeleteFuturesBarData, async (IActorService e, DeleteFuturesBarDataParameter cmdParam)
            => {
                var entityId = cmdParam.Id;
                DeleteFuturesBarDataCommand cmd = new(entityId)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, DeleteFuturesBarDataCommand.Actor, DeleteFuturesBarDataCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<DeleteFuturesBarDataCommand, FuturesBarDataId>(cmd);
            });

        endpoints.MapPost(MarketDataFeedUriPath.InsertFuturesEodData, async (IActorService e, InsertFuturesEodDataParameter o)
            => {
                var entityId = new FuturesEodDataId(o.Contract.ContractId, o.ValueDate);
                InsertFuturesEodDataCommand cmd = new(o.ValueDate, o.FuturesTickData, o.Contract, o.EodDataToday, o.EodDataRange, o.NormCurveData, o.WindowSize, o.VixEodData)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, InsertFuturesEodDataCommand.Actor, InsertFuturesEodDataCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = o.ErrorCode
                };
                return await e.RequestAsync<InsertFuturesEodDataCommand, FuturesEodDataId>(cmd);
            });

        endpoints.MapPost(MarketDataFeedUriPath.InsertVixFuturesEodData, async (IActorService e, InsertVixFuturesEodDataParameter o)
            => {
                var entityId = new FuturesEodDataId(o.VixFuturesTickData.ContractId ?? string.Empty,
                                        o.VixFuturesTickData.ValueDate);
                InsertVixFuturesEodDataCommand cmd = new(o.VixFuturesTickData)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, InsertVixFuturesEodDataCommand.Actor, InsertVixFuturesEodDataCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = o.ErrorCode
                };
                return await e.RequestAsync<InsertVixFuturesEodDataCommand, FuturesEodDataId>(cmd);
            });

        endpoints.MapPost(MarketDataFeedUriPath.InsertFuturesOptionTickData, async (IActorService e, InsertFuturesOptionTickDataParameter cmdParam)
            => {
                var entityId = cmdParam.FuturesOptionTickData.EntityId;
                InsertFuturesOptionTickDataCommand cmd = new(cmdParam.FuturesContract, cmdParam.FuturesOptionTickData)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, InsertFuturesOptionTickDataCommand.Actor, InsertFuturesOptionTickDataCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<InsertFuturesOptionTickDataCommand, FuturesOptionTickEntityId>(cmd!);
            });

        endpoints.MapPost(MarketDataFeedUriPath.StartFuturesOptionTickDataStreaming, async (IActorService e, StartFuturesOptionTickDataStreamingParameter cp)
           => {
               var entityId = cp.EntityId;
               StartFuturesOptionTickDataStreamingCommand cmd = new(cp.EntityId, cp.OptionContract, cp.BaseContract, cp.ValueDate, cp.MaturityDate, cp.RiskFreeRate)
               {
                   CommandId = Guid.NewGuid(),
                   Subject = new ActorSubject(ActorType.Command, StartFuturesOptionTickDataStreamingCommand.Actor, StartFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
                   EntityId = entityId,
                   ErrorCode = cp.ErrorCode
               };
               return await e.RequestAsync<StartFuturesOptionTickDataStreamingCommand, FuturesOptionTickEntityId>(cmd!);
           });

        endpoints.MapPost(MarketDataFeedUriPath.StopFuturesOptionTickDataStreaming, async (IActorService e, StopFuturesOptionTickDataStreamingParameter cp)
            => {
                var entityId = cp.EntityId;
                StopFuturesOptionTickDataStreamingCommand cmd = new(cp.EntityId, cp.ContractId)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, StopFuturesOptionTickDataStreamingCommand.Actor, StopFuturesOptionTickDataStreamingCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cp.ErrorCode
                };
                return await e.RequestAsync<StopFuturesOptionTickDataStreamingCommand, FuturesOptionTickEntityId>(cmd!);
            });

        endpoints.MapPost(MarketDataFeedUriPath.InsertFuturesTickData, async (IActorService e, InsertFuturesTickDataParameter cp)
           => {
               var entityId = new FuturesDataId(cp.FuturesTickData.ContractId, cp.FuturesTickData.ValueDate);
               InsertFuturesTickDataCommand cmd = new(cp.FuturesContract, cp.FuturesTickData)
               {
                   CommandId = Guid.NewGuid(),
                   Subject = new ActorSubject(ActorType.Command, InsertFuturesTickDataCommand.Actor, InsertFuturesTickDataCommand.Verb, entityId.Format()),
                   EntityId = entityId,
                   ErrorCode = cp.ErrorCode
               };
               return await e.RequestAsync<InsertFuturesTickDataCommand, FuturesDataId>(cmd!);
           });

        endpoints.MapPost(MarketDataFeedUriPath.StartFuturesTickDataStreaming, async (IActorService e, StartFuturesTickDataStreamingParameter cp)
           => {
               var entityId = new FuturesDataId(cp.FuturesContract.ContractId, cp.ValueDate);
               StartFuturesTickDataStreamingCommand cmd = new(cp.FuturesContract, cp.ValueDate, cp.ResetStream)
               {
                   CommandId = Guid.NewGuid(),
                   Subject = new ActorSubject(ActorType.Command, StartFuturesTickDataStreamingCommand.Actor, StartFuturesTickDataStreamingCommand.Verb, entityId.Format()),
                   EntityId = entityId,
                   ErrorCode = cp.ErrorCode
               };
               return await e.RequestAsync<StartFuturesTickDataStreamingCommand, FuturesDataId>(cmd!);
           });

        endpoints.MapPost(MarketDataFeedUriPath.StopFuturesTickDataStreaming, async (IActorService e, StopFuturesTickDataStreamingParameter cp)
            => {
                var entityId = new FuturesDataId(cp.ContractId, cp.ValueDate);
                StopFuturesTickDataStreamingCommand cmd = new(cp.ContractId, cp.ValueDate)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, StopFuturesTickDataStreamingCommand.Actor, StopFuturesTickDataStreamingCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cp.ErrorCode
                };
                return await e.RequestAsync<StopFuturesTickDataStreamingCommand, FuturesDataId>(cmd!);
            });

        return endpoints;
    }

}

public static class OptionPricerCommands
{
    public static IEndpointRouteBuilder MapOptionPricerCommands(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(OptionPricerUriPath.InsertSpreadDistribution, async (IActorService e, InsertSpreadDistributionParameter cmdParam)
            => {
                InsertSpreadDistributionCommand cmd = new(
                    cmdParam.PutSpreadDistribution,
                    cmdParam.CallSpreadDistribution);
                var entityId = cmd.EntityId;
                cmd = cmd with
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, InsertSpreadDistributionCommand.Actor, InsertSpreadDistributionCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<InsertSpreadDistributionCommand, SpreadDistributionEntityId>(cmd);
            });

        endpoints.MapPost(OptionPricerUriPath.DeleteSpreadDistribution, async (IActorService e, DeleteSpreadDistributionParameter cmdParam)
            => {
                var entityId = new SpreadDistributionEntityId(cmdParam.TradeId, cmdParam.ValueDate);
                DeleteSpreadDistributionCommand cmd = new()
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, DeleteSpreadDistributionCommand.Actor, DeleteSpreadDistributionCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    TradeId = cmdParam.TradeId,
                    ValueDate = cmdParam.ValueDate,
                    RouteTo = BoundedContextName.SpreadDistributionBoundedContext,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<DeleteSpreadDistributionCommand, SpreadDistributionEntityId>(cmd);
            });

        endpoints.MapPost(OptionPricerUriPath.SubmitSpreadDistributionJob, async (IActorService e, SubmitSpreadDistributionJobParameter cmdParam)
            => {
                SubmitSpreadDistributionJobCommand cmd = new(cmdParam.SpreadDistributionJob);
                var entityId = cmd.EntityId;
                cmd = cmd with
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, SubmitSpreadDistributionJobCommand.Actor, SubmitSpreadDistributionJobCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<SubmitSpreadDistributionJobCommand, SpreadDistributionJobEntityId>(cmd);
            });

        endpoints.MapPost(OptionPricerUriPath.CompleteSpreadDistributionJob, async (IActorService e, CompleteSpreadDistributionJobParameter cmdParam)
            => {
                var entityId = cmdParam.EntityId;
                CompleteSpreadDistributionJobCommand cmd = new(entityId, cmdParam.JobCompleted, cmdParam.JobStatus)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, CompleteSpreadDistributionJobCommand.Actor, CompleteSpreadDistributionJobCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<CompleteSpreadDistributionJobCommand, SpreadDistributionJobEntityId>(cmd);
            });

        endpoints.MapPost(OptionPricerUriPath.FailSpreadDistributionJob, async (IActorService e, FailSpreadDistributionJobParameter cmdParam)
            => {
                var entityId = cmdParam.EntityId;
                FailSpreadDistributionJobCommand cmd = new(entityId, cmdParam.JobFailed, cmdParam.JobStatus, cmdParam.ErrorMessage)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, FailSpreadDistributionJobCommand.Actor, FailSpreadDistributionJobCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<FailSpreadDistributionJobCommand, SpreadDistributionJobEntityId>(cmd);
            });

        endpoints.MapPost(OptionPricerUriPath.ClearSpreadDistributionJob, async (IActorService e, ClearSpreadDistributionJobParameter cmdParam)
            => {
                var entityId = cmdParam.EntityId;
                ClearSpreadDistributionJobCommand cmd = new(entityId)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, ClearSpreadDistributionJobCommand.Actor, ClearSpreadDistributionJobCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<ClearSpreadDistributionJobCommand, SpreadDistributionJobEntityId>(cmd);
            });

        endpoints.MapPost(OptionPricerUriPath.DeleteSpreadDistributionJobsInProgress, async (IActorService e, DeleteSpreadDistributionJobsInProgressParameter cmdParam)
            => {
                var entityId = cmdParam.EntityId;
                DeleteSpreadDistributionJobsInProgressCommand cmd = new(entityId)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, DeleteSpreadDistributionJobsInProgressCommand.Actor, DeleteSpreadDistributionJobsInProgressCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cmdParam.ErrorCode
                };
                return await e.RequestAsync<DeleteSpreadDistributionJobsInProgressCommand, SpreadDistributionJobEntityId>(cmd);
            });

        return endpoints;
    }
}

public static class MarketDataAnalyticsCommands
{
    public static IEndpointRouteBuilder MapMarketDataAnalyticsCommands(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(MarketDataAnalyticsUriPath.GenerateFuturesItiSignal, async (IActorService e, GenerateFuturesItiSignalParameter cp)
            => {
                var entityId = new FuturesItiSignalEntityId(cp.ContractId, cp.ValueDate, cp.TimePeriod);
                GenerateFuturesItiSignalCommand cmd = new(
                    cp.ContractId,
                    cp.ValueDate,
                    cp.TimePeriod,
                    cp.Timestamp,
                    cp.FuturesPrice,
                    cp.VixFuturesPrice)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, GenerateFuturesItiSignalCommand.Actor, GenerateFuturesItiSignalCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cp.ErrorCode
                };
                return await e.RequestAsync<GenerateFuturesItiSignalCommand, FuturesItiSignalEntityId>(cmd!);
            });

        endpoints.MapPost(MarketDataAnalyticsUriPath.SetFuturesItiSignalHoldTrade, async (IActorService e, SetFuturesItiSignalHoldTradeParameter cp)
            => {
                var entityId = new FuturesItiSignalEntityId(cp.ItiSignalId.ContractId, cp.ItiSignalId.ValueDate, cp.ItiSignalId.TimePeriod);
                SetFuturesItiSignalHoldTradeCommand cmd = new(cp.ItiSignalId.ContractId, cp.ItiSignalId.ValueDate, cp.ItiSignalId.TimePeriod, cp.ItiSignalId.IntrinsicTime)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, SetFuturesItiSignalHoldTradeCommand.Actor, SetFuturesItiSignalHoldTradeCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cp.ErrorCode
                };
                return await e.RequestAsync<SetFuturesItiSignalHoldTradeCommand, FuturesItiSignalEntityId>(cmd!);
            });

        endpoints.MapPost(MarketDataAnalyticsUriPath.ClearFuturesItiSignalHoldTrade, async (IActorService e, ClearFuturesItiSignalHoldTradeParameter cp)
            => {
                var entityId = new FuturesItiSignalEntityId(cp.ItiSignalId.ContractId, cp.ItiSignalId.ValueDate, cp.ItiSignalId.TimePeriod);
                ClearFuturesItiSignalHoldTradeCommand cmd = new(cp.ItiSignalId.ContractId, cp.ItiSignalId.ValueDate, cp.ItiSignalId.TimePeriod, cp.ItiSignalId.IntrinsicTime)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, ClearFuturesItiSignalHoldTradeCommand.Actor, ClearFuturesItiSignalHoldTradeCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = cp.ErrorCode
                };
                return await e.RequestAsync<ClearFuturesItiSignalHoldTradeCommand, FuturesItiSignalEntityId>(cmd!);
            });

        endpoints.MapPost(MarketDataAnalyticsUriPath.GenerateFuturesAtrSignal, async (IActorService e, GenerateFuturesAtrSignalParameter cp)
            => {
                var entityId = cp.FuturesAtrSignalId.ToEntityId();
                var futuresPrice = cp.FuturesItiSignals.Length > 0 ? (decimal)cp.FuturesItiSignals[^1].IntrinsicPrice : 0m;
                GenerateFuturesAtrSignalCommand cmd = new(cp.FuturesAtrSignalId, futuresPrice)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, GenerateFuturesAtrSignalCommand.Actor, GenerateFuturesAtrSignalCommand.Verb, entityId.Format()),
                    ErrorCode = cp.ErrorCode
                };
                return await e.RequestAsync<GenerateFuturesAtrSignalCommand, FuturesAtrSignalEntityId>(cmd!);
            });

        endpoints.MapPost(MarketDataAnalyticsUriPath.GenerateFuturesAdxSignal, async (IActorService e, GenerateFuturesAdxSignalParameter cp)
            => {
                var entityId = new FuturesAdxSignalEntityId(cp.FuturesAdxSignalId.ContractId, cp.FuturesAdxSignalId.ValueDate, cp.FuturesAdxSignalId.TimePeriod, cp.FuturesAdxSignalId.PeriodLength);
                GenerateFuturesAdxSignalCommand cmd = new(cp.FuturesAdxSignalId, cp.FuturesPrice)
                {
                    CommandId = Guid.CreateVersion7(),
                    Subject = new ActorSubject(ActorType.Command, GenerateFuturesAdxSignalCommand.Actor, GenerateFuturesAdxSignalCommand.Verb, entityId.Format()),
                    ErrorCode = cp.ErrorCode
                };
                return await e.RequestAsync<GenerateFuturesAdxSignalCommand, FuturesAdxSignalEntityId>(cmd!);
            });

        endpoints.MapPost(MarketDataAnalyticsUriPath.GenerateFuturesMacdSignal, async (IActorService e, GenerateFuturesMacdSignalParameter cp)
            => {
                var entityId = cp.FuturesMacdSignalId.ToEntityId();
                GenerateFuturesMacdSignalCommand cmd = new(cp.FuturesMacdSignalId, cp.FuturesPrice)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, GenerateFuturesMacdSignalCommand.Actor, GenerateFuturesMacdSignalCommand.Verb, entityId.Format()),
                    ErrorCode = cp.ErrorCode
                };
                return await e.RequestAsync<GenerateFuturesMacdSignalCommand, FuturesMacdSignalEntityId>(cmd!);
            });

        return endpoints;
    }
}

public static class OptionTradeCommands
{
    public static IEndpointRouteBuilder MapTradeCommands(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(OptionTradeUriPath.Open, async (IActorService e, OpenOptionTradeCommand cmd)
            => {
                var entityId = new OptionTradeEntityId(cmd.TradeOrder.OrderId, cmd.TradeOrder.TradeId);
                OpenOptionTradeCommand newCmd = new(cmd.TradeOrder)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, OpenOptionTradeCommand.Actor, OpenOptionTradeCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = OpenOptionTradeCommand.ErrorId
                };
                return await e.RequestAsync<OpenOptionTradeCommand, OptionTradeEntityId>(newCmd!);
            });

        endpoints.MapPost(OptionTradeUriPath.Close, async (IActorService e, CloseOptionTradeCommand cmd)
            => {
                var entityId = new OptionTradeEntityId(cmd.TradeOrder.OrderId, cmd.TradeOrder.TradeId);
                CloseOptionTradeCommand newCmd = new(cmd.TradeOrder)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, CloseOptionTradeCommand.Actor, CloseOptionTradeCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<CloseOptionTradeCommand, OptionTradeEntityId>(newCmd!);
            });

        endpoints.MapPost(OptionTradeUriPath.Delete, async (IActorService e, DeleteOptionTradeCommand cmd)
            => {
                var entityId = new OptionTradeEntityId(cmd.OrderId, cmd.TradeId);
                DeleteOptionTradeCommand newCmd = new(cmd.OrderId, cmd.TradeId)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, DeleteOptionTradeCommand.Actor, DeleteOptionTradeCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = DeleteOptionTradeCommand.ErrorId
                };
                return await e.RequestAsync<DeleteOptionTradeCommand, OptionTradeEntityId>(newCmd!);
            });

        endpoints.MapPost(OptionTradeUriPath.DeleteOptionTrades, async (IActorService e, DeleteOptionTradesCommand cmd)
            => {
                var entityId = cmd.OrderId;
                DeleteOptionTradesCommand newCmd = new(cmd.OrderId.Id)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, DeleteOptionTradesCommand.Actor, DeleteOptionTradesCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = DeleteOptionTradesCommand.ErrorId
                };
                return await e.RequestAsync<DeleteOptionTradesCommand, OrderId>(newCmd!);
            });

        endpoints.MapPost(OptionTradeUriPath.PlaceOrder, async (IActorService e, PlaceOptionTradeOrderCommand cmd)
            => {
                var entityId = new OptionTradeEntityId(cmd.OptionTrade.OrderId, cmd.OptionTrade.TradeId);
                PlaceOptionTradeOrderCommand newCmd = new(cmd.TradeOrder, cmd.OptionTrade)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, PlaceOptionTradeOrderCommand.Actor, PlaceOptionTradeOrderCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<PlaceOptionTradeOrderCommand, OptionTradeEntityId>(newCmd!);
            });

        endpoints.MapPost(OptionTradeUriPath.Snapshot, async (IActorService e, SnapshotOptionTradeCommand cmd)
            => {
                var entityId = new OptionTradeEntityId(cmd.OrderId, cmd.TradeId);
                SnapshotOptionTradeCommand newCmd = new(cmd.OrderId, cmd.TradeId)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, SnapshotOptionTradeCommand.Actor, SnapshotOptionTradeCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = SnapshotOptionTradeCommand.ErrorId
                };
                return await e.RequestAsync<SnapshotOptionTradeCommand, OptionTradeEntityId>(newCmd!);
            });

        endpoints.MapPost(OptionTradeUriPath.ChangeLegData, async (IActorService e, ChangeOptionTradeLegDataCommand cmd)
            => {
                var entityId = new OptionTradeEntityId(cmd.OrderId, cmd.TradeId);
                ChangeOptionTradeLegDataCommand newCmd = new(cmd.OrderId, cmd.TradeId, cmd.TradeType, cmd.ValueDate, cmd.TradeStatus, cmd.AssetPrice, cmd.RiskFreeRate, cmd.OptionLegData)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, ChangeOptionTradeLegDataCommand.Actor, ChangeOptionTradeLegDataCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<ChangeOptionTradeLegDataCommand, OptionTradeEntityId>(newCmd!);
            });

        endpoints.MapPost(OptionTradeUriPath.ChangeDistributionStatistics, async (IActorService e, UpdateOptionTradeSpreadDistributionStatisticsCommand cmd)
            => {
                var entityId = new OptionTradeEntityId(cmd.OrderId, cmd.TradeId);
                UpdateOptionTradeSpreadDistributionStatisticsCommand newCmd = new(cmd.OrderId, cmd.TradeId, cmd.TradeType, cmd.TradeStatus, cmd.ValueDate, cmd.DaysToExpiry, cmd.PutSpreadDistribution, cmd.CallSpreadDistribution)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, UpdateOptionTradeSpreadDistributionStatisticsCommand.Actor, UpdateOptionTradeSpreadDistributionStatisticsCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = UpdateOptionTradeSpreadDistributionStatisticsCommand.ErrorId
                };
                return await e.RequestAsync<UpdateOptionTradeSpreadDistributionStatisticsCommand, OptionTradeEntityId>(newCmd!);
            });

        endpoints.MapPost(OptionTradeUriPath.ProcessEndOfDay, async (IActorService e, ProcessOptionTradeEndOfDayCommand cmd)
            => {
                var entityId = new OptionTradeEntityId(cmd.OrderId, cmd.TradeId);
                ProcessOptionTradeEndOfDayCommand newCmd = new(cmd.FundId, cmd.OrderId, cmd.TradeId, cmd.TradeType, cmd.ValueDate, cmd.TradeStatus, cmd.OpenPrice, cmd.HighPrice, cmd.LowPrice, cmd.ClosePrice, cmd.Volume, cmd.Reference)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, ProcessOptionTradeEndOfDayCommand.Actor, ProcessOptionTradeEndOfDayCommand.Verb, entityId.Format()),
                    EntityId = entityId
                };
                return await e.RequestAsync<ProcessOptionTradeEndOfDayCommand, OptionTradeEntityId>(newCmd!);
            });

        endpoints.MapPost(OptionTradeUriPath.InsertSpreadData, async (IActorService e, InsertOptionTradeSpreadDataCommand cmd)
            => {
                var entityId = new OptionTradeEntityId(cmd.OptionTradeSpreadData.OrderId, cmd.OptionTradeSpreadData.TradeId);
                InsertOptionTradeSpreadDataCommand newCmd = new(cmd.OptionTradeSpreadData)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, InsertOptionTradeSpreadDataCommand.Actor, InsertOptionTradeSpreadDataCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = InsertOptionTradeSpreadDataCommand.ErrorId
                };
                return await e.RequestAsync<InsertOptionTradeSpreadDataCommand, OptionTradeEntityId>(newCmd!);
            });

        endpoints.MapPost(OptionTradeUriPath.InsertSpreadBarData, async (IActorService e, InsertOptionTradeSpreadBarDataCommand cmd)
            => {
                var entityId = new OptionTradeEntityId(cmd.OptionTradeSpreadBarData.OrderId, cmd.OptionTradeSpreadBarData.TradeId);
                InsertOptionTradeSpreadBarDataCommand newCmd = new(cmd.OptionTradeSpreadBarData)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, InsertOptionTradeSpreadBarDataCommand.Actor, InsertOptionTradeSpreadBarDataCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = InsertOptionTradeSpreadBarDataCommand.ErrorId
                };
                return await e.RequestAsync<InsertOptionTradeSpreadBarDataCommand, OptionTradeEntityId>(newCmd!);
            });

        endpoints.MapPost(OptionTradeUriPath.DeleteSpreadBarData, async (IActorService e, DeleteOptionTradeSpreadBarDataCommand cmd)
            => {
                var entityId = cmd.OptionTradeId;
                DeleteOptionTradeSpreadBarDataCommand newCmd = new(cmd.OptionTradeId, cmd.TradeType, cmd.ValueDate)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, DeleteOptionTradeSpreadBarDataCommand.Actor, DeleteOptionTradeSpreadBarDataCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = DeleteOptionTradeSpreadBarDataCommand.ErrorId
                };
                return await e.RequestAsync<DeleteOptionTradeSpreadBarDataCommand, OptionTradeEntityId>(newCmd!);
            });

        endpoints.MapPost(OptionTradeUriPath.UpdateDailyProfitTarget, async (IActorService e, UpdateOptionTradeDailyProfitTargetCommand cmd)
            => {
                var entityId = new OptionTradeEntityId(cmd.OrderId, cmd.TradeId);
                UpdateOptionTradeDailyProfitTargetCommand newCmd = new(cmd.OrderId, cmd.TradeId, cmd.TradingDays, cmd.MaxTradingDays)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, UpdateOptionTradeDailyProfitTargetCommand.Actor, UpdateOptionTradeDailyProfitTargetCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = UpdateOptionTradeDailyProfitTargetCommand.ErrorId
                };
                return await e.RequestAsync<UpdateOptionTradeDailyProfitTargetCommand, OptionTradeEntityId>(newCmd!);
            });

        return endpoints;
    }
}

public static class TradePlanCommands
{
    public static IEndpointRouteBuilder MapTradePlanCommands(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(TradePlanUriPath.Update, async (IActorService e, UpdateTradePlanCommand cmd)
            => {
                var entityId = new TradePlanEntityId(cmd.TradePlan.OrderId, cmd.TradePlan.TradeId, cmd.TradePlan.ValueDate);
                UpdateTradePlanCommand newCmd = new(cmd.TradePlan)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, UpdateTradePlanCommand.Actor, UpdateTradePlanCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = UpdateTradePlanCommand.ErrorId
                };
                return await e.RequestAsync<UpdateTradePlanCommand, TradePlanEntityId>(newCmd!);
            });

        endpoints.MapPost(TradePlanUriPath.UpdateForwardLossLimit, async (IActorService e, UpdateTradePlanForwardLossLimitCommand cmd)
            => {
                var entityId = new TradePlanForwardLossLimitEntityId(cmd.TradePlanForwardLossLimit.OrderId, cmd.TradePlanForwardLossLimit.TradeId, cmd.TradePlanForwardLossLimit.ValueDate, cmd.TradePlanForwardLossLimit.TradeType);
                UpdateTradePlanForwardLossLimitCommand newCmd = new(cmd.TradePlanForwardLossLimit)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, UpdateTradePlanForwardLossLimitCommand.Actor, UpdateTradePlanForwardLossLimitCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = UpdateTradePlanForwardLossLimitCommand.ErrorId
                };
                return await e.RequestAsync<UpdateTradePlanForwardLossLimitCommand, TradePlanForwardLossLimitEntityId>(newCmd!);
            });

        endpoints.MapPost(TradePlanUriPath.ClearForwardLossLimit, async (IActorService e, ClearTradePlanForwardLossLimitCommand cmd)
            => {
                var entityId = cmd.TradePlanForwardLossLimitId;
                ClearTradePlanForwardLossLimitCommand newCmd = new(cmd.TradePlanForwardLossLimitId)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, ClearTradePlanForwardLossLimitCommand.Actor, ClearTradePlanForwardLossLimitCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = ClearTradePlanForwardLossLimitCommand.ErrorId
                };
                return await e.RequestAsync<ClearTradePlanForwardLossLimitCommand, TradePlanForwardLossLimitEntityId>(newCmd!);
            });

        return endpoints;
    }
}

public static class SystemAdminCommands
{
    public static IEndpointRouteBuilder MapSystemAdminCommands(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(SystemAdminUriPath.BackupDatabase, async (IActorService e, BackupDatabaseCommand cp)
            => {
                var entityId = new DatabaseBackupId(cp.DatabaseName);
                BackupDatabaseCommand cmd = new(cp.DatabaseName, cp.BackupType, cp.CommandTimeout)
                {
                    CommandId = Guid.NewGuid(),
                    Subject = new ActorSubject(ActorType.Command, BackupDatabaseCommand.Actor, BackupDatabaseCommand.Verb, entityId.Format()),
                    EntityId = entityId,
                    ErrorCode = BackupDatabaseCommand.ErrorId
                };
                return await e.RequestAsync<BackupDatabaseCommand, DatabaseBackupId>(cmd!);
            });

        return endpoints;
    }
}


