using FluentAssertions;
using TomasAI.IFM.Domain.SystemAdmin.Query.Api;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ServiceApi;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.Query.Api;

public class ActorSystemAdminQueryApiTests
{
    [Fact]
    public async Task DatabaseNamesReturnATypedDirectResult()
    {
        var api = new ActorSystemAdminQueryApi();

        var result = await api.GetDatabaseNamesAsync();

        api.Should().BeAssignableTo<IActorSystemAdminQueryApi>();
        result.Success.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task CancellationIsNotConvertedToFailure()
    {
        var api = new ActorSystemAdminQueryApi();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Func<Task> act = async () => await api.GetDatabaseNamesAsync(cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
