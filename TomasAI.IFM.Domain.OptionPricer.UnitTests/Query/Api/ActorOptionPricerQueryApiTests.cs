using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Domain.OptionPricer.Query.Api;
using TomasAI.IFM.Domain.OptionPricer.Shared.Queries;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.OptionPricer.UnitTests.Query.Api;

public class ActorOptionPricerQueryApiTests
{
    [Fact]
    public async Task JobStatusUsesDirectStorageAndReturnsTypedSuccess()
    {
        var (api, db) = CreateApi();
        db.GetSpreadDistributionJobInProgressCountAsync(1, 2).Returns(1);

        var result = await api.IsSpreadDistributionJobInProgressAsync(1, 2);

        api.Should().BeAssignableTo<IActorOptionPricerQueryApi>();
        result.Success.Should().BeTrue();
        result.Value!.Value.Should().BeTrue();
        await db.Received(1).GetSpreadDistributionJobInProgressCountAsync(1, 2);
    }

    [Fact]
    public async Task StorageFailureReturnsTheQueryErrorId()
    {
        var (api, db) = CreateApi();
        var exception = new InvalidOperationException("option pricer unavailable");
        db.GetSpreadDistributionJobInProgressCountAsync(Arg.Any<int>(), Arg.Any<int>())
            .Returns(_ => Task.FromException<int>(exception));

        var result = await api.IsSpreadDistributionJobInProgressAsync(1, 2);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(GetSpreadDistributionJobInProgressQuery.ErrorId);
        result.ErrorMessage.Should().Be(exception.Message);
    }

    static (ActorOptionPricerQueryApi Api, IOptionPricerDbContext Db) CreateApi()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IOptionPricerDbContext>();
        dbFactory.OptionPricerDb.Returns(db);
        return (new ActorOptionPricerQueryApi(dbFactory), db);
    }
}
