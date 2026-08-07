using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation.Commands;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Command.State;
using TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Command;
using TomasAI.IFM.Domain.MarketData.Feed.Command.Actor;
using TomasAI.IFM.Shared.Domain;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Feed.TickAggregation.Command.Actor;

public sealed class TickAggregationCommandActor(
    IEventSourceActorDbContext eventSource,
    ILogger<TickAggregationCommandActor> logger)
    : BaseEventSourceCommandActor<TickAggregationCommandActor>(
        logger, new ActorMailboxId(ActorType.Command, ActorName))
{
    public const string ActorName = "TickAggregationCommand";
    private readonly TickAggregationCommandAuditTracker _audit = new(eventSource);
    private readonly ConcurrentDictionary<Guid, byte> _completedRetries = [];
    private IEventSourceActorStateRepository<TickAggregationCommandState> _repository = default!;

    protected override ValueTask OnStartup(ICommandActorContext context)
    {
        _repository = context.Container.Resolve<IEventSourceActorStateRepository<TickAggregationCommandState>>();
        return ValueTask.CompletedTask;
    }

    protected override ICommand ParseMessage(ICommandActorContext context, IActorMessage message)
    {
        var subject = message.Subject;
        ICommand command = subject switch
        {
            { ActorType: ActorType.Command, Name: ActorName, Verb: InsertFuturesTickTradeDataCommand.Verb } =>
                message.AsCommand<InsertFuturesTickTradeDataCommand>()!,
            { ActorType: ActorType.Command, Name: ActorName, Verb: InsertFuturesTickQuoteDataCommand.Verb } =>
                message.AsCommand<InsertFuturesTickQuoteDataCommand>()!,
            _ => throw new InvalidOperationException($"Unable to resolve {ActorName} command from {subject}.")
        };
        _audit.Start(command);
        return command;
    }

    protected override ValueTask<ServiceResult<GuidResult>> ReceiveAsync(
        ICommandActorContext context, IActorState state, ICommand command)
    {
        var typedState = (TickAggregationCommandState)state;
        if (_completedRetries.TryRemove(command.CommandId, out _))
            return ValueTask.FromResult<ServiceResult<GuidResult>>(
                new ServiceOk<GuidResult>(new GuidResult(command.CommandId)));
        _ = command switch
        {
            InsertFuturesTickTradeDataCommand trade => trade.Execute(typedState),
            InsertFuturesTickQuoteDataCommand quote => quote.Execute(typedState),
            _ => throw new InvalidOperationException($"Unsupported tick aggregation command {command.CommandName}.")
        };
        return ValueTask.FromResult<ServiceResult<GuidResult>>(new ServiceOk<GuidResult>(new GuidResult(command.CommandId)));
    }

    protected override async ValueTask OnValidateAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command)
    {
        var completedRetry = await _audit.CompleteAsync(command).ConfigureAwait(false);
        switch (command)
        {
            case InsertFuturesTickTradeDataCommand trade:
                ValidateCommon(trade.CommandId, trade.EntityId, trade.TickDataId, trade.AssetTypeId,
                    trade.Dataset, trade.DefinitionDate, trade.PublisherId, trade.InstrumentId, trade.SchemaVersion);
                if (trade.TradeData.Price != trade.TradeData.PriceRaw / 1_000_000_000m)
                    throw new ArgumentException("Trade raw and decimal prices do not match.", nameof(trade));
                break;
            case InsertFuturesTickQuoteDataCommand quote:
                ValidateCommon(quote.CommandId, quote.EntityId, quote.TickDataId, quote.AssetTypeId,
                    quote.Dataset, quote.DefinitionDate, quote.PublisherId, quote.InstrumentId, quote.SchemaVersion);
                if (quote.QuoteCount is 0 or > FuturesTickQuoteDataSegment.MaximumCount || quote.QuoteCount != quote.QuoteData.Count)
                    throw new ArgumentOutOfRangeException(nameof(quote.QuoteCount));
                foreach (ref readonly var item in quote.QuoteData.Items)
                {
                    ValidateQuotePrice(item.BidPriceRaw, item.BidPrice, nameof(item.BidPrice));
                    ValidateQuotePrice(item.AskPriceRaw, item.AskPrice, nameof(item.AskPrice));
                }
                break;
            default:
                throw new InvalidOperationException($"Unsupported tick aggregation command {command.CommandName}.");
        }
        if (completedRetry)
            _completedRetries.TryAdd(command.CommandId, 0);
    }

    private static void ValidateCommon(
        Guid commandId,
        TickDataEntityId entity,
        TickDataId tick,
        AssetTypeId assetType,
        string dataset,
        DateOnly definitionDate,
        ushort publisherId,
        uint instrumentId,
        ushort schemaVersion)
    {
        if (commandId == Guid.Empty || schemaVersion != 1 || assetType != AssetTypeId.Futures ||
            entity.AssetTypeId != assetType || string.IsNullOrWhiteSpace(entity.ContractId) ||
            entity.ContractId != tick.ContractId || entity.ValueDate != tick.ValueDate || tick.SequenceId <= 0 ||
            tick.TimestampUtc.Kind != DateTimeKind.Utc || string.IsNullOrWhiteSpace(dataset) ||
            definitionDate == default || publisherId == 0 || instrumentId == 0)
            throw new ArgumentException("The tick aggregation identity or schema is invalid.");
    }

    private static void ValidateQuotePrice(long raw, decimal? price, string parameterName)
    {
        var expected = raw == long.MaxValue ? (decimal?)null : raw / 1_000_000_000m;
        if (price != expected)
            throw new ArgumentException("Quote raw and decimal prices do not match.", parameterName);
    }

    protected override async ValueTask<IActorState> OnLoadStateAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command) =>
        await _repository.LoadStateAsync(command).ConfigureAwait(false);

    protected override async ValueTask OnSaveStateAsync(
        ICommandActorContext context, ActorThreadId threadId, IActorState state, ICommand command) =>
        await _repository.SaveStateAsync(context, (TickAggregationCommandState)state, command).ConfigureAwait(false);

    protected override async ValueTask<ServiceResult<GuidResult>> OnExceptionAsync(
        ICommandActorContext context, ActorThreadId threadId, ICommand command, Exception exception)
    {
        if (command is not null)
            _completedRetries.TryRemove(command.CommandId, out _);
        try
        {
            var failed = await exception.SendErrorEventAsync<
                TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent,
                ActorEntityId>(ErrorType.Command, context, command, ActorEntityId.Default, ActorName,
                TomasAI.IFM.Shared.EventModelActor.Events.CommandExceptionEvent.CommandFail).ConfigureAwait(false);
            return new ServiceFailed<GuidResult>(failed);
        }
        catch (Exception fatal)
        {
            return CommandFailed(fatal, command);
        }
    }
}

