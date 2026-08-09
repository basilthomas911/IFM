using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
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
        var redis = Substitute.For<IRedisCache>();
        redis.Get("ReferenceLookup").Returns((string?)null);
        var serializer = Substitute.For<IJsonSerializer>();
        serializer.Serialize(Arg.Any<object>()).Returns("{}");
        var service = new ReferenceLookupActorService(
            actorService,
            new BlackboardService(redis, serializer));

        await service.EnsureLoadedAsync();
        service.CurrencyExists("usd").Should().BeTrue();
        service.CurrencyExists("USD").Should().BeTrue();

        actorService.Received(1)
            .RequestAsync<LookupTypeCollection, GetLookupTypesQuery>(
                Arg.Any<GetLookupTypesQuery>(), Arg.Any<CancellationToken>());
        redis.Received(1).Get("ReferenceLookup");
    }
}
