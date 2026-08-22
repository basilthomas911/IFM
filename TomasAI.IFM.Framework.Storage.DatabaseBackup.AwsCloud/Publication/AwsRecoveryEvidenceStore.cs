using Amazon.S3;
using Amazon.S3.Model;
using TomasAI.IFM.Application.DatabaseBackup.Contracts;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

public sealed class AwsRecoveryEvidenceStore(
    IAmazonS3 s3,
    S3ImmutableObjectStore objects,
    IAwsDocumentSignatureService signatures,
    AwsCloudDatabaseBackupOptions options,
    TimeProvider timeProvider) : IDatabaseRecoveryEvidenceStore
{
    readonly AwsBackupObjectKeyFactory _keys = new(options.Environment.ToString().ToLowerInvariant());

    public ValueTask<string> WriteDrillEvidenceAsync(DatabaseRestoreDrillEvidence evidence, CancellationToken cancellationToken)
        => WriteAsync(evidence.OperationId, "restore-drill-v1", evidence, cancellationToken);

    public ValueTask<string> WriteBreakGlassRecordAsync(DatabaseBreakGlassRecoveryRecord record, CancellationToken cancellationToken)
        => WriteAsync(record.RecoveryOperationId, "break-glass-v1", record, cancellationToken);

    public async ValueTask<DatabaseBreakGlassRecoveryRecord> ReconcileBreakGlassRecordAsync(
        DatabaseRecoveryOperationId operationId, CancellationToken cancellationToken)
    {
        var key = _keys.Evidence(operationId, "break-glass-v1");
        var version = await OnlyVersionAsync(key, cancellationToken).ConfigureAwait(false);
        using var response = await s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = options.PrimaryBucketName, Key = key.Value, VersionId = version
        }, cancellationToken).ConfigureAwait(false);
        using var stream = new MemoryStream();
        await response.ResponseStream.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        var content = stream.ToArray();
        var signatureKey = _keys.EvidenceSignature(operationId, "break-glass-v1");
        var signatureVersion = await OnlyVersionAsync(signatureKey, cancellationToken).ConfigureAwait(false);
        using var signatureResponse = await s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = options.PrimaryBucketName, Key = signatureKey.Value, VersionId = signatureVersion
        }, cancellationToken).ConfigureAwait(false);
        using var signatureStream = new MemoryStream();
        await signatureResponse.ResponseStream.CopyToAsync(signatureStream, cancellationToken).ConfigureAwait(false);
        await signatures.VerifyAsync(content,
            DatabaseBackupCanonicalJson.Deserialize<AwsSignatureEnvelope>(signatureStream.ToArray()), cancellationToken).ConfigureAwait(false);
        return DatabaseBackupCanonicalJson.Deserialize<DatabaseBreakGlassRecoveryRecord>(content);
    }

    async ValueTask<string> WriteAsync<T>(
        DatabaseRecoveryOperationId operationId, string documentName, T document, CancellationToken cancellationToken)
    {
        var content = DatabaseBackupCanonicalJson.Serialize(document);
        if (content.Length > options.MaximumSignedDocumentBytes)
            throw new InvalidDataException("AWS recovery evidence exceeds its configured document bound.");
        var signature = await signatures.SignAsync(content, cancellationToken).ConfigureAwait(false);
        var retention = timeProvider.GetUtcNow().AddDays(options.DefaultRetentionDays);
        var context = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["application"] = "IFM", ["component"] = "DatabaseBackup",
            ["operationId"] = operationId.Format(), ["document"] = documentName
        };
        var key = _keys.Evidence(operationId, documentName);
        var version = await objects.UploadAsync(key,
            _ => ValueTask.FromResult<Stream>(new MemoryStream(content, writable: false)),
            content.LongLength, retention, context, cancellationToken).ConfigureAwait(false);
        var signatureBytes = DatabaseBackupCanonicalJson.Serialize(signature);
        _ = await objects.UploadAsync(_keys.EvidenceSignature(operationId, documentName),
            _ => ValueTask.FromResult<Stream>(new MemoryStream(signatureBytes, writable: false)),
            signatureBytes.LongLength, retention, context, cancellationToken).ConfigureAwait(false);
        return $"s3://{version.BucketName}/{version.ObjectKey}?versionId={Uri.EscapeDataString(version.VersionId)}";
    }

    async Task<string> OnlyVersionAsync(AwsGeneratedObjectKey key, CancellationToken cancellationToken)
    {
        var response = await s3.ListVersionsAsync(new ListVersionsRequest
        {
            BucketName = options.PrimaryBucketName, Prefix = key.Value, MaxKeys = 2
        }, cancellationToken).ConfigureAwait(false);
        var versions = (response.Versions ?? []).Where(value => value.IsDeleteMarker != true
            && StringComparer.Ordinal.Equals(value.Key, key.Value)).ToArray();
        if (versions.Length != 1 || string.IsNullOrWhiteSpace(versions[0].VersionId))
            throw new InvalidDataException("The immutable AWS recovery evidence is missing or version-ambiguous.");
        return versions[0].VersionId;
    }
}
