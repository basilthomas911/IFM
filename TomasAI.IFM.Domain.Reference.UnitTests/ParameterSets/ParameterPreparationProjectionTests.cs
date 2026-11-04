using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query;
using TomasAI.IFM.Domain.Reference.ParameterSets.Preparation.Query.Actor;
using TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

namespace TomasAI.IFM.Domain.Reference.UnitTests.ParameterSets;

public sealed class ParameterPreparationProjectionTests
{
    [Theory]
    [InlineData(12L)]
    [InlineData(13L)]
    [InlineData(null)]
    public async Task Preparation_returns_evidence_only_if_the_projection_revision_stays_stable(long? endingFence)
    {
        var context = Substitute.For<IParameterPreparationQueryContext>();
        context.AccessPolicy.Returns(Substitute.For<IParameterAccessPolicy>());
        context.ConfigurationDb.GetProjectedParameterRevisionAsync(CancellationToken.None).Returns(12L, endingFence);
        var runId = Guid.NewGuid();
        context.ConfigurationDb.HasParameterStartupProjectionAsync(runId, CancellationToken.None).Returns(true);
        var query = new PrepareParameterCommandQuery
        {
            Preparation = new(Startup: new ApplySignalStartupPlanCommand { RunId = runId, CommandId = Guid.NewGuid() })
        };
        var logger = NullLogger<ParameterPreparationQueryActor>.Instance;

        if (endingFence == 12)
        {
            var result = await query.ExecuteAsync(context, logger, CancellationToken.None);
            Assert.Equal(12L, result.Startup!.Evidence!.RevisionFence);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                query.ExecuteAsync(context, logger, CancellationToken.None).AsTask());
            Assert.Equal(endingFence is null ? "PARAM.PROJECTION_LAG" : "PARAM.EVIDENCE_STALE", error.Message);
        }

        await context.ConfigurationDb.DidNotReceive().AcquireParameterWriteLeaseAsync(CancellationToken.None);
    }
}
