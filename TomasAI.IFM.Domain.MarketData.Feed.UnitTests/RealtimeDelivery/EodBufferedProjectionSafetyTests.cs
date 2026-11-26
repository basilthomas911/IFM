using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Actor;
using TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Realtime.Projector;
using TomasAI.IFM.Domain.MarketData.Feed.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.TestSupport;

namespace TomasAI.IFM.Domain.MarketData.Feed.UnitTests.RealtimeDelivery;

[Collection("MarketPriceDeliverySafety")]
public sealed class EodBufferedProjectionSafetyTests
{
    [Fact]
    public async Task Buffered_projector_refuses_startup_without_a_durable_capture_owner()
    {
        // Counterexample for the next gate: neither a database commit nor a durable history owner exists.
        var directory = Path.Combine(Path.GetTempPath(), "IFM-eod-buffer-audit-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var blocked = Path.Combine(directory, "not-a-directory"); File.WriteAllText(blocked, "blocked");
        var configuration = Substitute.For<IConfiguration>();
        configuration["MarketData:FuturesEodMinuteBatchWriter"].Returns("true");
        configuration["MarketData:FuturesEodHistorySpoolPath"].Returns(blocked);
        var db = Substitute.For<IDbContextFactory>();
        await using var projector = new FuturesEodDataRealtimeProjector(db, Substitute.For<ISequenceIdGenerator>(), NullLogger<FuturesEodDataRealtimeProjector>.Instance, configuration);
        var context = Substitute.For<IEventActorContext>(); context.ActorId.Returns(new ActorMailboxId(ActorType.Realtime, FuturesEodDataRealtimeActor.ActorName));
        context.SendAsync<FuturesEodSessionStatisticsUpdatedEvent, FuturesEodDataId>(Arg.Any<FuturesEodSessionStatisticsUpdatedEvent>())
            .Returns(_ => ValueTask.FromException(new IOException("Injected source publication failure.")));
        await Assert.ThrowsAsync<IOException>(() => projector.StartAsync(context).AsTask());
        await context.DidNotReceive().SendAsync<FuturesEodSessionStatisticsUpdatedEvent, FuturesEodDataId>(Arg.Any<FuturesEodSessionStatisticsUpdatedEvent>());
        Assert.Empty(db.ReceivedCalls());
    }
}
