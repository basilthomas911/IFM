using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Domain.Reference.Query.Api;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.Reference.UnitTests.Query.Api;

public class ActorReferenceQueryApiTests
{
    [Fact]
    public async Task NextSeedIdUsesDirectStorageAndReturnsTypedSuccess()
    {
        var (api, db) = CreateApi();
        db.GetNextSeedIdAsync("Trade").Returns(42);

        var result = await api.GetNextSeedIdAsync("Trade");

        api.Should().BeAssignableTo<IActorReferenceQueryApi>();
        result.Success.Should().BeTrue();
        result.Value!.Value.Should().Be(42);
        await db.Received(1).GetNextSeedIdAsync("Trade");
    }

    [Fact]
    public async Task StorageFailureReturnsTheQueryErrorId()
    {
        var (api, db) = CreateApi();
        var exception = new InvalidOperationException("reference unavailable");
        db.GetNextSeedIdAsync(Arg.Any<string>())
            .Returns(_ => Task.FromException<int>(exception));

        var result = await api.GetNextSeedIdAsync("Trade");

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(GetNextSeedIdQuery.ErrorId);
        result.ErrorMessage.Should().Be(exception.Message);
    }

    static (ActorReferenceQueryApi Api, IReferenceDbContext Db) CreateApi()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IReferenceDbContext>();
        dbFactory.ReferenceDb.Returns(db);
        return (new ActorReferenceQueryApi(dbFactory), db);
    }
}
