# Futures VX Term Structure Signal Implementation v1.0

Date: 2026-08-26
Status: Implemented baseline

## Purpose

`FuturesVxTermStructureSignal` replaces the unused `VixVolatility` scaffold
with the concrete first/second eligible VX futures curve needed by Market
Outlook and Regime Discovery. It does not compose the separate VIX spot index.

## Runtime design

`IMarketDataApi` owns provider-neutral contract discovery and market-data
subscriptions. During the Databento startup rollover check, the API resolves
the first two eligible VX expiries, registers them atomically as front and
back, and preserves the front contract as the current VX contract. Databento
symbol lookup remains inside the provider integration.

The VX Realtime actor registers for futures market-price updates, ensures that
both registered streams are leased, accepts eligible trades for those exact
contracts, and forwards immutable leg observations to the Command actor. It
retains no prices, accumulator, calculation state, or projector. Its only
mutable runtime object tracks external stream leases so they can be exchanged
on rollover and released at shutdown.

The event-sourced Command actor is the sole owner of the latest front/back
observations and previous calculated values. It rejects wrong-contract,
duplicate, stale, invalid, or incompatible observations and calculates only
when both legs share a stream epoch and satisfy maximum source skew.

## Calculation

- spread: `back price - front price`;
- front/back ratio: `front price / back price`;
- term-structure percent: `(back price / front price) - 1`;
- Contango: percentage is above the configured flat epsilon;
- Backwardation: percentage is below the negative epsilon;
- Flat: absolute percentage is within the epsilon.

The generated Command event contains the replayable checkpoint and optional
paired signal. Its Command-folder event projector writes valid paired signals
to `futures_vx_term_structure_signal` and publishes complete/fail lifecycle
events. The stateless Event actor dispatches those events through typed
extension handlers. The Query actor returns the latest value-date and
configuration read model.

## Rollover boundary

The actor entity ID includes value date, front contract, back contract, and
configuration. A changed pair therefore creates a new durable stream and
prevents observations from the old and new curves from mixing. Stream
ownership acquires the replacement pair before releasing the old pair.

## Development configuration

Live calculation requires two eligible VX contracts. Real Databento startup
discovers them from provider contract definitions. A synthetic development
run must configure/register two VX expiries; the implementation intentionally
does not invent a back-contract symbol when only one contract is available.

## Qualification scope

Unit coverage verifies formula classifications, skew behavior, duplicate and
stale rejection, epoch reset, identity rollover, provider expiry ordering,
subscription rollback, and stateless Realtime actor shape. Integration
coverage verifies realtime route registration and the
Command-to-projector-to-Scylla-to-Query cycle using an isolated actor identity
and configuration.

Final qualification results:

- Application MarketData unit tests: 82 passed;
- Domain MarketData Analytics unit tests: 919 passed;
- Domain MarketData Analytics BDD tests: 462 passed;
- Domain MarketData Analytics integration tests: 46 passed;
- serialized whole-solution build: succeeded with 0 warnings and 0 errors;
- `git diff --check`: no whitespace errors.

VIX spot composition, historical contract-roll replay, cache publication, and
Regime Discovery snapshot fusion are explicitly outside this baseline.