internal sealed class TickAggregationCommandAuditTracker(IEventSourceActorDbContext eventSource)
{
    private readonly ConcurrentDictionary<Guid, Task<bool>> _pending = [];

    public void Start(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var task = AuditAsync(command);
        if (task.IsCompleted)
            task.GetAwaiter().GetResult();
        if (!_pending.TryAdd(command.CommandId, task))
            throw new InvalidOperationException($"Command audit {command.CommandId} is already pending.");
    }

    public async ValueTask<bool> CompleteAsync(ICommand command)
    {
        if (!_pending.TryRemove(command.CommandId, out var task))
            task = AuditAsync(command);
        return await task.ConfigureAwait(false);
    }

    private async Task<bool> AuditAsync(ICommand command)
    {
        var commandData = CreateFingerprint(command);
        if (await eventSource.TryInsertCommandLogAsync(command, DateTime.UtcNow, commandData).ConfigureAwait(false))
            return false;
        var existing = await eventSource.GetCommandLogAsync(command.CommandId).ConfigureAwait(false);
        if (existing is null)
            throw new InvalidOperationException($"Command audit {command.CommandId} conflicted but could not be reloaded.");
        if (!string.Equals(existing.StreamId, command.StreamId, StringComparison.Ordinal) ||
            !string.Equals(existing.CommandName, command.CommandName, StringComparison.Ordinal) ||
            !string.Equals(existing.CommandData, commandData, StringComparison.Ordinal))
            throw new InvalidOperationException($"Command ID {command.CommandId} was reused with different content.");
        return await eventSource.HasEventForCommandAsync(command.CommandId).ConfigureAwait(false);
    }

    internal static string CreateFingerprint(ICommand command)
    {
        var json = JsonConvert.SerializeObject(command);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}
