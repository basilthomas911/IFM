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
    pub(crate) fn checkpoint(&self)->Value{
        Value::Array(vec![1.into(),(self.lane as u8).into(),(self.capacity as u64).into(),Value::Array(self.instruments.iter().map(|(id,state)|Value::Array(vec![(*id).into(),state.instrument.capture_identity(),state.vwap.into(),state.vx.into(),crate::checkpoint::wide(state.physical),state.sequence.into(),state.last_vwap.into(),state.observed.into(),Value::Array(state.quotes.iter().map(crate::checkpoint::quote).collect())])).collect())])
    }
    pub(crate) fn restore_checkpoint(value:&Value)->Result<Self,String>{
        use crate::checkpoint as c;let f=c::fields(value,4)?;if c::unsigned(&f[0])?!=1{return Err("Unknown futures mapper checkpoint".into());}
        let lane=c::lane(&f[1])?;let capacity=usize::try_from(c::unsigned(&f[2])?).map_err(|_|"Mapper capacity overflow")?;
        if lane==Lane::OptionsGlbx||!(1..=4096).contains(&capacity){return Err("Invalid mapper checkpoint bounds".into());}
        let values=f[3].as_array().filter(|v|!v.is_empty()&&v.len()<=2048).ok_or("Invalid mapper checkpoint registrations")?;
        let mut instruments=BTreeMap::new();
        for value in values{let fields=c::fields(value,9)?;let id=u32::try_from(c::unsigned(&fields[0])?).map_err(|_|"Invalid mapper instrument")?;let instrument=Instrument::restore_identity(&fields[1])?;
            let quotes=fields[8].as_array().filter(|v|v.len()<=capacity).ok_or("Invalid mapper quote capacity")?.iter().map(c::read_quote).collect::<Result<Vec<_>,_>>()?;
            let identity=instrument.capture_identity();let identity=c::fields(&identity,12)?;
            let vwap=c::boolean(&fields[2])?;let vx=c::boolean(&fields[3])?;
            if c::unsigned(&identity[0])?!=u64::from(id)||c::lane(&identity[8])?!=lane||c::boolean(&identity[6])?!=vwap||c::string(&identity[2],256)?.starts_with("VX")!=vx{return Err("Mapper checkpoint registration mismatch".into());}
            let state=State{instrument,vwap,vx,physical:c::read_wide(&fields[4])?,sequence:c::number(&fields[5])?,last_vwap:c::number(&fields[6])?,observed:c::number(&fields[7])?,quotes};
            if state.sequence<0||instruments.insert(id,state).is_some(){return Err("Invalid duplicate mapper checkpoint state".into());}
        }
        Ok(Self{lane,capacity,instruments})
    }
    pub(crate) fn capture_identity(&self)->Value{Value::Array(self.instruments.values().map(|state|state.instrument.capture_identity()).collect())}
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
        if !emit_vwap_checkpoint(cp.ordinal, state.last_vwap, source.observed) {
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
                // Publish the coherent cumulative session volume before the trade
                // that consumes it on the API side. The remote mirror must not
                // depend on an unrelated official-statistics change to advance.
                if let Some(stats) = source.state.session.filter(|s| s.has_volume()) {
                    output.push(Event {
                        kind: 4,
                        value: state
                            .instrument
                            .statistics(stats, Identity::fresh(source.observed))?,
                    });
                }
                Self::vwap(state, source, source.state.market, &mut output)?;
                Self::flush(state, 2, source.observed, &mut output)?;
                // Bars require every accepted execution, including unchanged
                // prices. Filtering here creates false gaps in TradeOrdinal and
                // loses volume; the query/display cache retains its own filter.
                {
                    let next = Self::next(state)?;
                    let (identity, stored_sequence) = source.raw.map_or_else(
                        || (Identity::fresh(source.observed), next), |stamp| (stamp.identity, stamp.sequence));
                    let trade =
                        state
                            .instrument
                            .trade(trade, identity, stored_sequence)?;
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

// The completed replay/first execution is an ownership boundary, not a periodic
// display update. ITI intake must see it before the first trade of a new epoch.
fn emit_vwap_checkpoint(ordinal: i64, last: i64, now: i64) -> bool {
    ordinal <= 1 || last == 0 || now.saturating_sub(last) >= 20_000_000
}

#[cfg(test)]
mod checkpoint_boundary_tests {
    use super::emit_vwap_checkpoint;

    #[test]
    fn first_live_execution_is_not_hidden_by_recent_replay_checkpoint() {
        assert!(emit_vwap_checkpoint(0, 0, 100));
        assert!(emit_vwap_checkpoint(1, 100, 101));
        assert!(!emit_vwap_checkpoint(2, 101, 102));
        assert!(emit_vwap_checkpoint(2, 101, 20_000_101));
    }
}
