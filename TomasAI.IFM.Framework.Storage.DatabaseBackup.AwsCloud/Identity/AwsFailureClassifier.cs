using System.Net;
using Amazon.Runtime;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Identity;

public enum AwsFailureKind { Cancelled, Transient, Throttled, AccessDenied, ExpiredCredentials, Configuration, Permanent }

public sealed record AwsFailureObservation(AwsFailureKind Kind, bool Retryable, string Code, string? RequestId);

public static class AwsFailureClassifier
{
    public static AwsFailureObservation Classify(Exception exception, bool cancellationRequested = false)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is OperationCanceledException)
            return new(AwsFailureKind.Cancelled, !cancellationRequested, "cancelled", null);
        if (exception is AwsIdentityRejectedException or InvalidOperationException)
            return new(AwsFailureKind.Configuration, false, "configuration", null);
        if (exception is AmazonServiceException aws)
        {
            var code = string.IsNullOrWhiteSpace(aws.ErrorCode) ? "aws-service-error" : aws.ErrorCode;
            if (code.Contains("ExpiredToken", StringComparison.OrdinalIgnoreCase))
                return new(AwsFailureKind.ExpiredCredentials, true, code, aws.RequestId);
            if (code.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase) || aws.StatusCode == HttpStatusCode.Forbidden)
                return new(AwsFailureKind.AccessDenied, false, code, aws.RequestId);
            if (code.Contains("Throttl", StringComparison.OrdinalIgnoreCase) || aws.StatusCode == HttpStatusCode.TooManyRequests)
                return new(AwsFailureKind.Throttled, true, code, aws.RequestId);
            if ((int)aws.StatusCode >= 500 || aws.InnerException is TimeoutException)
                return new(AwsFailureKind.Transient, true, code, aws.RequestId);
            return new(AwsFailureKind.Permanent, false, code, aws.RequestId);
        }
        if (exception is AmazonClientException)
            return new(AwsFailureKind.Configuration, false, "credential-chain", null);
        if (exception is TimeoutException or HttpRequestException)
            return new(AwsFailureKind.Transient, true, "transport", null);
        return new(AwsFailureKind.Permanent, false, exception.GetType().Name, null);
    }
}
