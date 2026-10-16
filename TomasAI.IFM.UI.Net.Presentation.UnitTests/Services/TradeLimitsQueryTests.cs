using NSubstitute;
using TomasAI.IFM.Domain.Trade.Shared.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Services.Trade;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Services;
public sealed class TradeLimitsQueryTests
{
    [Fact] public async Task Storage_failure_is_reported_instead_of_missing_limits()
    {
        var api = Substitute.For<ITradeQueryApi>();
        api.GetTradeLimitAsync(1501).Returns(new ServiceResult<TradeLimitReadModel>
            { Success = false, ErrorCode = 1022, ErrorMessage = "Expected NpgsqlParameter[]" });
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new TradeQueryService(api).GetPersistedTradeLimitsAsync(1501));
        Assert.Contains("1022",error.Message);
        Assert.Contains("Expected NpgsqlParameter[]",error.Message);
    }
    [Fact] public async Task Persisted_limits_are_returned_to_the_loading_view()
    {
        var api = Substitute.For<ITradeQueryApi>();
        var saved = new TradeLimitReadModel { TradeId = 1501, RiskMargin = 38.55m };
        api.GetTradeLimitAsync(1501).Returns(new ServiceResult<TradeLimitReadModel>(saved));
        Assert.Same(saved,await new TradeQueryService(api).GetPersistedTradeLimitsAsync(1501));
    }
}
