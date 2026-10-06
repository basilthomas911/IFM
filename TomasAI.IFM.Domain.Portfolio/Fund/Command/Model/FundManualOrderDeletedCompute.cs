using TomasAI.IFM.Domain.Portfolio.Fund.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Portfolio.Fund.Command.Model;

/// <summary>Immutable proposed FundManualOrderDeleted business change; contains no state mutation or persistence.</summary>
internal sealed record FundManualOrderDeletedCompute : IPortfolioFundChange
{
    /// <summary>Whether the proposed business transition passed computation guards.</summary>
    public bool Accepted { get; init; } = true;
    /// <summary>The business classification of a rejected transition.</summary>
    public string RejectionCode { get; init; } = string.Empty;
    /// <summary>The original business explanation for a rejected transition.</summary>
    public string RejectionReason { get; init; } = string.Empty;
    /// <summary>Creates a rejected computation without a business payload or pending event.</summary>
    internal FundManualOrderDeletedCompute() { }

    public Guid Id { get; init; }
    public Guid CommandId { get; init; }
    public long Revision { get; init; }
    public DateTime OccurredOnUtc { get; init; }
    public string Principal { get; init; } = string.Empty;
    public int OrderId { get; init; } = default!;
    public int[] RemovedTradeIds { get; init; } = [];
    /// <summary>Initializes accepted immutable business values.</summary>
    /// <param name="id">The id value.</param>
    /// <param name="commandId">The commandId value.</param>
    /// <param name="revision">The revision value.</param>
    /// <param name="occurredOnUtc">The occurredOnUtc value.</param>
    /// <param name="principal">The principal value.</param>
    /// <param name="orderId">The orderId value.</param>
    /// <param name="removedTradeIds">The economically inactive trade identifiers removed with the order.</param>
    internal FundManualOrderDeletedCompute(Guid id, Guid commandId, long revision, DateTime occurredOnUtc, string principal, int orderId, int[]? removedTradeIds = null)
    {
        Id = id;
        CommandId = commandId;
        Revision = revision;
        OccurredOnUtc = occurredOnUtc;
        Principal = principal;
        OrderId = orderId;
        RemovedTradeIds = removedTradeIds is null ? [] : [.. removedTradeIds];
    }
}
