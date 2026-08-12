using System;
using System.Text.RegularExpressions;
using FluentAssertions;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.ReferenceDb;

public sealed class ReferenceProjectionCqlTests
{
    [Fact]
    public void ProjectionState_UsesGenerationCheckedCutover()
    {
        var schema = Normalize(ReferenceSchemaCql.CreateReferenceProjectionStateV3Table);
        var read = Normalize(ReferenceDbCql.GetReferenceProjectionStateV3);
        var invalidate = Normalize(ReferenceDbCql.InvalidateReferenceProjectionStateV3);
        var complete = Normalize(ReferenceDbCql.CompleteReferenceProjectionStateV3);

        schema.Should().Contain("generation uuid");
        schema.Should().Contain("completed boolean");
        read.Should().Contain("generation as \"generation\", completed as \"completed\"");
        invalidate.Should().Contain("set generation = :generation, completed = false");
        complete.Should().Contain("if generation = :generation");
        read.Should().NotContain("allow filtering");
    }

    [Fact]
    public void ProjectionMutationBarrier_TracksEachActiveWriterByProjection()
    {
        var schema = Normalize(ReferenceSchemaCql.CreateReferenceProjectionMutationV3Table);
        var insert = Normalize(ReferenceDbCql.InsertReferenceProjectionMutationV3);
        var read = Normalize(ReferenceDbCql.GetReferenceProjectionMutationsV3);
        var delete = Normalize(ReferenceDbCql.DeleteReferenceProjectionMutationV3);
        var mutationId = Guid.NewGuid();
        var startedOn = new DateTime(2045, 2, 1, 12, 0, 0, DateTimeKind.Utc);

        schema.Should().Contain("primary key ((projectionname), mutationid)");
        insert.Should().Contain("values (:projectionname, :mutationid, :startedon)");
        read.Should().Contain("where projectionname = :projectionname");
        delete.Should().Contain("where projectionname = :projectionname and mutationid = :mutationid");
        read.Should().NotContain("allow filtering");

        new InsertReferenceProjectionMutationV3("scheduled_job_by_name_v3", mutationId, startedOn)
            .Bind()
            .Should().BeEquivalentTo(
                new object[] { "scheduled_job_by_name_v3", mutationId, startedOn },
                options => options.WithStrictOrdering());
    }

    [Fact]
    public void ProjectionOwnership_UsesLwtConflictPoisonAndConditionalSafeRelease()
    {
        var schema = Normalize(ReferenceSchemaCql.CreateReferenceProjectionOwnershipV3Table);
        var claim = Normalize(ReferenceDbCql.ClaimReferenceProjectionOwnershipV3);
        var conflict = Normalize(ReferenceDbCql.FlagReferenceProjectionOwnershipConflictV3);
        var release = Normalize(ReferenceDbCql.ReleaseReferenceProjectionOwnershipIfSafeV3);

        schema.Should().Contain("projectionname text primary key");
        schema.Should().Contain("ownermutationid uuid");
        schema.Should().Contain("conflicted boolean");
        claim.Should().Contain("if not exists");
        conflict.Should().Contain("set conflicted = true");
        conflict.Should().Contain("if exists");
        release.Should().Contain("if ownermutationid = :mutationid and conflicted = false");

        var mutationId = Guid.NewGuid();
        new ClaimReferenceProjectionOwnershipV3(
                "scheduled_job_by_name_v3",
                mutationId,
                new DateTime(2045, 2, 1, 12, 0, 0, DateTimeKind.Utc))
            .Bind()
            .Should().BeEquivalentTo(
                new object[]
                {
                    "scheduled_job_by_name_v3",
                    mutationId,
                    new DateTime(2045, 2, 1, 12, 0, 0, DateTimeKind.Utc)
                },
                options => options.WithStrictOrdering());
    }

    [Theory]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    public void MutationJournalCleanup_RequiresNoTargetSubmissionAndConfirmedOwnershipResolution(
        bool targetMutationSubmissionStarted,
        bool ownershipResolved,
        bool activationConfirmed,
        bool expected)
    {
        ProjectionMutationSafety.CanRemoveMutationJournalAfterFailure(
                targetMutationSubmissionStarted,
                ownershipResolved,
                activationConfirmed)
            .Should().Be(expected);
        Normalize(ReferenceDbCql.ReleaseReferenceProjectionOwnershipV3)
            .Should().Contain("if ownermutationid = :mutationid");
    }

    [Fact]
    public void OverlappingSuccessfulWriters_CannotRestoreProjectionReadiness()
    {
        ProjectionMutationSafety.CanPublishReady(
                operationSucceeded: true,
                ownsWriteEpoch: true,
                wasReadyOrExactlyReconciled: true,
                markerIsExclusive: true,
                generationStillMatches: true,
                ownershipReleasedWithoutConflict: false)
            .Should().BeFalse("the owner's LWT release is poisoned by the overlapping contender");
        ProjectionMutationSafety.CanPublishReady(
                operationSucceeded: true,
                ownsWriteEpoch: false,
                wasReadyOrExactlyReconciled: true,
                markerIsExclusive: true,
                generationStillMatches: true,
                ownershipReleasedWithoutConflict: true)
            .Should().BeFalse("a contender never owns the epoch it would need to publish");
    }

