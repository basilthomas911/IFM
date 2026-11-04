using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Commands;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Command.Validation;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.Validation;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;

public sealed class WorkflowCompletionPayloadBudgetTests
{
    [Theory]
    [InlineData(false, 70 * 1024, true)]
    [InlineData(true, 70 * 1024, true)]
    [InlineData(false, 524289, false)]
    [InlineData(true, 524289, false)]
    public void Composer_and_risk_ingress_honor_their_declared_content_budget(bool risk, int size, bool accepted)
    {
        var envelope = StrategyStageResultEnvelope.Create(Guid.NewGuid(), "budget-fixture", 1,
            new byte[size], DateTime.UtcNow, DateTime.UtcNow, maximumPayloadBytes: size);
        var errors = new List<ValidationError>();
        if (risk)
        {
            var command = new CompleteRiskManagementCommand { Result = envelope,
                Subject = new(ActorType.Command, "BudgetFixture", "Complete", "fixture") };
            errors.ValidateWorkflowCommand(command);
        }
        else
        {
            var command = new CompleteOrderCompositionCommand { Result = envelope,
                Subject = new(ActorType.Command, "BudgetFixture", "Complete", "fixture") };
            errors.ValidateWorkflowCommand(command);
        }
        Assert.Equal(!accepted, errors.Any(error => error.ErrorMessage == StrategyStageResultEnvelopeValidationRules.PayloadLimitErrorMessage));
    }
}
