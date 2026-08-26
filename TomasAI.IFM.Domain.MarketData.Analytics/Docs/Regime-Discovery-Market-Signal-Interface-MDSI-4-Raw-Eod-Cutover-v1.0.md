# Regime Discovery Market Signal Interface MDSI-4 Raw EOD Cutover

Raw Futures EOD Responsibility Cutover v1.0

| Item | Value |
| --- | --- |
| Gate | `MDSI-4 - Raw Futures EOD cutover` |
| Status | Complete |
| Date | 2026-08-25 |
| Compatibility boundary | Legacy EOD read tables retained for MDSI-17 consumer removal |
| Design authority | `Regime-Discovery-Market-Signal-Interface-Design-v1.0.md` |
| Implementation authority | `Regime-Discovery-Market-Signal-Interface-Implementation-v1.0.md` |

## 1. Gate conclusion

Feed no longer calculates or owns Bollinger Bands, moving averages, market
classification, or other Analytics results when writing Futures EOD data. The
active command projector now converts the compatibility event into a raw Daily
observation containing session OHLCV and lineage only, then writes it through
`IHistoricalObservationStore`.

## 2. Responsibility boundary

The removed `BollingerBands` model and simplified Feed EOD model prevent new
derived calculations in Feed. The raw factory copies only OHLCV and constructs
session boundaries, deterministic observation identity, and source lineage.
The realtime cutover also prevents the former partial legacy session writer
from racing the shared observation coordinator.

`FuturesEodAnalyticsAssembler` is the compatibility read boundary. It accepts
one raw EOD row plus exact Analytics signal results and rejects mismatched
observation identities. Missing signal families remain explicit; the assembler
does not recalculate a missing value.

## 3. Legacy schema decision

`futures_eod_data` and its existing query models remain temporarily because
the MDSI-0 inventory identifies Feed API, Analytics, and UI consumers that have
not yet migrated. They are a frozen compatibility read surface, not the active
raw writer. Their deletion remains assigned to MDSI-17 after those consumers
use exact signal results. Removing them in this gate would violate the baseline
rule that a compatibility surface is deleted only after its callers migrate.

## 4. Accepted qualification

| Suite | Passed | Skipped | Failed |
| --- | ---: | ---: | ---: |
| Feed unit | 489 | 0 | 0 |
| Feed BDD | 314 | 0 | 0 |
| Feed integration | 46 | 4 | 0 |
| Application MarketData unit | 80 | 0 | 0 |

Focused integration coverage deletes the prior immutable test identity, sends
the compatibility command through the actor/event/projector cycle, reads the
raw row, and proves its OHLCV exactly matches the committed event. Unit tests
prove the assembler uses exact observation identities and never recalculates.

## 5. Exit decision

The active write contains raw session facts only and enriched compatibility is
assembled from exact Analytics inputs. The legacy read surface is explicitly
deferred to its already-planned consumer-removal gate. MDSI-4 is complete.
