//! Native ordered futures event assembly over source-time coherent outputs.
//! Preserves the current service's quote batches, replay
//! checkpoints and two-second VWAP cadence without a managed receiver or pool.

use super::events::{Identity, Instrument};
use crate::{
    dataset::{
        book::Update,
        live::{Output, OutputSource, Registration},
        records::{Observation, Quote},
        transport::Lane,
    },
    latest::Asset,
};
use rmpv::Value;
use std::collections::BTreeMap;

/// Detached existing event payload and DatasetPublicationKind identity.
pub struct Event {
    /// Quote=2, market price (including nested source trade/VWAP)=3, statistics=4.
    pub kind: u8,
    /// Fully native existing numeric-key MessagePack DTO, before compression.
    pub value: Value,
}
struct State {
    instrument: Instrument,
    vwap: bool,
    vx: bool,
    physical: u128,
    sequence: i64,
    last_vwap: i64,
    observed: i64,
    quotes: Vec<Quote>,
}
/// One bounded native futures lane assembler. No provider, broker or storage
/// await occurs while this object mutates its instrument-local wire buffers.
pub struct Futures {
    lane: Lane,
    capacity: usize,
    instruments: BTreeMap<u32, State>,
}
impl Futures {
    /// Creates exact source mappings and fixed per-instrument quote capacity.
    ///
    /// # Errors
    /// Rejects wrong assets, duplicates, invalid mappings or unbounded capacity.
    pub fn new(
        lane: Lane,
        date: time::Date,
        definition: time::Date,
        registrations: Vec<Registration>,
        capacity: usize,
    ) -> Result<Self, &'static str> {
        if lane == Lane::OptionsGlbx
            || !(1..=4096).contains(&capacity)
            || registrations.is_empty()
            || registrations.len() > 2048
        {
            return Err("Invalid native futures publication configuration");
        }
        let mut instruments = BTreeMap::new();
        for registration in registrations {
            if registration.asset != Asset::Futures {
                return Err("Futures publication requires futures registrations");
            }
            let id = registration.instrument;
            let vwap = registration.vwap;
            let vx = registration.contract.starts_with("VX");
            let instrument = Instrument::new(registration, lane, date, definition)?;
            if instruments
                .insert(
                    id,
                    State {
                        instrument,
                        vwap,
                        vx,
                        physical: 0,
                        sequence: 0,
                        last_vwap: 0,
                        observed: 0,
                        quotes: Vec::with_capacity(capacity),
                    },
                )
                .is_some()
            {
                return Err("Duplicate native futures registration");
            }
        }
        Ok(Self {
            lane,
            capacity,
            instruments,
        })
    }
    fn next(state: &State) -> Result<i64, &'static str> {
        state
            .sequence
            .checked_add(1)
            .ok_or("Futures publication sequence exhausted")
    }
    fn flush(
        state: &mut State,
        reason: u8,
        observed: i64,
        output: &mut Vec<Event>,
    ) -> Result<(), &'static str> {
        if state.quotes.is_empty() {
            return Ok(());
        }
        let next = Self::next(state)?;
        output.push(Event {
            kind: 2,
            value: state.instrument.quotes(
                &state.quotes,
                reason,
                Identity::fresh(observed),
                next,
            )?,
        });
        state.sequence = next;
        state.quotes.clear();
        Ok(())
    }
    fn vwap(
        state: &mut State,
        source: &Output,
        snapshot: Option<crate::latest::market::Snapshot>,
        output: &mut Vec<Event>,
    ) -> Result<(), &'static str> {
        let Some(cp) = source
            .state
            .vwap
            .filter(|cp| state.vwap && cp.replay_complete)
        else {
            return Ok(());
        };
        if state.last_vwap != 0 && source.observed.saturating_sub(state.last_vwap) < 20_000_000 {
            return Ok(());
        }
        output.push(Event {
            kind: 3,
            value: state.instrument.market(
                snapshot,
                2,
                Some(cp),
                None,
                Identity::fresh(source.observed),
            )?,
        });
        state.last_vwap = source.observed;
        Ok(())
    }
    /// Assembles one current physical contribution in existing publication order.
    /// Caller fences obsolete generations before entry and admits all resulting
    /// events to its bounded native outbox; admission failure is a visible fault.
    ///
    /// # Errors
    /// Rejects unqualified identities, changed source publishers and overflow.
    pub fn process(&mut self, source: &Output) -> Result<Vec<Event>, &'static str> {
        if source.lane != self.lane || source.generation == 0 {
            return Err("Invalid futures output lane/generation");
        }
        let (instrument, flags, publisher) = match source.source {
            OutputSource::ReplayComplete(id) | OutputSource::StatisticsReplayComplete(id) => {
                (id, 0, None)
            }
            OutputSource::Observation(record) => {
                let header = match record {
                    Observation::Quote(q) => q.header,
                    Observation::Trade(t) => t.header,
                    Observation::Order(o) => o.header,
                    Observation::Statistic(s) => s.header,
                };
                (header.instrument, header.flags, Some(header.publisher))
            }
        };
        let state = self
            .instruments
            .get_mut(&instrument)
            .ok_or("Unqualified native futures output")?;
        if let Some(publisher) = publisher {
            state.instrument.source(instrument, publisher)?;
        }
        let mut output = Vec::new();
        if state.physical != source.generation {
            if state.physical != 0 {
                Self::flush(state, 5, state.observed, &mut output)?;
            }
            state.physical = source.generation;
            state.sequence = 0;
            state.last_vwap = 0;
        }
        state.observed = source.observed;
        if flags & 2 != 0 {
            return Ok(output);
        }
        match source.source {
            OutputSource::StatisticsReplayComplete(_) => {
                if let Some(stats) = source.state.session.filter(|s| s.has_any()) {
                    output.push(Event {
                        kind: 4,
                        value: state
                            .instrument
                            .statistics(stats, Identity::fresh(source.observed))?,
                    });
                }
            }
            OutputSource::ReplayComplete(_) => {
                if source.state.vwap.is_some_and(|cp| cp.eligible > 0) {
                    Self::vwap(state, source, None, &mut output)?;
                }
                state.last_vwap = 0;
                if let Some(stats) = source.state.session.filter(|s| s.has_any()) {
                    output.push(Event {
                        kind: 4,
                        value: state
                            .instrument
                            .statistics(stats, Identity::fresh(source.observed))?,
                    });
                }
            }
            OutputSource::Observation(Observation::Quote(quote))
                if source.state.update == Some(Update::Quote) =>
            {
                if state.vx {
                    output.push(Event {
                        kind: 3,
                        value: state.instrument.market(
                            source.state.market,
                            1,
                            None,
                            None,
                            Identity::fresh(source.observed),
                        )?,
                    });
                }
                state.quotes.push(quote);
                if state.quotes.len() == self.capacity {
                    Self::flush(state, 1, source.observed, &mut output)?;
                }
            }
            OutputSource::Observation(Observation::Trade(trade))
                if matches!(source.state.update, Some(Update::Trade { .. })) =>
            {
                Self::vwap(state, source, source.state.market, &mut output)?;
                Self::flush(state, 2, source.observed, &mut output)?;
                // Bars require every accepted execution, including unchanged
                // prices. Filtering here creates false gaps in TradeOrdinal and
                // loses volume; the query/display cache retains its own filter.
                {
                    let next = Self::next(state)?;
                    let trade =
                        state
                            .instrument
                            .trade(trade, Identity::fresh(source.observed), next)?;
                    output.push(Event {
                        kind: 3,
                        value: state.instrument.market(
                            source.state.market,
                            2,
                            None,
                            Some(trade),
                            Identity::fresh(source.observed),
                        )?,
                    });
                    state.sequence = next;
                }
            }
            OutputSource::Observation(Observation::Statistic(_))
                if matches!(source.state.update, Some(Update::Statistics(_))) =>
            {
                if let Some(stats) = source.state.session.filter(|s| s.has_any()) {
                    output.push(Event {
                        kind: 4,
                        value: state
                            .instrument
                            .statistics(stats, Identity::fresh(source.observed))?,
                    });
                }
            }
            _ => {}
        }
        Ok(output)
    }
    /// Flushes graceful stop/removal buffers. Emergency reset uses `retire` to
    /// discard obsolete display work without waiting on any broker publication.
    ///
    /// # Errors
    /// Rejects invalid reason/timestamp or exhausted event identity.
    pub fn flush_pending(&mut self, reason: u8, observed: i64) -> Result<Vec<Event>, &'static str> {
        let mut output = Vec::new();
        for state in self.instruments.values_mut() {
            Self::flush(state, reason, observed, &mut output)?;
        }
        Ok(output)
    }
    /// Drops obsolete wire buffers and source continuity during joined reset.
    pub fn retire(&mut self) {
        for state in self.instruments.values_mut() {
            state.quotes.clear();
            state.physical = 0;
            state.sequence = 0;
            state.last_vwap = 0;
        }
    }
}
