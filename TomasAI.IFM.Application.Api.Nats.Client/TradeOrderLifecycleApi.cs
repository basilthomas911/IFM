using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.Api.Nats.Client;

/// <summary>Dispatches the standard Trade Order actor sequence for Portfolio-accepted orders.</summary>
/// <param name="producer">The actor-message producer used to issue lifecycle commands.</param>
public sealed class TradeOrderLifecycleApi(IActorProducer producer)
    : NatsClientApi(producer), ITradeOrderLifecycleApi
{
    /// <inheritdoc />
    public async Task<ServiceResult<Guid>> SubmitAcceptedAsync(
        TradeOrderDefinition order,
        Guid portfolioEventId,
        ExecutionChannel channel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (!order.Id.IsValid)
            throw new ArgumentException("A Portfolio-accepted Trade Order identity is required.", nameof(order));
        if (portfolioEventId == Guid.Empty)
            throw new ArgumentException("The authorizing Portfolio event is required.", nameof(portfolioEventId));

        var seed = StableId(portfolioEventId.ToString("N"), order.Id.Format());
        var create = await SendAsync(new CreateTradeOrderCommand
        {
            CommandId = StableId(seed.ToString("N"), "create"),
            Subject = Subject(CreateTradeOrderCommand.Verb, order.Id),
            EntityId = order.Id,
            Order = order
        }, cancellationToken).ConfigureAwait(false);
        if (!create.Success) return create;

        var approve = await SendAsync(new ApproveTradeOrderCommand
        {
            CommandId = StableId(seed.ToString("N"), "approve"),
            Subject = Subject(ApproveTradeOrderCommand.Verb, order.Id),
            EntityId = order.Id
        }, cancellationToken).ConfigureAwait(false);
        if (!approve.Success) return approve;

        var ready = await SendAsync(new ReadyTradeOrderCommand
        {
            CommandId = StableId(seed.ToString("N"), "ready"),
            Subject = Subject(ReadyTradeOrderCommand.Verb, order.Id),
            EntityId = order.Id
        }, cancellationToken).ConfigureAwait(false);
        if (!ready.Success) return ready;

        var attemptId = StableId(seed.ToString("N"), "execution-attempt");
        return await SendAsync(new BindTradeOrderExecutionCommand
        {
            CommandId = StableId(seed.ToString("N"), "bind"),
            Subject = Subject(BindTradeOrderExecutionCommand.Verb, order.Id),
            EntityId = order.Id,
            ExecutionAttemptId = attemptId,
            ExecutionChannel = channel,
            EffectiveAtUtc = DateTime.UtcNow
        }, cancellationToken).ConfigureAwait(false);
    }

    async Task<ServiceResult<Guid>> SendAsync<TCommand>(
        TCommand command,
        CancellationToken cancellationToken)
        where TCommand : class, ICommand<TradeOrderId> =>
        await RequestCommandAsync(command, command.EntityId, cancellationToken).ConfigureAwait(false);

    static ActorSubject Subject(string verb, TradeOrderId id) =>
        new(ActorType.Command, TradeOrderActorNames.Command, verb, id.Format());

    static Guid StableId(params string[] values)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', values)));
        return new Guid(bytes.AsSpan(0, 16));
    }
}
