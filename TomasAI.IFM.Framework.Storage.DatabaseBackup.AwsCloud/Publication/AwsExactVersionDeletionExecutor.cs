using Amazon.S3;
using Amazon.S3.Model;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

public sealed record AwsRetentionDeletionResult(
    DatabaseRetentionPlanId PlanId,
    long Revision,
    int DeletedObjectVersionCount,
    long DeletedBytes);

public sealed class AwsPartialRetentionExecutionException : Exception
{
    public AwsPartialRetentionExecutionException(
        DatabaseRetentionPlanId planId,
        long revision,
        IReadOnlyList<AwsRetentionPlanObject> completed,
        Exception innerException)
        : base("AWS retention execution stopped after a partial exact-version deletion and requires reconciliation.", innerException)
    {
        PlanId = planId;
        Revision = revision;
        Completed = completed;
    }

    public DatabaseRetentionPlanId PlanId { get; }
    public long Revision { get; }
    public IReadOnlyList<AwsRetentionPlanObject> Completed { get; }
}

public sealed class AwsExactVersionDeletionExecutor(IAmazonS3 s3)
{
    public async ValueTask<AwsRetentionDeletionResult> ExecuteAsync(
        AwsApprovedRetentionExecution execution,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(execution);
        var completed = new List<AwsRetentionPlanObject>();
        long bytes = 0;
        foreach (var item in execution.Objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await s3.DeleteObjectAsync(new DeleteObjectRequest
                {
                    BucketName = item.BucketName,
                    Key = item.ObjectKey,
                    VersionId = item.VersionId
                }, cancellationToken).ConfigureAwait(false);
                completed.Add(item);
                bytes = checked(bytes + item.Length);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                throw new AwsPartialRetentionExecutionException(
                    execution.PlanId, execution.Revision, completed.ToArray(), exception);
            }
        }
        return new AwsRetentionDeletionResult(
            execution.PlanId, execution.Revision, completed.Count, bytes);
    }
}
