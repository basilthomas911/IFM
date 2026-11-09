# Source inventory files

Read the [feasibility and change-boundary report](../../Application-Backtesting-Shared-Code-Inventories-2026-11-09.md) first.

Snapshot date: 2026-11-09; source HEAD d2c234b9c. Existing uncommitted changes at capture were the backtesting design/specification documents. Production code was not changed for the audit.

C# syntax extraction used Roslyn from .NET SDK 10.0.302 (preview language parsing); Rust/PowerShell/SQL inventories are bounded lexical searches. Source enumeration used `rg --files` for `.cs`, `.rs`, `.ps1`, excluding bin/obj/target, artifact folders/junctions and `TWS API`. SQL candidates additionally include `.sql`. Inline Rust test modules are separated; lexical and short-name inheritance limitations are documented in the report. `HasDefault` means an interface body/default exists, not that it succeeds. A CSV row is not automatically an edit request.

- `market-data-api-members.csv`: all 76 exact signatures/properties, current location/default status, required replay capability and behavior.
- `message-declarations.csv`: declarations/base families and declared origin treatment, including non-production scope.
- `message-time-properties.csv`: declared DateTime/DateTimeOffset/DateOnly/TimeOnly/TimeSpan properties and serialization attributes.
- `message-system-time-properties.csv`: subset with a direct system time expression.
- `message-origin-consumers.csv`: candidate reads of origin; includes helpers/tests, not all are business handlers.
- `csharp-clock-sites.csv`: syntax sites, scope, method/property, expression and provisional treatment.
- `csharp-clock-files.csv`: file index and role counts; not an edit count.
- `rust-clock-sites.csv`: timer/elapsed/system-time candidates, tests/vendor retained with labels.
- `rust-business-clock-seams.csv`: explicit existing native business-clock reads/constructions; includes tests.
- `powershell-clock-sites.csv`: launcher/logging/timeout candidates.
- `database-clock-candidates.csv`: SQL/server-side time text candidates, including defaults/physical lease timestamps.
- `summary.json`: counts corresponding to these inventories.

Locations describe this snapshot and can move with later commits. Preserve the distinction between observed syntax, proposed treatment and verified runtime behavior. No runtime tests, replay, data acquisition or Development service changes were performed.
