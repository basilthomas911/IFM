# Rust MarketData implementation

Implementation root for the new, separately selectable Rust FFI backend. The
current backend and `native.rust/DatabentoFeed.Rust` are preserved.

## Implementation status

**DBNMETA-001 is addressed at the shared live/historical/file decoder boundary by a fixed 65,536-byte streaming
metadata decoder and a scoped SDK hook.** See [qualification report](docs/METADATA-STREAMING-QUALIFICATION.md).
The isolated DBN copy now routes public sync/async/compressed/dynamic metadata
entry points through bounded parsing. The current backend's dependency remains
unchanged. Complete API/production integration gates remain pending.

The new managed facade explicitly maps all 73 API/property routes to native
lifecycle/data owners or concrete Host application policy/storage services. The
opt-in Host is in `host/`; the current Host and provider registration remain the
default. One user-approved exception adds backward-compatible backend ownership
metadata and native owner replacement validation in the API recovery caller.
C# process recovery still requires a changed PID. Original source hashes remain
checked after reversing only that exact approved contract addition.

Qualification includes exact C# pricing/definition/reference/history comparisons,
Tokio cancellation and metadata allocation tests, real localhost SDK sessions,
whole-owner replacement, stable readers, isolated NATS/API ingress and PostgreSQL
persistence. See [status and evidence](docs/IMPLEMENTATION-STATUS.md) and the
[operation matrix](docs/OperationMatrix.json). A component pass is not full live
operation/actor/UI acceptance. Production cutover remains disabled.

Build separate packages with `scripts/Build-NativeHost.ps1 -Configuration Debug`
or `Release`. Debug uses Cargo's optimized development profile; Release uses the
release profile. Packages go only to `.artifacts/rust-marketdata-package/`; source
Host binaries and the legacy provider are not overwritten. The package contains
one new provider DLL and excludes the old Databento/Treasury driver libraries.
The managed Host still requires .NET 10; Rust replaces provider work, not this Host.

The new Host reads the same API `ConnectionStrings`, `Storage`, `AppSettings`,
`MarketDataRecovery` and `Telemetry` configuration sections, plus explicit
`MarketDataHost:Rust` test/deployment endpoints, DLL path, DNS and Tokio thread
count. `DATABENTO_API_KEY` and existing FMP key configuration stay environment
based. ValueDate is supplied by the API; neither Host nor Rust invents one.
To select this package, use its `TomasAI.IFM.Application.MarketData.Host.exe` as
the sole Host for the chosen NATS scope and pass
`--MarketDataHost:ApiConfigurationDirectory=<API configuration directory>`.
Do not run two Host publishers on one scope. Existing actor/API contracts and
remote clients remain the same. Rollback selects the untouched current Host
package for that scope. No production selection has been performed.

`scripts/Prepare-NativeAcceptance.ps1` stages a separate Server Manager package
whose Development configuration selects the new native Host and the rebuilt
Release API. It preserves the existing Manager/VS Code settings. `-Start` launches
that explicit acceptance session only when no API/Host/UI is already running.
The existing standalone eGui and Development client profile are reused. Build
the Release API after the approved recovery caller change before using this path.
This is an opt-in acceptance configuration, not a default deployment switch.

`qualification/LiveProviderProbe` is a separate, explicit real-provider transport
check. It requires an API-supplied ValueDate/UTC anchor and an exact ES raw symbol;
it acquires one native definition, subscribes, resets once and joins. It publishes
no application events and sends no orders. It is never run by the offline suite.
See [documented differences](docs/PARITY-DIFFERENCES.md) before final acceptance.

## Ownership

An owned future contains the entire provider client. Cancellation drops that
future, including the SDK reader and writer socket halves. Awaiting the owner
confirms this destructor path has completed. Dropping a task join handle or merely
setting cancellation is not cleanup proof. Setup operations that are not
cancellation safe are abandoned with their whole session.

Recovery `runtime::Deadline` is absolute and capped at 120 seconds. Historical
operation deadlines are separately capped at 30 minutes; lane lifecycle operations
can never inherit that longer cap. Reusing it for a retry
consumes the original budget. A separate observer can report expiration without
waiting for an unresponsive owner. Provider SDK heartbeat detection is separate
from this recovery deadline.

All qualification servers bind localhost ephemeral ports and use fabricated API
keys. They do not connect to Databento, NATS, PostgreSQL or the running Host/API.

## Verification

Run the complete offline component/FFI qualification with per-stage logs and a
machine-readable results report:

```powershell
& TomasAI.IFM.Application.MarketData.Rust/qualification/Verify-Offline.ps1
```

`-IncludeProductionDeadlines` additionally runs the real two-minute SDK/DNS
deadline fixtures. These tests use localhost peers; full Host/actor/UI and live
provider acceptance remain separate gates.

From the repository root:

```powershell
cargo fmt --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml --check
cargo clippy --locked --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml --all-targets -- -D warnings
cargo test --locked --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml
cargo test --locked --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml --test production_deadline -- --ignored --nocapture
cargo test --locked --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml --test dns_lifecycle production_dns_deadline -- --ignored --nocapture
$env:RUSTDOCFLAGS = '-D warnings'
cargo doc --locked --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml --no-deps
```

Managed asynchronous ABI qualification (no Host/API or live provider required):