    [Fact]
    public void StaleRecoveryInventory_ContainsScopeNameMutationIdAndStartedOn()
    {
        var inventory = Normalize(ReferenceDbCql.GetReferenceProjectionMutationsV3All);

        inventory.Should().Contain("projectionname");
        inventory.Should().Contain("mutationid");
        inventory.Should().Contain("startedon");
        inventory.Should().NotContain("allow filtering");
    }

    [Fact]
    public void ScheduledJobNameProjection_UsesCaseSensitivePrimaryKeyAndLwtReservation()
    {
        var schema = Normalize(ReferenceSchemaCql.CreateScheduledJobByNameV3Table);
        var reserve = Normalize(ReferenceDbCql.InsertScheduledJobByNameV3);
        var rotate = Normalize(ReferenceDbCql.RotateScheduledJobNameV3Reservation);
        var release = Normalize(ReferenceDbCql.ReleaseScheduledJobNameV3);
        var read = Normalize(ReferenceDbCql.GetScheduledJobId);
        var reservationToken = Guid.Parse("73c27988-d257-475f-af4e-428e8ab3dc39");

        schema.Should().Contain("jobname text primary key");
        schema.Should().Contain("reservationtoken uuid");
        reserve.Should().Contain("if not exists");
        rotate.Should().Contain("if jobid = :jobid and reservationtoken = :expectedreservationtoken");
        release.Should().Contain("if jobid = :jobid and reservationtoken = :reservationtoken");
        read.Should().Contain("where jobname = :jobname");
        read.Should().NotContain("allow filtering");

        new ReleaseScheduledJobNameV3("job-name", 42, reservationToken)
            .Bind()
            .Should().BeEquivalentTo(
                new object[] { "job-name", 42, reservationToken },
                options => options.WithStrictOrdering());

        var replacementToken = Guid.Parse("b462dcc8-f423-411e-919b-7a55819616d3");
        new RotateScheduledJobNameV3Reservation(
                replacementToken,
                "job-name",
                42,
                reservationToken)
            .Bind()
            .Should().BeEquivalentTo(
                new object[] { replacementToken, "job-name", 42, reservationToken },
                options => options.WithStrictOrdering());
    }

    [Fact]
    public void ScheduledJobMutationOwnership_UsesIdAndNameScopesWithExactLwtRelease()
    {
        var schema = Normalize(ReferenceSchemaCql.CreateScheduledJobWriteOwnershipV3Table);
        var claim = Normalize(ReferenceDbCql.ClaimScheduledJobWriteOwnershipV3);
        var release = Normalize(ReferenceDbCql.ReleaseScheduledJobWriteOwnershipV3);
        var inventory = Normalize(ReferenceDbCql.GetScheduledJobWriteOwnershipsV3All);
        var operationId = Guid.Parse("34e0f1f2-e89f-4bf3-b80a-699a3e8ed9ab");
        var startedOn = new DateTime(2045, 2, 1, 12, 0, 0, DateTimeKind.Utc);

        schema.Should().Contain("primary key ((scopetype, scopekey))");
        schema.Should().Contain("operationid uuid");
        schema.Should().Contain("startedon timestamp");
        claim.Should().Contain("if not exists");
        release.Should().Contain("if operationid = :operationid");
        inventory.Should().Contain("scopetype, scopekey, operationid, startedon");
        inventory.Should().NotContain("allow filtering");

        new ClaimScheduledJobWriteOwnershipV3("job-name", "nightly", operationId, startedOn)
            .Bind()
            .Should().BeEquivalentTo(
                new object[] { "job-name", "nightly", operationId, startedOn },
                options => options.WithStrictOrdering());
        new ReleaseScheduledJobWriteOwnershipV3("job-id", "42", operationId)
            .Bind()
            .Should().BeEquivalentTo(
                new object[] { "job-id", "42", operationId },
                options => options.WithStrictOrdering());
        new GetScheduledJobWriteOwnershipV3("job-id", "42")
            .Bind()
            .Should().BeEquivalentTo(
                new object[] { "job-id", "42" },
                options => options.WithStrictOrdering());
    }

    [Fact]
    public void ReferenceReconciliation_TokenlessScheduledJobReservationFailsReadiness()
    {
        var result = new ReferenceProjectionReconciliationResult(
            SourceScheduledJobs: 1,
            ProjectedScheduledJobs: 1,
            MissingScheduledJobs: 0,
            UnexpectedScheduledJobs: 0,
            TokenlessScheduledJobReservations: 1);

        result.IsConsistent.Should().BeFalse();
    }

    static string Normalize(string cql)
        => Regex.Replace(cql, @"\s+", " ").Trim().ToLowerInvariant();
}
