using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Tests.Support;
using TomasAI.IFM.UI.Net.ViewModels.Strategy;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public sealed class MarketAssessmentPresenterTests
{
    [Theory]
    [InlineData(TimeFrameType.Daily)][InlineData(TimeFrameType.Weekly)][InlineData(TimeFrameType.Monthly)]
    public void Presenter_labels_one_accepted_timeframe_and_distinguishes_current_from_expired(TimeFrameType horizon)
    {
        var f=new MarketAssessmentTestScenario(horizon);
        var current=MarketAssessmentPresenter.Render(f.View,f.Projection,f.Result.EvaluatedAtUtc);
        current.Should().Contain(horizon.ToString()).And.Contain("Accepted and current").And.Contain("Matches accepted result")
            .And.Contain("age at evaluation").And.Contain("Parameter hash").And.NotContain("Tradeability").And.NotContain("HintTradeType");
        MarketAssessmentPresenter.Render(f.View,f.Projection,f.Result.Assessment.ValidUntilUtc!.Value).Should().Contain("Accepted: expired").And.NotContain("Accepted and current");
    }
    [Fact]
    public void Projection_alone_never_claims_workflow_authority()
    {
        var f=new MarketAssessmentTestScenario();
        var orphan=MarketAssessmentPresenter.Render(f.View with {MarketCondition=new()},f.Projection,f.Result.EvaluatedAtUtc);
        orphan.Should().Contain("Projection not accepted by workflow").And.NotContain("Accepted and current");
        MarketAssessmentPresenter.Render(f.View with {AssessmentBinding=null},null,f.Result.EvaluatedAtUtc).Should().StartWith("Legacy Market Condition");
    }
}
