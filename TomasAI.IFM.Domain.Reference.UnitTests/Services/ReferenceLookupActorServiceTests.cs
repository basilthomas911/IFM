using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.Services;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Reference.UnitTests.Services;

public sealed class ReferenceLookupActorServiceTests
{
    [Fact]
    public async Task InvalidationDuringARefreshDoesNotPublishTheOldProjection()
    {
        var actor = Substitute.For<IActorService>();
        var oldResult = new TaskCompletionSource<ServiceResult<LookupTypeCollection>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        actor.RequestAsync<LookupTypeCollection, GetLookupTypesQuery>(Arg.Any<GetLookupTypesQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref reads) == 1
                ? new ValueTask<ServiceResult<LookupTypeCollection>>(oldResult.Task)
                : new ValueTask<ServiceResult<LookupTypeCollection>>(Result("CAD")));
        using var service = new ReferenceLookupActorService(actor);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var refresh = service.EnsureLoadedAsync(deadline.Token).AsTask();
        Assert.Equal(1, reads);
        ReferenceLookupCacheGeneration.Invalidate();
        oldResult.SetResult(Result("USD"));
        await refresh;
        Assert.True(service.CurrencyExists("CAD"));
        Assert.False(service.CurrencyExists("USD"));
        Assert.Equal(2, reads);

        static ServiceResult<LookupTypeCollection> Result(string code) => new ServiceOk<LookupTypeCollection>(
            new LookupTypeCollection([new LookupTypeReadModel("Currency", code, 0, "", DateTime.UtcNow, "test")]));
    }

    [Fact]
    public async Task RepeatedChecksUseOneColdLoadAndCaseInsensitiveFrozenIndex()
    {
        var actorService = Substitute.For<IActorService>();
        var values = new LookupTypeCollection(
        [
            new LookupTypeReadModel("Currency", "USD", 0, string.Empty, DateTime.UtcNow, "test")
        ]);
        actorService.RequestAsync<LookupTypeCollection, GetLookupTypesQuery>(
                Arg.Any<GetLookupTypesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<LookupTypeCollection>>(
                new ServiceOk<LookupTypeCollection>(values)));


        using var service = new ReferenceLookupActorService(
            actorService);

        await service.EnsureLoadedAsync();
        service.CurrencyExists("usd").Should().BeTrue();
        service.CurrencyExists("USD").Should().BeTrue();

        actorService.Received(1)
            .RequestAsync<LookupTypeCollection, GetLookupTypesQuery>(
                Arg.Any<GetLookupTypesQuery>(), Arg.Any<CancellationToken>());

    }
}
