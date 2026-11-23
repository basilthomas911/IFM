namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.Runtime;

// Functional deadline qualification must not compete with unrelated tests that deliberately hang storage workers.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WorkflowDecisionQualificationCollection
{
    public const string Name = "Workflow decision runtime qualification";
}
