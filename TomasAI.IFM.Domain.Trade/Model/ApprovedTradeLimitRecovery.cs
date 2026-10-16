using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
namespace TomasAI.IFM.Domain.Trade.Model;
/// <summary>Explicit backend repair of missing Iron Condor monitoring limits using original saved business evidence.</summary>
public static class ApprovedTradeLimitRecovery
{
    /// <summary>Recovers a missing limit through its source command; existing limits are preserved.</summary>
    public static async Task<bool> RecoverAsync(ITradeDbContext database, IActorService actors, TradeEntityId id,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(actors);
        if (!id.IsValid) throw new ArgumentException("A complete trade identity is required.", nameof(id));
        if (await database.GetTradeLimitAsync(id.TradeId, token).ConfigureAwait(false) is not null) return false;
        var trade = await database.GetEstablishedTradeAsync(id, token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("TRADE_LIMITS.TRADE_NOT_FOUND");
        var order = await database.GetTradeOrderAsync(new(id.PortfolioId,id.FundId,id.OrderId), token).ConfigureAwait(false)
            ?? throw new InvalidOperationException("TRADE_LIMITS.ORIGINAL_ORDER_NOT_FOUND");
        var command = CreateCommand(order,trade);
        var position = command.EntityId;
        var result = await actors.SendAsync<InitializeIronCondorMonitoringCommand,StrategyPositionId>(command,position).ConfigureAwait(false);
        if (!result.Success) throw new InvalidOperationException($"TRADE_LIMITS.RECOVERY_FAILED;{result.ErrorCode};{result.ErrorMessage}");
        return true;
    }

    /// <summary>Captures one deterministic recovery command from original immutable approval and execution evidence.</summary>
    public static InitializeIronCondorMonitoringCommand CreateCommand(TradeOrderDefinition order, EstablishedTradeDefinition trade)
    {
        _ = ApprovedTradeMonitoringLimits.Create(order,trade);
        var id = trade.Id;
        var position = StrategyPositionId.Create(id,TradeStrategyKind.IronCondor);
        return new InitializeIronCondorMonitoringCommand
        {
            CommandId = TradeHandoffIdentity.Create("recover-approved-trade-limits", id.Format(), order.PortfolioApprovalId.ToString("N")),
            EntityId = position,
            Subject = new(ActorType.Command, PositionActorNames.IronCondorCommand, InitializeIronCondorMonitoringCommand.Verb, position.Format()),
            MonitoringInitialization = new() { IronCondorTrade = trade, ApprovedOrder = order,
                InitializedAtUtc = trade.EstablishedAtUtc, ValueDate = trade.TradeDate!.Value,
                RequiredCapital = order.RequiredCapital, TradeOrderRevision = order.Revision }
        };
    }
}
