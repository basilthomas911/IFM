using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Services;

/// <summary>Verifies the UI-owned Economic Calendar service boundary.</summary>
public sealed class EconomicCalendarServiceTests
{
    /// <summary>Verifies country-code query values are mapped into UI-owned records.</summary>
    [Fact]
    public async Task GetCountryCodesAsync_MapsSuccessfulBackendResult()
    {
        var api = Substitute.For<IMarketDataQueryApi>();
        api.GetEconomicCalendarCountryCodesAsync().Returns(
            new ServiceOk<EconomicCalendarCountryCodeReadModel[]>([new("CA"), new("US")]));
        var service = UiServiceFactory.CreateEconomicCalendar(queryApi: api);

        var result = await service.GetCountryCodesAsync();

        result.RequireValue().Should().Equal(
            new EconomicCalendarCountryCodeUiModel("CA"),
            new EconomicCalendarCountryCodeUiModel("US"));
    }

    /// <summary>Verifies cancellation is honored before a typed backend query is dispatched.</summary>
    [Fact]
    public async Task GetCountryCodesAsync_CanceledBeforeDispatch_DoesNotCallBackend()
    {
        var api = Substitute.For<IMarketDataQueryApi>();
        var service = UiServiceFactory.CreateEconomicCalendar(queryApi: api);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = async () => await service.GetCountryCodesAsync(cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
        await api.DidNotReceive().GetEconomicCalendarCountryCodesAsync();
    }
}
