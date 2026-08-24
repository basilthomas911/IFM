# UIR-13 Development qualification - 2026-08-24

| Item | Result |
| --- | --- |
| Gate | UIR-13 full qualification and documentation closeout |
| Configuration | Debug, .NET 10, Development services, unlocked interactive Windows desktop |
| Decision | Passed; the UI service/model boundary is Implemented |

## Completed validation

| Validation | Result |
| --- | --- |
| Sequential solution build | Passed; 0 warnings, 0 errors |
| Presentation unit and architecture | 232 passed |
| Non-live UI system and composition coverage | 35 passed |
| G0 startup | Passed all 25 steps; run `20260824-183930-593f79d8ae664aee94a3ea6a983d1d84` |
| G1 navigation/query | Passed all 15 steps; run `20260824-190550-41c795350c5b4189a2fe4707df4854c9` |
| G2 yield curve | Passed 11 steps; run `20260824-192754-9a542381493e4aabae50fb1775c77bf9` |
| G2 economic calendar | Passed 11 steps; run `20260824-192911-df60e802e7954799b667dfa8bc0e0da4` |
| G2 lookup | Passed 10 steps; run `20260824-193026-1dc59d0b31a748438619fc28cc5fafd6` |
| G2 fund | Passed 10 steps; run `20260824-193127-a9411b9dabef4b088162dbd6ff690527` |
| G2 order/trade | Passed 13 steps; run `20260824-193234-dd65049e9eb44381bd97ee4b991cfdcd` |
| G2 securities | Passed all 13 steps; run `20260824-203009-0686d418bbcc49018172ccb0cf651b26` |
| G2 end of day, backup, cleanup, and shutdown | Passed G2-035 through G2-038; run `20260824-204736-749c508f5024456e88498ca5f5285d63` |
| G3 live NATS correlation | Passed; one test |
| G4 live transport reconnect | Passed; one test, including failure correlation and exactly-once listener behavior |
| G4 process resilience | Passed; run `20260824-205323-362389379a494ff68890d4c01e5b6b31`, including three lifecycle cycles and the visible 10,000-event/500-row burst |
| Cleanup | No API, desktop, backup-host, or testhost process remained after the sequence |

G0 used the configured live DataBento provider. G1 and isolated G2 domain slices used bounded synthetic feed input so navigation and reversible command qualification were not coupled to an external provider stream. Each passing G2 slice restored or removed its run-owned Development fixtures.

## Qualification corrections

- The G2 date helper now uses the writable DateTimePicker property with bounded generic/native fallbacks, so the editor submits the requested fixture date rather than its default date.
- The G2 backup request uses a run-isolated, allowlisted PostgreSQL protection set. This prevents another Development backup host from claiming the test operation and changing its producing-host identity.
- The backup view enables Request Backup only when a protection set is checked, matching the command precondition presented to operators.
- Repeated status-log notifications are coalesced at the WinForms dispatch boundary. The view renders the latest bounded snapshot instead of repainting obsolete intermediate snapshots during a burst.
- G4 supplies a complete bounded Synthetic futures catalog because provider behavior is outside the lifecycle-resilience decision.

The initial unsliced G2 run also recorded a native DataBento `RingOverrun` while stopping the external feed. G0 had already accepted the live feed lifecycle, so the reversible G2 domain operations were rerun as isolated bounded slices; the provider error is retained in run `20260824-190818-9ad895e59a204309a519f41932afa184` and is not hidden.

## Final dependency metrics

| Measure | UIR-0 baseline | Current |
| --- | ---: | ---: |
| C# files in `UI.Net.Models` | 37 | 6 |
| `BaseModel` derivations | 32 | 0 |
| Project references owned by `UI.Net.Models` | 14 | 4 |
| `GetModel<TModel>()` calls in ViewModels | 150 | 0 |
| ViewModel files containing backend `*ReadModel` boundary DTOs | 31 | 24 |
| Models files containing transport/API/event-consumer ownership | Not separately counted | 0 |
| C# files in `UI.Net.Services` | 0 | 46 |

The remaining 24 ViewModel boundary-DTO files are the reviewed workflow contracts documented in [UI backend boundary DTO inventory](../UI-Backend-Boundary-DTOs.md); they do not restore service-location or transport ownership to ViewModels.

## Exit decision

UIR-13 is complete. The solution and non-live suites pass, every accepted G0-G4 live check passes with bounded cleanup, the final dependency metrics satisfy the approved boundary, and the decision record is marked Implemented.
