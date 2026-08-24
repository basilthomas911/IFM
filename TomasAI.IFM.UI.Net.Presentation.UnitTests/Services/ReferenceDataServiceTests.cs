using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Models;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Services;

public class ReferenceDataServiceTests
{
    [Fact]
    public async Task GetLookupTypesAsync_MapsBackendRecordsToUiOwnedModels()
    {
        var backend = new LookupTypeReadModel(
            "Symbol", "ES", 1, "S&P 500", DateTime.UtcNow, "test");
        var api = Substitute.For<IReferenceQueryApi>();
        api.GetLookupTypesAsync().Returns(
            new ServiceOk<LookupTypeCollection>(new LookupTypeCollection([backend])));
        var service = UiServiceFactory.CreateReference(api);

        var result = await service.GetLookupTypesAsync();

        result.IsSuccess.Should().BeTrue();
        result.RequireValue().Should().Equal(new LookupTypeUiModel(
            backend.LookupTypeName,
            backend.ShortCode,
            backend.OrderId,
            backend.Description,
            backend.CreatedOn,
            backend.CreatedBy));
    }

    [Fact]
    public async Task GetLookupTypesAsync_CanceledBeforeDispatch_DoesNotCallBackend()
    {
        var api = Substitute.For<IReferenceQueryApi>();
        var service = UiServiceFactory.CreateReference(api);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = async () => await service.GetLookupTypesAsync(cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        await api.DidNotReceive().GetLookupTypesAsync();
    }

    [Fact]
    public async Task LoadReferenceDataDefinitionTypesAsync_ReportsServiceFailureAndDoesNotPublishData()
    {
        var api = Substitute.For<IReferenceQueryApi>();
        api.GetReferenceDataDefinitionTypesAsync().Returns(
            Task.FromResult<ServiceResult<LookupTypeCollection>>(
                new ServiceFailed<LookupTypeCollection>(741, "reference unavailable")));
        var service = UiServiceFactory.CreateReference(api);

        var result = await service.GetReferenceDataDefinitionTypesAsync();

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Error.Should().Be(new UiOperationError(741, "reference unavailable"));
    }
}