```powershell
cargo build --locked --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml
dotnet build TomasAI.IFM.Application.MarketData.Rust/qualification/RuntimeQualification/RuntimeQualification.csproj --artifacts-path .artifacts/rust-marketdata-managed -m:4
dotnet .artifacts/rust-marketdata-managed/bin/RuntimeQualification/debug/RuntimeQualification.dll C:/repos/IFM/TomasAI.IFM.Application.MarketData.Rust/target/debug/ifm_market_data_native.dll TomasAI.IFM.Application.MarketData.Rust/baseline/FrameworkOracle.json

dotnet run --project TomasAI.IFM.Application.MarketData.Rust/qualification/PricingBridge -- C:/repos/IFM/TomasAI.IFM.Application.MarketData.Rust/target/debug/ifm_market_data_native.dll C:/repos/IFM/TomasAI.IFM.Application.MarketData.Rust/baseline
dotnet run --project TomasAI.IFM.Application.MarketData.Rust/qualification/ApplicationPricingBridge -- C:/repos/IFM/TomasAI.IFM.Application.MarketData.Rust/target/debug/ifm_market_data_native.dll
```

Regenerate the exhaustive normalization fixture from unchanged production C#:

```powershell
dotnet build TomasAI.IFM.Application.MarketData.Rust/qualification/NormalizationOracle/NormalizationOracle.csproj --artifacts-path .artifacts/rust-marketdata-normalization -m:4
dotnet .artifacts/rust-marketdata-normalization/bin/NormalizationOracle/debug/NormalizationOracle.dll TomasAI.IFM.Application.MarketData.Rust/baseline/NormalizationOracle.json
powershell -File TomasAI.IFM.Application.MarketData.Rust/qualification/Write-OperationMatrix.ps1
```

The normalization oracle calls internal production methods using reflection only
inside the qualification executable. It does not copy the expected formulas or
modify production visibility. All oracle builds use isolated artifact directories.

The explicit production-deadline test takes two real minutes. It proves SDK socket
cancellation and observer timing, not a working actor pipeline or UI notification.
Those require later adapter and live acceptance tests.

## Cancellable gateway resolution

`dns::Resolver` requires 1-4 explicitly configured **numeric recursive DNS server
endpoints supporting TCP DNS** (normally port 53). There is no OS DNS fallback,
search suffix expansion, DNS cache, detached task or `spawn_blocking` resolver.
Hickory 0.25.2 supplies packet encoding/validation types only; socket lifecycle is
owned by this crate. This differs from the C# backend's system resolver transport,
not provider pricing or gateway names. TCP DNS reachability must be checked in the
deployment environment before enabling this backend; that check is not yet done.

`qualify_named_session` resolves A/AAAA and bounded CNAME chains on every attempt,
then supplies the numeric address slice to the unchanged SDK. Configured recursive
servers race; losers are dropped. A/AAAA results from the winning server are
combined, including a usable family when the other reports an error. A stalled
family still consumes the caller's original deadline; another complete resolver
can win. There is no additional DNS or SDK connect/auth deadline.

Resolution is limited to 16 concurrent calls, at most 128 exchanges with four
servers, one TCP socket per exchange and 65,535 bytes per DNS message. Cancelled
capacity waiters open no sockets. Answers are not retained between attempts, so
TTL expiry cannot produce stale addresses. These bounds are per Resolver instance;
the eventual backend must own one resolver, rather than construct one per request.

The default SDK hostname builder remains unsuitable: all new backend gateway
sessions use this owned named path or already-qualified numeric addresses. Live,
replay and historical transports use the cancellable resolver.

## Baseline and provenance

`baseline/CurrentBackend.json` freezes normalized source/build hashes and the
complete interface method list at the recorded commit. The baseline test detects
changes to those existing inputs; it is not a financial parity or performance test.

The initial SDK builder/session ownership construction is adapted from the
existing `native.rust/DatabentoFeed.Rust/src/live.rs`, using the same pinned
Databento SDK 0.58.0. The new implementation replaces interval-based cancellation
checks with an event-driven owner deadline/token and disables separate SDK
connect/auth timers inside this qualification owner. No old crate or DLL is
modified. Native record decoding, aggregation, pricing and managed publication
are now implemented; their component evidence is recorded separately from final
live Host/actor/UI acceptance.


Live managed/native bridge qualification:

```powershell
cargo build --locked --profile development --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml
cargo run --locked --manifest-path TomasAI.IFM.Application.MarketData.Rust/Cargo.toml --example qualification_fixture -- .artifacts/rust-marketdata-live-fixture.dbn
dotnet run --project TomasAI.IFM.Application.MarketData.Rust/qualification/LiveBridge -- C:/repos/IFM/TomasAI.IFM.Application.MarketData.Rust/target/development/ifm_market_data_native.dll C:/repos/IFM/.artifacts/rust-marketdata-live-fixture.dbn
```

The generated DBN fixture is offline acceptance data, not a provider connection.
Native batch downloads keep one fixed heap scratch buffer: placing a 64 KiB array
inside nested async futures caused a small-stack failure in qualification and was
corrected. Download output remains provisional until size/checksum verification;
application durable staging/checkpoints are not bypassed by the streaming API.


### Heartbeat connection health

Live sessions explicitly request a 30-second heartbeat. Any valid DBN record is
activity; a busy feed need not send separate heartbeats. The SDK reports silence
after 40 seconds (interval plus ten), starting the full 120-second native recovery
budget at failure detection. Missing, unchanged or expired financial quotes alone
do not establish a broken connection and do not trigger dataset replacement.

Connection observation and financial readiness are separate. Feed-start commands
may complete when the provider connection is alive; business operations still
require their actual reference/pricing/publication proofs and real quote values.
The observer holds copied channel state and cannot retain cancellation ownership.
Tests cover 15 simulated quiet minutes, real heartbeat-only SDK traffic, silent
SDK peers, resource cancellation and the original option/futures pipelines.
