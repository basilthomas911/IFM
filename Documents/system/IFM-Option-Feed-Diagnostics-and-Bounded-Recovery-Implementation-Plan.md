# Option feed diagnostics and bounded recovery

## Objective

Preserve Databento diagnostics and make unexpected option-processing failures observable by the existing two-minute recovery coordinator. A living worker is not proof that an enabled trade feed is working. Do not restart the running development session during implementation.

## Requirements

- Preserve provider numeric codes, severity, message, dataset and connection context in structured logs; redact credentials.
- Classify errors explicitly. Unknown errors and timeouts are unhealthy. Intentional shutdown cancellation is not failure. Provider slow-reader warnings remain advisory while quote progress is checked.
- Report failures independently of logging, including returned failure results and asynchronous maintenance failures.
- Keep monitoring enabled trade intent when all quotes are stale. One recovery episode owns the two-minute deadline and existing UI/escalation path.
- Preserve renewal ownership across replacement and replace definitively ended lease identities without reviving retired work.

## Implementation

1. Add a native diagnostic callback in Rust and C++ and a managed receiver. Forward provider errors/system warnings with raw codes and complete text. Keep native terminal exception detail through managed drain errors. Logging callback failures must be observable.
2. Introduce generation-scoped option processing health and typed failure classification. Wire Host lease maintenance, worker exceptions and API ingress into health. Export Host failure evidence through the existing backend status contract and read mirror; do not parse logs to trigger recovery.
3. Correct lease maintenance: retain registrations while admission is temporarily unavailable; reconstruct renewal metadata after replacement; allocate a new lease identity after confirmed expiry/LeaseEnded. Preserve desired owners and release semantics.
4. Update API recovery inspection to consume processing failures and actor failure evidence, retain enabled live feeds regardless of quote age, and detect missing required-leg progress. Clear old failure evidence only through generation replacement/verified recovery or session close.
5. Add focused unit/integration tests for provider codes, transient classification, unknown failures, callback logging, expired leases, replacement adoption, stale subscriptions and bounded success/failure notification. Build changed managed/native projects. Record limitations of live acceptance separately.

## Acceptance

- Error/warning diagnostics retain original provider identity and text in logs without secrets.
- LeaseEnded/unknown processing exceptions cannot leave the option pipeline green indefinitely.
- A failed transient retry cannot reset the recovery deadline indefinitely.
- All four required legs and current position/plan publication must be verified before recovery succeeds.
- Failure after the deadline uses the existing UI notification and configured single API restart escalation.
- No orders or running process mutations are performed by verification.

## Status

Implementation complete; final automated verification recorded below. Live acceptance requires deploying matching Host, worker, API and native-driver binaries and a controlled test session. No running process was restarted and no live fault was injected during implementation.

## Completed behavior

- Rust and C++ expose an additive process-lifetime diagnostic callback. ErrorMsg retains numeric code, name, fatality, is_last and original detail. System warnings and native terminal failures are also delivered. Managed logging adds dataset, worker, PID and generation; native feed identity identifies individual connections when available. Credential values are redacted. Callback sink exceptions cannot escape into native code and make worker diagnostics unhealthy.
- Worker API/control exceptions and aggregation/publication failures affect health. Pricing scheduler/trade-pricing and option-chain shutdown failures retain exception detail rather than only exception type.
- Host acquisition, renewal, risk capture and maintenance failures report generation-scoped health, independent of logging. Unknown exceptions/timeouts are immediately unhealthy. Explicit transient codes receive at most three attempts; intentional shutdown cancellation is excluded. Bounded evidence replaces oldest entries on overflow and still reports unhealthy.
- Backend status carries processing evidence and enabled trade intent to API. API ingress, cache assembly and synchronization failures also report health. Actor failure evidence includes option routing, position, forward-price and trade-plan actors.
- Enabled trades remain monitored with entirely stale/missing quotes. Required-leg quote/pricing freshness has a 30-second startup allowance and 30-second stale threshold. These detect failure; they do not replace or extend the two-minute recovery deadline. Disabled intent is excluded.
- Host-owned recovery deadlines propagate to API; joining recovery does not allocate another two minutes. End-to-end position, all four legs, risk, plan and downstream publication proof remain required. Existing UI notification and single API restart escalation remain the terminal path.
- Temporary admission fencing preserves renewal registrations. Confirmed ended/expired lease identities are replaced rather than retried indefinitely. Saved feed intent is retained until explicitly disabled/closed.

## Catch-boundary audit

| Boundary | Outcome |
| --- | --- |
| Provider records and native terminal errors | Full diagnostic callback plus terminal native health |
| Worker API/control processing | Redacted exception log plus latched unhealthy generation |
| Worker health/diagnostic sampling | Sampling exception logs and returns unhealthy evidence |
| Pricing/lease worker background tasks | Terminal fault or processing-health report |
| Host monitored acquisition/release and restoration | Processing-health evidence, returned failure, bounded owner recovery |
| API publication/cache/synchronization | Processing-health report before continuing independent reception |
| Option routing/position/plan actors | Mailbox failure evidence consumed by recovery inspector |
| Invalid/stale quote qualification without an exception | Unavailable risk evidence and required-leg freshness monitoring |
| Explicit shutdown cancellation/cleanup | Shutdown handling; no new trading recovery episode |

## Verification results

- Application/lease/wire/process tests: 67 passed; two existing opt-in stress/soak tests skipped.
- Recovery integration tests: 13 passed, including stale-trade failure to the 120-second UI failure event, original Host deadline adoption, noncooperative work, and successful end-to-end proof.
- Managed native-diagnostic/ABI tests: 4 passed, including credential redaction, unknown diagnostic kind, and a throwing logging sink.
- Option routing tests: 4 passed.
- Rust live-enabled release native tests: 16 passed (6 unit and 10 ABI/integration); release DLL rebuilt.
- C++ live-enabled native build and CTest suite passed.
- Managed API/Host/Worker dependency build passed with zero warnings and zero errors.

Process-test fixture corrections: child launches now use a runtime configuration/full output that supplies their logging dependencies; the synthetic composition test verifies the current UTC-converted quote behavior and retired-generation rejection. This changes test harness expectations, not market quote timestamps.

## Deployment and acceptance

Host diagnostics are now independently persisted under `Host/Logs/ifm-marketdata-host-*.log` and exported through the configured collector. Worker diagnostics remain under `Workers/Logs/ifm-dataset-worker-*.log` and the collector. Both include service/process identity.

Deploy matching binaries together: updated workers require the new native diagnostic export. Restart through the existing development launch/build workflow, load a saved enabled trade, and perform controlled success/failure injections. Confirm actual fresh quotes/plans after recovery and an actual UI dialog at terminal failure; the automated UI-event tests do not claim that this new deployment has already been observed live.
