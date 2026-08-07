using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Domain.Reference.Query.Api;
using TomasAI.IFM.Domain.Reference.Shared.Queries;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

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

    [Fact]
    public async Task DefaultDefinitions_StartAllIndependentReadsBeforeAwaitingCompletion()
    {
        var (api, db) = CreateApi();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;
        db.GetLookupTypeAsync(Arg.Any<string>()).Returns(call => CompleteAsync(call.Arg<string>()));

        var result = await api.GetDefaultFuturesContractDefinitionsAsync().WaitAsync(TimeSpan.FromSeconds(2));

        result.Success.Should().BeTrue();
        started.Should().Be(6);

        async Task<ICollection<LookupTypeReadModel>> CompleteAsync(string name)
        {
            if (Interlocked.Increment(ref started) == 6)
                release.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(1));
            return [new LookupTypeReadModel(name, name, 0, string.Empty, DateTime.UtcNow, "test")];
        }
    }

    [Fact]
    public async Task LookupExistence_UsesCentralizedStoragePath()
    {
        var (api, db) = CreateApi();
        db.LookupTypeShortCodeExistsAsync("Currency", "USD").Returns(true);

        var result = await api.LookupTypeShortCodeExistsAsync("Currency", "USD");

        result.Success.Should().BeTrue();
        result.Value!.Value.Should().BeTrue();
        await db.Received(1).LookupTypeShortCodeExistsAsync("Currency", "USD");
        await db.DidNotReceive().GetLookupTypeShortCodesAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task CancellationUsesTokenAwareStorageAndIsNotConvertedToFailure()
    {
        var (api, db) = CreateApi();
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        db.GetCurrentSeedIdAsync("Trade", cancellation.Token)
            .Returns(async _ =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token);
                return 0;
            });

        var operation = api.GetCurrentSeedIdAsync("Trade", cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        Func<Task> act = async () => await operation;

        await act.Should().ThrowAsync<OperationCanceledException>();
        await db.Received(1).GetCurrentSeedIdAsync("Trade", cancellation.Token);
    }

    static (ActorReferenceQueryApi Api, IReferenceDbContext Db) CreateApi()
    {
        var dbFactory = Substitute.For<IDbContextFactory>();
        var db = Substitute.For<IReferenceDbContext>();
        dbFactory.ReferenceDb.Returns(db);
        return (new ActorReferenceQueryApi(dbFactory), db);
    }
}
