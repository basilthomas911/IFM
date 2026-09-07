using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Fund.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Services.Fund;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Portfolio;

public sealed class FundMetricsViewModelTests
{
    static FundPnlReportReadModel Report(decimal pnl) => new(0.5, 10, 0.5, -5, 2, 1, 1, pnl, .1, 2) { HasHistory = true, MaximumDrawdownAmount = 20, MaximumDrawdownPercent = .05 };

    [Fact]
    public async Task Late_previous_fund_report_cannot_replace_current_selection_and_clear_removes_values()
    {
        var api = Substitute.For<IFundQueryApi>();
        var from = new DateOnly(2026, 1, 1); var through = new DateOnly(2026, 9, 8);
        var old = new TaskCompletionSource<ServiceResult<FundPnlReportReadModel>>(TaskCreationOptions.RunContinuationsAsynchronously);
        api.GetFundPnlReportAsync(1, from, through).Returns(old.Task);
        api.GetFundPnlReportAsync(2, from, through).Returns(new ServiceOk<FundPnlReportReadModel>(Report(20)));
        using var model = new FundMetricsViewModel(new FundQueryService(api));
        var first = model.LoadAsync(1, from, through);
        model.Report.Should().BeNull();
        await model.LoadAsync(2, from, through);
        old.SetResult(new ServiceOk<FundPnlReportReadModel>(Report(100)));
        await first;
        model.Report!.PnlAmount.Should().Be(20);
        model.Clear(); model.Report.Should().BeNull();
        await api.Received(1).GetFundPnlReportAsync(2, from, through);
    }

    [Fact]
    public async Task Invalid_dates_failure_and_no_history_do_not_leave_previous_metrics_visible()
    {
        var api = Substitute.For<IFundQueryApi>(); var from = new DateOnly(2026, 1, 1);
        api.GetFundPnlReportAsync(1, from, from).Returns(new ServiceOk<FundPnlReportReadModel>(Report(10)));
        using var model = new FundMetricsViewModel(new FundQueryService(api));
        await model.LoadAsync(1, from, from); model.Report.Should().NotBeNull();
        await model.LoadAsync(1, from.AddDays(1), from); model.Report.Should().BeNull();
        model.Message.Should().Contain("valid");
        api.GetFundPnlReportAsync(1, from, from).Returns(new ServiceFailed<FundPnlReportReadModel>(1, "offline"));
        await model.LoadAsync(1, from, from); model.Report.Should().BeNull(); model.Message.Should().Contain("unavailable");
        api.GetFundPnlReportAsync(1, from, from).Returns(new ServiceOk<FundPnlReportReadModel>(Report(0) with { HasHistory = false }));
        await model.LoadAsync(1, from, from); model.Message.Should().Contain("no recorded history");
    }
}
