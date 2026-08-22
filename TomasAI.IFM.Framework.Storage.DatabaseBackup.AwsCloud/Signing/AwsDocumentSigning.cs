using System.Security.Cryptography;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using TomasAI.IFM.Application.DatabaseBackup.Policies;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Configuration;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;

public sealed record AwsSignatureEnvelope
{
    public int SchemaVersion { get; init; } = 1;
    public required string KeyArn { get; init; }
    public required string Algorithm { get; init; }
    public required string DigestAlgorithm { get; init; }
    public required string DigestBase64 { get; init; }
    public required string SignatureBase64 { get; init; }
    public required DateTimeOffset SignedUtc { get; init; }
}

public sealed record AwsRecoveryTrustedKey(
    string KeyArn,
    string KeySpec,
    string KeyUsage,
    string SigningAlgorithm,
    string SubjectPublicKeyInfoBase64,
    string PublicKeySha256,
    DateTimeOffset TrustedFromUtc,
    DateTimeOffset? TrustedUntilUtc);

public sealed record AwsRecoveryTrustBundle
{
    public int SchemaVersion { get; init; } = 1;
    public required string Environment { get; init; }
    public required long Revision { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required AwsRecoveryTrustedKey[] Keys { get; init; }
}

public interface IAwsDocumentSignatureService
{
    ValueTask<AwsSignatureEnvelope> SignAsync(ReadOnlyMemory<byte> canonicalDocument, CancellationToken cancellationToken);
    ValueTask VerifyAsync(ReadOnlyMemory<byte> canonicalDocument, AwsSignatureEnvelope envelope, CancellationToken cancellationToken);
    ValueTask<AwsRecoveryTrustedKey> ExportTrustedPublicKeyAsync(DateTimeOffset trustedFromUtc, DateTimeOffset? trustedUntilUtc, CancellationToken cancellationToken);
}

public sealed class KmsDocumentSignatureService(
    IAmazonKeyManagementService kms,
    AwsCloudDatabaseBackupOptions options,
    TimeProvider timeProvider) : IAwsDocumentSignatureService
{
    const string AlgorithmName = "ECDSA_SHA_256";

    public async ValueTask<AwsSignatureEnvelope> SignAsync(
        ReadOnlyMemory<byte> canonicalDocument,
        CancellationToken cancellationToken)
    {
        var digest = SHA256.HashData(canonicalDocument.Span);
        var response = await kms.SignAsync(new SignRequest
        {
            KeyId = options.SigningKeyArn,
            Message = new MemoryStream(digest, writable: false),
            MessageType = MessageType.DIGEST,
            SigningAlgorithm = SigningAlgorithmSpec.ECDSA_SHA_256
        }, cancellationToken).ConfigureAwait(false);
        var signature = response.Signature?.ToArray()
            ?? throw new InvalidOperationException("AWS KMS returned no document signature.");
        var envelope = new AwsSignatureEnvelope
        {
            KeyArn = options.SigningKeyArn,
            Algorithm = AlgorithmName,
            DigestAlgorithm = "SHA-256",
            DigestBase64 = Convert.ToBase64String(digest),
            SignatureBase64 = Convert.ToBase64String(signature),
            SignedUtc = timeProvider.GetUtcNow()
        };
        await VerifyAsync(canonicalDocument, envelope, cancellationToken).ConfigureAwait(false);
        return envelope;
    }

    public async ValueTask VerifyAsync(
        ReadOnlyMemory<byte> canonicalDocument,
        AwsSignatureEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ValidateEnvelope(canonicalDocument.Span, envelope, options.SigningKeyArn);
        var response = await kms.VerifyAsync(new VerifyRequest
        {
            KeyId = envelope.KeyArn,
            Message = new MemoryStream(Convert.FromBase64String(envelope.DigestBase64), writable: false),
            MessageType = MessageType.DIGEST,
            Signature = new MemoryStream(Convert.FromBase64String(envelope.SignatureBase64), writable: false),
            SigningAlgorithm = SigningAlgorithmSpec.ECDSA_SHA_256
        }, cancellationToken).ConfigureAwait(false);
        if (response.SignatureValid != true)
            throw new CryptographicException("AWS KMS rejected the immutable document signature.");
    }

    public async ValueTask<AwsRecoveryTrustedKey> ExportTrustedPublicKeyAsync(
        DateTimeOffset trustedFromUtc,
        DateTimeOffset? trustedUntilUtc,
        CancellationToken cancellationToken)
    {
        if (trustedUntilUtc is not null && trustedUntilUtc <= trustedFromUtc)
            throw new ArgumentOutOfRangeException(nameof(trustedUntilUtc));
        var response = await kms.GetPublicKeyAsync(new GetPublicKeyRequest { KeyId = options.SigningKeyArn }, cancellationToken)
            .ConfigureAwait(false);
        var publicKey = response.PublicKey?.ToArray()
            ?? throw new InvalidOperationException("AWS KMS returned no signing public key.");
        if (response.KeyUsage != KeyUsageType.SIGN_VERIFY || response.KeySpec != KeySpec.ECC_NIST_P256
            || response.SigningAlgorithms?.Contains(SigningAlgorithmSpec.ECDSA_SHA_256) != true)
            throw new InvalidOperationException("The AWS signing key metadata is not the approved P-256 SIGN_VERIFY profile.");
        return new AwsRecoveryTrustedKey(
            options.SigningKeyArn, "ECC_NIST_P256", "SIGN_VERIFY", AlgorithmName,
            Convert.ToBase64String(publicKey), Convert.ToHexString(SHA256.HashData(publicKey)),
            trustedFromUtc.ToUniversalTime(), trustedUntilUtc?.ToUniversalTime());
    }

    internal static void ValidateEnvelope(ReadOnlySpan<byte> canonicalDocument, AwsSignatureEnvelope envelope, string trustedKeyArn)
    {
        if (!StringComparer.Ordinal.Equals(envelope.KeyArn, trustedKeyArn)
            || !StringComparer.Ordinal.Equals(envelope.Algorithm, AlgorithmName)
            || !StringComparer.Ordinal.Equals(envelope.DigestAlgorithm, "SHA-256"))
            throw new CryptographicException("The immutable document signature uses an untrusted key or algorithm.");
        var expected = SHA256.HashData(canonicalDocument);
        var claimed = Convert.FromBase64String(envelope.DigestBase64);
        if (!CryptographicOperations.FixedTimeEquals(expected, claimed))
            throw new CryptographicException("The immutable document digest does not match its signature envelope.");
    }
}

public static class AwsOfflineSignatureVerifier
{
    public static void Verify(
        ReadOnlySpan<byte> canonicalDocument,
        AwsSignatureEnvelope envelope,
        AwsRecoveryTrustBundle trustBundle,
        DateTimeOffset verificationTimeUtc)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(trustBundle);
        var key = trustBundle.Keys.SingleOrDefault(value =>
            StringComparer.Ordinal.Equals(value.KeyArn, envelope.KeyArn)
            && value.TrustedFromUtc <= envelope.SignedUtc
            && (value.TrustedUntilUtc is null || envelope.SignedUtc <= value.TrustedUntilUtc))
            ?? throw new CryptographicException("The signature key is not trusted by the recovery bundle for the signing time.");
        if (verificationTimeUtc < envelope.SignedUtc)
            throw new CryptographicException("The signature verification time precedes the signing time.");
        KmsDocumentSignatureService.ValidateEnvelope(canonicalDocument, envelope, key.KeyArn);
        var publicKey = Convert.FromBase64String(key.SubjectPublicKeyInfoBase64);
        if (!CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(publicKey), Convert.FromHexString(key.PublicKeySha256)))
            throw new CryptographicException("The recovery trust-bundle public key fingerprint is invalid.");
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(publicKey, out var consumed);
        if (consumed != publicKey.Length || !ecdsa.VerifyHash(
                Convert.FromBase64String(envelope.DigestBase64),
                Convert.FromBase64String(envelope.SignatureBase64), DSASignatureFormat.Rfc3279DerSequence))
            throw new CryptographicException("Offline recovery signature verification failed.");
    }

    public static byte[] SerializeBundle(AwsRecoveryTrustBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (bundle.Revision <= 0 || bundle.Keys.Length == 0
            || bundle.Keys.Select(static key => key.KeyArn).Distinct(StringComparer.Ordinal).Count() != bundle.Keys.Length)
            throw new InvalidDataException("The AWS recovery trust bundle is incomplete or contains duplicate keys.");
        return DatabaseBackupCanonicalJson.Serialize(bundle);
    }
}
