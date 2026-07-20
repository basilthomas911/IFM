using TomasAI.IFM.Application.Storage.ScyllaDb.TradeDb;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.Trade.Actor.Queries.State;

/// <summary>
/// Represents the state for a trade query actor, managing the retrieval of trade
/// data from the trade database.
/// </summary>
/// <param name="db">The trade database context used to access trade data for queries.</param>
public class TradeQueryState(ITradeDbContext db)
    : BaseQueryActorState<TradeQueryState>, IQueryActorState<TradeQueryState>
{
    readonly ITradeDbContext _db = db;

    public override ActorThreadId Id { get; set; } = default!;

    public TradeQueryState As => this;
}
