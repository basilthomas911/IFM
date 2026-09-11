using FluentAssertions;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Pipeline;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;

public sealed class PipelineExceptionDiagnosticsTests
{
    [Fact]
    public void Persisted_diagnostics_include_actionable_exception_chain_and_pipeline_context()
    {
        Exception captured;
        try
        {
            ThrowFailure();
            throw new InvalidOperationException("Unreachable.");
        }
        catch (Exception exception)
        {
            captured = new ApplicationException("Outer failure; with delimiter.", exception);
        }

        var diagnostics = PipelineExceptionDiagnostics.Create(captured,
            new Dictionary<string, string>
            {
                ["WorkflowId"] = "workflow-123",
                ["TargetHorizon"] = "Daily"
            });

        diagnostics["WorkflowId"].Should().Be("workflow-123");
        diagnostics["TargetHorizon"].Should().Be("Daily");
        diagnostics["Exception.Type"].Should().Be(typeof(ApplicationException).FullName);
        diagnostics["Exception.Message"].Should().Be("Outer failure, with delimiter.");
        diagnostics["Exception.HResult"].Should().StartWith("0x");
        diagnostics["InnerException.1.Type"].Should().Be(typeof(ArgumentException).FullName);
        diagnostics["InnerException.1.Message"].Should().Contain("Unsupported signal configuration");
        diagnostics["InnerException.1.Target"].Should().Contain(nameof(ThrowFailure));
        diagnostics["InnerException.1.StackTrace"].Should().Contain(nameof(ThrowFailure));
        diagnostics.Values.Should().OnlyContain(value => !value.Contains(';'));

        PipelineExceptionDiagnostics.Summary("Regime Discovery initialization failed", captured)
            .Should().Contain("Outer failure, with delimiter.");
    }

    [Fact]
    public void Failed_initialization_retains_the_resolved_parameter_reference()
    {
        var id = Guid.NewGuid();
        var result = PipelineStartResult<object>
            .Failed("RD.INIT.EXCEPTION", "InitializationFailed", "Failed.")
            .WithParameterSet(id, 7, new string('A', 64));

        result.Error!.ParameterSetId.Should().Be(id);
        result.Error.ParameterSetVersion.Should().Be(7);
        result.Error.ParameterPayloadSha256.Should().Be(new string('A', 64));
    }

    [Fact]
    public void Persisted_diagnostics_bound_large_values()
    {
        var diagnostics = PipelineExceptionDiagnostics.Create(
            new ArgumentException(new string('x', 10_000)));

        diagnostics["Exception.Message"].Length.Should().BeLessThanOrEqualTo(4096);
        diagnostics["Exception.Message"].Should().EndWith("...[truncated]");
    }

    static void ThrowFailure()
        => throw new ArgumentException("Unsupported signal configuration AtrBaselineRatio/FifteenSeconds.");
}