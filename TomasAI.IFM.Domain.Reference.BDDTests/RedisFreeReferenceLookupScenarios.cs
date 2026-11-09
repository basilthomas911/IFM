using NSubstitute;
using TomasAI.IFM.Domain.Reference.Services;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using Xunit;

namespace TomasAI.IFM.Domain.Reference.BDDTests;

public sealed class RedisFreeReferenceLookupScenarios
{
    [Fact]
    public async Task GivenACommittedLookupChangeWhenTheNextRequestArrivesThenItUsesTheNewProjectedValue()
    {
        var actor = Substitute.For<IActorService>();
        var shortCode = "USD";
        actor.RequestAsync<LookupTypeCollection, GetLookupTypesQuery>(Arg.Any<GetLookupTypesQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<ServiceResult<LookupTypeCollection>>(new ServiceOk<LookupTypeCollection>(
                new LookupTypeCollection([new LookupTypeReadModel("Currency", shortCode, 0, "", DateTime.UtcNow, "test")]))));
        using var service = new ReferenceLookupActorService(actor);
        await service.EnsureLoadedAsync();
        Assert.True(service.CurrencyExists("usd"));
        shortCode = "CAD";
        ReferenceLookupCacheGeneration.Invalidate();
        await service.EnsureLoadedAsync();
        Assert.True(service.CurrencyExists("cad"));
        Assert.False(service.CurrencyExists("USD"));
        await actor.Received(2).RequestAsync<LookupTypeCollection, GetLookupTypesQuery>(Arg.Any<GetLookupTypesQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GivenAFailedProjectionQueryWhenItRecoversThenTheFailureIsNotCached()
    {
        var actor = Substitute.For<IActorService>();
        actor.RequestAsync<LookupTypeCollection, GetLookupTypesQuery>(Arg.Any<GetLookupTypesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<LookupTypeCollection>>(new ServiceResult<LookupTypeCollection>(500, "Unavailable")));
        using var service = new ReferenceLookupActorService(actor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.EnsureLoadedAsync().AsTask());
        actor.RequestAsync<LookupTypeCollection, GetLookupTypesQuery>(Arg.Any<GetLookupTypesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<LookupTypeCollection>>(new ServiceOk<LookupTypeCollection>(new LookupTypeCollection([]))));
        await service.EnsureLoadedAsync();
        Assert.False(service.CurrencyExists("USD"));
    }
}
