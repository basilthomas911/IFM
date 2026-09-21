using NSubstitute;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public sealed class InstrumentDefinitionSelectorViewModelTests
{
    [Fact]
    public async Task Late_search_response_cannot_replace_newer_selection_page()
    {
        var api = Substitute.For<IMarketDataQueryApi>();
        var old = new TaskCompletionSource<ServiceResult<InstrumentDefinitionPage>>();
        var first = new InstrumentDefinitionPageRequest { Root = "ES" };
        var second = first with { Root = "NQ" };
        var expected = new InstrumentDefinitionPage(Guid.NewGuid(), DateTime.UtcNow, [], null);
        api.GetInstrumentDefinitionsAsync(first, Arg.Any<CancellationToken>()).Returns(old.Task);
        api.GetInstrumentDefinitionsAsync(second, Arg.Any<CancellationToken>()).Returns(new ServiceOk<InstrumentDefinitionPage>(expected));
        using var model = new InstrumentDefinitionSelectorViewModel(new(api, Substitute.For<IMarketDataFeedQueryApi>()));
        var pending = model.SearchAsync(first);
        Assert.True(await model.SearchAsync(second));
        old.SetResult(new ServiceOk<InstrumentDefinitionPage>(new(Guid.NewGuid(), DateTime.UtcNow, [], "old")));
        Assert.False(await pending);
        Assert.Same(expected, model.Page);
    }

    [Fact]
    public async Task Provider_failure_and_disposal_do_not_publish_a_success_page()
    {
        var api = Substitute.For<IMarketDataQueryApi>();
        api.GetInstrumentDefinitionsAsync(Arg.Any<InstrumentDefinitionPageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ServiceFailed<InstrumentDefinitionPage>(1063, "Catalog unavailable"));
        var model = new InstrumentDefinitionSelectorViewModel(new(api, Substitute.For<IMarketDataFeedQueryApi>()));
        await Assert.ThrowsAsync<UiServiceOperationException>(() => model.SearchAsync(new() { Root = "ES" }));
        Assert.Null(model.Page);
        model.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => model.SearchAsync(new() { Root = "ES" }));
    }
}
