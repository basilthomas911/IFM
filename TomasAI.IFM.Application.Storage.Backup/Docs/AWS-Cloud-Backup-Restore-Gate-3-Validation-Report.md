# AWS Cloud Backup and Restore Gate 3 Validation Report

**Gate:** 3 - AWS identity, options, and client lifecycle

**Result:** Complete

**Date:** 2026-08-21

**AWS mutation performed:** None

## Implemented result

- Credential-free options validate accounts, Regions, ARNs, buckets, table, Object Lock mode, retention, timeouts,
  retry bounds, and test/admission flags.
- Production enforces distinct trust accounts, distinct Regions, Compliance Object Lock, and temporary sessions.
- AWS SDK v4 default credential resolution and optional bounded role assumption are used; no custom access-key options
  exist.
- S3, DynamoDB, KMS, and STS clients are singleton registrations with centralized Region, timeout, and retry bounds.
- STS preflight enforces partition/account/Region and returns only safe identity/request metadata.
- Expected cancellation, throttling, expiry, denial, timeout, transport, configuration, and permanent failures map to
  bounded observations used by source-specific degraded health.

## Test evidence

| Check | Result |
| --- | --- |
| AWS unit tests | 23 passed, 0 failed |
| AWS integration tests | 5 passed, 0 failed |
| Explicit live .NET STS test | Passed for account `107651266250`, `aws`, `ca-central-1` |
| Static development credential policy | Accepted only for Development |
| Temporary-session policy | Accepted; required outside Development |
| Wrong account/Region and role/access denial | Rejected/classified |
| Expired/throttled/transient failure classification | Passed |
| Options/reflection secret scan | No credential, secret, token, or password property |
| NuGet deprecated/vulnerable audit | No deprecated or vulnerable direct/transitive package |

Credential values were mapped only in process memory for the explicit live test and were never printed, serialized,
logged, or committed.

**Rollback:** Disable AwsCloud registration. No persistent AWS dependency or resource exists.
