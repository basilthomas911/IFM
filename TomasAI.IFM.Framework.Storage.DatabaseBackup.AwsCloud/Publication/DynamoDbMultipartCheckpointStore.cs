using System.Security.Cryptography;
using System.Text;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Publication;

public sealed class DynamoDbMultipartCheckpointStore(
    IAmazonDynamoDB dynamoDb,
    AwsCloudDatabaseBackupOptions options) : IAwsMultipartCheckpointStore
{
    readonly string _partition = $"ENV#{options.Environment.ToString().ToLowerInvariant()}#MULTIPART";

    public async ValueTask<AwsMultipartCheckpoint?> ReadAsync(AwsGeneratedObjectKey key, CancellationToken cancellationToken)
    {
        var response = await dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = options.JournalTableName, Key = Key(key), ConsistentRead = true
        }, cancellationToken).ConfigureAwait(false);
        var item = response.Item;
        if (item is not { Count: > 0 }) return null;
        if (!StringComparer.Ordinal.Equals(item["object_key"].S, key.Value))
            throw new InvalidDataException("The multipart checkpoint key digest collided with different content.");
        return new AwsMultipartCheckpoint(
            item["bucket_name"].S, item["object_key"].S, item["upload_id"].S,
            int.Parse(item["completed_parts"].N, System.Globalization.CultureInfo.InvariantCulture),
            long.Parse(item["uploaded_bytes"].N, System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse(item["updated_utc"].S, null, System.Globalization.DateTimeStyles.RoundtripKind));
    }

    public async ValueTask WriteAsync(AwsMultipartCheckpoint checkpoint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        _ = new AwsGeneratedObjectKey(checkpoint.ObjectKey);
        await dynamoDb.PutItemAsync(new PutItemRequest
        {
            TableName = options.JournalTableName,
            Item = new()
            {
                ["PK"] = Text(_partition), ["SK"] = Text(Sort(checkpoint.ObjectKey)),
                ["schema_version"] = Number(1), ["record_type"] = Text("replica"),
                ["bucket_name"] = Text(checkpoint.BucketName), ["object_key"] = Text(checkpoint.ObjectKey),
                ["upload_id"] = Text(checkpoint.UploadId), ["completed_parts"] = Number(checkpoint.CompletedPartCount),
                ["uploaded_bytes"] = Number(checkpoint.UploadedBytes),
                ["updated_utc"] = Text(checkpoint.UpdatedUtc.UtcDateTime.ToString("O"))
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask RemoveAsync(AwsGeneratedObjectKey key, CancellationToken cancellationToken)
        => new(dynamoDb.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = options.JournalTableName, Key = Key(key)
        }, cancellationToken));

    Dictionary<string, AttributeValue> Key(AwsGeneratedObjectKey key)
        => new() { ["PK"] = Text(_partition), ["SK"] = Text(Sort(key.Value)) };
    static string Sort(string value) => "REPLICA#UPLOAD#" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    static AttributeValue Text(string value) => new() { S = value };
    static AttributeValue Number(long value) => new() { N = value.ToString(System.Globalization.CultureInfo.InvariantCulture) };
}
