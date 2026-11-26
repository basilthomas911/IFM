using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.EventProjector.Realtime;
using TomasAI.IFM.Application.EventProjector.Realtime.Contracts;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Domain.MarketData.Analytics.FuturesTdiSignal.Realtime.Projector;

public sealed class FuturesTdiSignalRealtimeProjector(
    IDbContextFactory dbFactory,
    ILogger<FuturesTdiSignalRealtimeProjector> logger,
    FuturesTdiResultReceipts? receipts = null)
    : BaseRealtimeProjector<FuturesTdiSignalRealtimeActor>(logger)
{
    readonly FuturesTdiResultReceipts receipts = receipts ?? new();

    /// <summary>Resumes the immutable original result; no stage recomputes the TDI cross.</summary>
    public override async ValueTask<bool> ProcessRealtimeEventAsync(IEvent domainEvent, CancellationToken cancellationToken = default)
    {
        if (domainEvent is not FuturesTdiSignalGeneratedEvent generated)
            throw new ArgumentException("TDI receipt publication requires a generated TDI event.", nameof(domainEvent));
        var receipt = receipts.Find(generated.Id) ?? throw new InvalidDataException("TDI result must be prepared before publication.");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (receipt.Stage < TdiReceiptStage.SourcePublished)
            {
                await PublishRealtimeEventAsync(Context, receipt.Generated, ActorName, cancellationToken).ConfigureAwait(false);
                receipt = receipts.Advance(generated.Id, TdiReceiptStage.SourcePublished);
            }
            if (receipt.Stage < TdiReceiptStage.Applied)
            {
                await _descriptors[0].ApplyAsync(receipt.Generated, cancellationToken).ConfigureAwait(false);
                receipt = receipts.Advance(generated.Id, TdiReceiptStage.Applied);
            }
            if (receipt.Stage < TdiReceiptStage.Completed)
            {
                await PublishRealtimeEventAsync(Context, receipt.Completed, ActorName, cancellationToken).ConfigureAwait(false);
                receipts.Advance(generated.Id, TdiReceiptStage.Completed);
            }
            else receipts.Advance(generated.Id, TdiReceiptStage.Completed); // Finish a crash-interrupted receipt retirement.
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || RealtimeActorGeneration.Current?.Token.IsCancellationRequested == true) { throw; }
        catch (Exception error)
        {
            await PublishFailureAsync(Context, _descriptors[0], receipt.Generated, error).ConfigureAwait(false);
            throw; // Preserve the failed delivery; a diagnostic failure event does not settle its input.
        }
    }

    readonly ImmutableArray<RealtimeProjectionDescriptor> _descriptors =
    [
        Describe<FuturesTdiSignalGeneratedEvent, FuturesTdiSignalGeneratedCompleteEvent,
            FuturesTdiSignalGeneratedFailEvent, FuturesTdiSignalEntityId>(
            (Func<FuturesTdiSignalGeneratedEvent, Task>)(async e =>
            {
                if (e.FuturesTdiSignal.SchemaVersion != FuturesTdiConfiguration.CurrentSchemaVersion)
                    throw new InvalidDataException("Unsupported TDI receipt schema; publication remains pending.");
                await dbFactory.MarketDataDb.InsertFuturesTdiSignalAsync(e.FuturesTdiSignal).ConfigureAwait(false);
                RegimeDiscoverySignalCacheAdapter.Publish(e.FuturesTdiSignal);
            }))
    ];

    public override string ActorName => FuturesTdiSignalRealtimeActor.ActorName;
    public override string ProjectorName => nameof(FuturesTdiSignalRealtimeProjector);
    public override IReadOnlyCollection<RealtimeProjectionDescriptor> ProjectionDescriptors => _descriptors;
}
