using System.Security.Cryptography;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.Signing;

namespace TomasAI.IFM.Framework.Storage.DatabaseBackup.AwsCloud.UnitTests;

public sealed class AwsSigningTests
{
    [Fact]
    public async Task Disabled_signing_key_fails_closed_without_producing_an_envelope()
    {
        var kms = Substitute.For<IAmazonKeyManagementService>();
        kms.SignAsync(Arg.Any<SignRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<SignResponse>>(_ => throw new DisabledException("qualification key disabled"));
        var signer = new KmsDocumentSignatureService(
            kms, AwsCloudOptionsTests.Valid(), TimeProvider.System);

        var action = () => signer.SignAsync("disabled-key"u8.ToArray(), CancellationToken.None).AsTask();

        await action.Should().ThrowAsync<DisabledException>();
        await kms.DidNotReceive().VerifyAsync(Arg.Any<VerifyRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Rollover_overlap_bundle_verifies_pre_and_post_rollover_evidence()
    {
        using var oldKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var newKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var rolloverUtc = new DateTimeOffset(2026, 8, 23, 4, 0, 0, TimeSpan.Zero);
        var oldDocument = "pre-rollover-evidence"u8.ToArray();
        var newDocument = "post-rollover-evidence"u8.ToArray();
        var oldArn = "arn:aws:kms:ca-central-1:107651266250:key/old-signing-key";
        var newArn = "arn:aws:kms:ca-central-1:107651266250:key/new-signing-key";
        var oldSignature = Sign(oldKey, oldArn, oldDocument, rolloverUtc.AddMinutes(-1));
        var newSignature = Sign(newKey, newArn, newDocument, rolloverUtc.AddMinutes(1));
        var bundle = new AwsRecoveryTrustBundle
        {
            Environment = "development",
            Revision = 2,
            CreatedUtc = rolloverUtc,
            Keys =
            [
                Trust(oldKey, oldArn, rolloverUtc.AddDays(-35), rolloverUtc.AddDays(35)),
                Trust(newKey, newArn, rolloverUtc, rolloverUtc.AddDays(35))
            ]
        };

        AwsOfflineSignatureVerifier.Verify(oldDocument, oldSignature, bundle, rolloverUtc.AddDays(1));
        AwsOfflineSignatureVerifier.Verify(newDocument, newSignature, bundle, rolloverUtc.AddDays(1));
        AwsOfflineSignatureVerifier.SerializeBundle(bundle).Should().NotBeEmpty();

        var withoutOldKey = bundle with { Revision = 3, Keys = [bundle.Keys[1]] };
        var action = () => AwsOfflineSignatureVerifier.Verify(
            oldDocument, oldSignature, withoutOldKey, rolloverUtc.AddDays(1));
        action.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Trust_bundle_rejects_wrong_key_identity_and_invalid_fingerprint()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var signedUtc = DateTimeOffset.UtcNow;
        var document = "trusted-content"u8.ToArray();
        var arn = "arn:aws:kms:ca-central-1:107651266250:key/trusted";
        var signature = Sign(key, arn, document, signedUtc);
        var trusted = Trust(key, arn, signedUtc.AddDays(-1), signedUtc.AddDays(1));
        var wrongIdentity = new AwsRecoveryTrustBundle
        {
            Environment = "development", Revision = 1, CreatedUtc = signedUtc,
            Keys = [trusted with { KeyArn = arn + "-other" }]
        };
        var badFingerprint = wrongIdentity with
        {
            Revision = 2,
            Keys = [trusted with { PublicKeySha256 = new string('0', 64) }]
        };

        FluentActions.Invoking(() => AwsOfflineSignatureVerifier.Verify(
                document, signature, wrongIdentity, signedUtc.AddMinutes(1)))
            .Should().Throw<CryptographicException>();
        FluentActions.Invoking(() => AwsOfflineSignatureVerifier.Verify(
                document, signature, badFingerprint, signedUtc.AddMinutes(1)))
            .Should().Throw<CryptographicException>();
    }

    static AwsSignatureEnvelope Sign(
        ECDsa key, string arn, byte[] document, DateTimeOffset signedUtc)
    {
        var digest = SHA256.HashData(document);
        return new AwsSignatureEnvelope
        {
            KeyArn = arn,
            Algorithm = "ECDSA_SHA_256",
            DigestAlgorithm = "SHA-256",
            DigestBase64 = Convert.ToBase64String(digest),
            SignatureBase64 = Convert.ToBase64String(
                key.SignHash(digest, DSASignatureFormat.Rfc3279DerSequence)),
            SignedUtc = signedUtc
        };
    }

    static AwsRecoveryTrustedKey Trust(
        ECDsa key, string arn, DateTimeOffset fromUtc, DateTimeOffset untilUtc)
    {
        var publicKey = key.ExportSubjectPublicKeyInfo();
        return new AwsRecoveryTrustedKey(
            arn, "ECC_NIST_P256", "SIGN_VERIFY", "ECDSA_SHA_256",
            Convert.ToBase64String(publicKey), Convert.ToHexString(SHA256.HashData(publicKey)),
            fromUtc, untilUtc);
    }
}
