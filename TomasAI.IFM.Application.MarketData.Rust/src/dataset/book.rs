//! Single-writer native market-input pipeline. Registration maps provider IDs to
//! canonical contracts once. Hot observations and authoritative session volume
//! update before downstream display coalescing; no provider borrow is retained.
use super::records::{Header, Observation};
use crate::aggregation::vwap::{Checkpoint as VwapCheckpoint, Input as VwapInput, Vwap};
use crate::{
    aggregation::normalization,
    latest::market::{Market, Snapshot as MarketSnapshot, Trade as MarketTrade},
};
use crate::{
    aggregation::session::{Sessions, Snapshot},
    latest::{Asset, Contract, QuoteValue, Store, Tick, TradeValue},
};
use std::{collections::BTreeMap, sync::Arc};
use time::Date;
/// Visible pipeline failures. Callers route these into the lane recovery owner.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Failure {
    /// Invalid/duplicate provider or canonical registration.
    Registration,
    /// Latest observation owner failed; it must not remain labelled healthy.
    Latest(crate::latest::Failure),
    /// Authoritative statistics failed; never hide as a dropped display update.
    Statistics(crate::aggregation::session::Failure),
    /// Published signed trade ordinal cannot advance without wrapping identity.
    ContinuityOverflow,
    /// Authoritative VWAP input could not be accumulated exactly.
    Vwap(crate::aggregation::vwap::Failure),
    /// Checked option replay/statistics processing failure.
    OptionStatistics(crate::option_chain::statistics::Failure),
    /// A prior panic interrupted the logical option-statistics owner.
    OptionStatisticsWriter,
}
/// Output of one exact record; application publications consume these fields.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Update {
    /// A stale generation, duplicate/older observation or unknown registration.
    Unchanged,
    /// Latest quote changed; raw input preserved in the native reader.
    Quote,
    /// Latest trade changed; authoritative volume incorporated, if a future.
    Trade {
        /// Whether this accepted trade moves the published price by a tick.
        price_changed: bool,
    },
    /// Current session statistics changed, potentially for an older reference date.
    Statistics(Snapshot),
}
/// Coherent derived state captured by the single writer immediately after one
/// observation. Copies contain no provider borrows, strings or heap ownership.
#[derive(Debug, Clone, Copy, Default)]
pub struct PublicationState {
    /// Result of applying this observation; unchanged inputs must not manufacture
    /// an extra market-price publication merely because a snapshot exists.
    pub update: Option<Update>,
    /// Publication-price cache at this exact processing point.
    pub market: Option<MarketSnapshot>,
    /// Usable statistics for the input's selected date.
    pub session: Option<Snapshot>,
    /// Exact authoritative VWAP checkpoint, including invalid/incomplete flags.
    pub vwap: Option<VwapCheckpoint>,
    /// Logical option replay/official volume and open interest at this record.
    pub option_statistics: Option<crate::option_chain::statistics::Statistics>,
}
struct Entry<G> {
    id: String,
    asset: Asset,
    reader: Arc<Contract<G>>,
    sessions: Sessions,
    market: Market,
    ordinal: i64,
    stream_epoch: u128,
    vwap: Option<Vwap>,
    replay_started: bool,
    option_statistics: Option<crate::option_chain::statistics::Statistics>,
}
/// A lane-attempt book. Recovery creates a new book after retiring this one;
/// retained readers cannot accidentally read prices from the replacement.
pub struct Book<G> {
    active: bool,
    date: Date,
    generation: u128,
    capacity: usize,
    latest: Arc<Store<G>>,
    entries: BTreeMap<u32, Entry<G>>,
    ids: std::collections::BTreeSet<String>,
    option_statistics: Option<crate::option_chain::statistics::Bank>,
}
impl<G> Book<G> {
    /// Opaque owner identity, never reused for replacement readers.
    pub fn generation(&self) -> u128 {
        self.generation
    }
    /// Creates explicit API date/generation and capacities before subscriptions.
    pub fn new(date: Date, generation: u128, capacity: usize) -> Result<Self, Failure> {
        Self::with_stop(
            date,
            generation,
            capacity,
            tokio_util::sync::CancellationToken::new(),
        )
    }
    /// Links retained numeric handles to source retirement without destructor locks.
    pub fn with_stop(
        date: Date,
        generation: u128,
        capacity: usize,
        stop: tokio_util::sync::CancellationToken,
    ) -> Result<Self, Failure> {
        Ok(Self {
            active: true,
            date,
            generation,
            capacity,
            latest: Arc::new(
                Store::with_stop(date, generation, capacity, stop).map_err(Failure::Latest)?,
            ),
            entries: BTreeMap::new(),
            ids: Default::default(),
            option_statistics: None,
        })
    }
    /// Registers one canonical contract and provider instrument. Failed duplicate
    /// registration changes no state and cannot steal another contract's reader.
    pub fn register(
        &mut self,
        instrument: u32,
        id: &str,
        asset: Asset,
        retained_dates: usize,
    ) -> Result<Arc<Contract<G>>, Failure> {
        self.register_with_tick_size(instrument, id, asset, retained_dates, None)
    }
    /// Registers exact provider tick size for the market-price change threshold.
    /// Raw last observations remain independent from publication price.
    pub fn register_with_tick_size(
        &mut self,
        instrument: u32,
        id: &str,
        asset: Asset,
        retained_dates: usize,
        tick_size: Option<i64>,
    ) -> Result<Arc<Contract<G>>, Failure> {
        self.register_with_policy(instrument, id, asset, retained_dates, tick_size, false)
    }
    /// Registers the explicit application-selected authoritative VWAP source.
    /// The caller selects this policy from reviewed root/asset metadata; this
    /// pipeline never guesses it from provider symbols or nearest contracts.
    pub fn register_with_policy(
        &mut self,
        instrument: u32,
        id: &str,
        asset: Asset,
        retained_dates: usize,
        tick_size: Option<i64>,
        track_vwap: bool,
    ) -> Result<Arc<Contract<G>>, Failure> {
        if track_vwap && asset != Asset::Futures {
            return Err(Failure::Registration);
        }
        if !self.active
            || instrument == 0
            || id.trim().is_empty()
            || id.len() > 256
            || self.entries.len() >= self.capacity
            || self.entries.contains_key(&instrument)
            || self.ids.contains(id)
        {
            return Err(Failure::Registration);
        }
        let sessions = Sessions::new(retained_dates).map_err(Failure::Statistics)?;
        let vwap = if track_vwap {
            let mut value = Vwap::default();
            value.reset(self.generation);
            Some(value)
        } else {
            None
        };
        let reader = self.latest.register(id, asset).map_err(Failure::Latest)?;
        self.ids.insert(id.to_owned());
        self.entries.insert(
            instrument,
            Entry {
                id: id.to_owned(),
                asset,
                reader: reader.clone(),
                sessions,
                market: Market::new(tick_size),
                ordinal: 0,
                stream_epoch: self.generation,
                vwap,
                replay_started: false,
                option_statistics: None,
            },
        );
        Ok(reader)
    }
    /// Attaches bounded logical statistics before source processing. A new
    /// physical book copies the retained watermark rather than recounting replay.
    /// Registrations absent from this exact scope are removed from the bank.
    pub fn bind_option_statistics(
        &mut self,
        bank: crate::option_chain::statistics::Bank,
    ) -> Result<(), Failure> {
        {
            let mut values = bank.lock().map_err(|_| Failure::OptionStatisticsWriter)?;
            values.retain(|id, _| {
                self.entries
                    .get(id)
                    .is_some_and(|entry| entry.asset == Asset::Option)
            });
            for (id, entry) in self
                .entries
                .iter_mut()
                .filter(|(_, entry)| entry.asset == Asset::Option)
            {
                entry.option_statistics = Some(*values.entry(*id).or_default());
            }
        }
        self.option_statistics = Some(bank);
        Ok(())
    }
    /// Resolves an immediate coherent reader by canonical ID in this attempt.
    pub fn reader(&self, id: &str) -> Result<Option<Arc<Contract<G>>>, Failure> {
        self.latest
            .lookup(id, self.date, self.generation)
            .map_err(Failure::Latest)
    }
    /// Returns the canonical ID for publication without constructing per-tick strings.
    pub fn contract_id(&self, instrument: u32) -> Option<&str> {
        self.entries.get(&instrument).map(|e| e.id.as_str())
    }
    /// Copies the immediate raw underlying quote by its qualified instrument.
    /// A poisoned writer remains an infrastructure failure, not a missing quote.
    pub fn raw_quote(&self, instrument: u32) -> Result<Option<Tick<QuoteValue>>, Failure> {
        self.entries.get(&instrument).map_or(Ok(None), |entry| {
            entry.reader.quote().map_err(Failure::Latest)
        })
    }
    /// Copies the immediate raw trade, independently of publication-price
    /// suppression. Repeated executions at the same price still advance this.
    pub fn raw_trade(
        &self,
        instrument: u32,
    ) -> Result<Option<Tick<crate::latest::TradeValue>>, Failure> {
        self.entries.get(&instrument).map_or(Ok(None), |entry| {
            entry.reader.trade().map_err(Failure::Latest)
        })
    }
    /// Captures all derived values while the enclosing book writer is held.
    /// Official statistics may select a previous date; that date is retained.
    pub fn publication_state(&self, instrument: u32, update: Update) -> PublicationState {
        let date = if let Update::Statistics(value) = update {
            value.value_date
        } else {
            self.date
        };
        PublicationState {
            update: Some(update),
            market: if matches!(update, Update::Trade { .. }) {
                self.entries.get(&instrument).and_then(|entry| {
                    if entry.asset == Asset::Futures {
                        entry.market.execution_snapshot()
                    } else {
                        entry.market.snapshot()
                    }
                })
            } else {
                self.market_price(instrument)
            },
            session: self.statistics(instrument, date),
            vwap: self.vwap(instrument),
            option_statistics: self
                .entries
                .get(&instrument)
                .and_then(|entry| entry.option_statistics),
        }
    }
    /// Processes a copied record with the API clock's local ingestion ticks.
    /// Replay session volume is authoritative even when no live route is owned.
    pub fn apply(
        &mut self,
        generation: u128,
        record: Observation,
        local_received_ticks: i64,
    ) -> Result<Update, Failure> {
        if !self.active || generation != self.generation {
            return Ok(Update::Unchanged);
        }
        let header = match record {
            Observation::Trade(v) => v.header,
            Observation::Quote(v) => v.header,
            Observation::Statistic(v) => v.header,
            Observation::Order(v) => v.header,
        };
        let Some(entry) = self.entries.get_mut(&header.instrument) else {
            return Ok(Update::Unchanged);
        };
        match record {
            Observation::Quote(v) => {
                // Preserve baseline adapter semantics: normalized undefined wire
                // prices may be zero; only retained i64::MAX maps to nullable.
                let price = |v| if v == i64::MAX { None } else { Some(v) };
                let changed = entry
                    .reader
                    .update_quote(tick(
                        header,
                        QuoteValue {
                            bid: price(v.bid),
                            ask: price(v.ask),
                            bid_size: v.bid_size,
                            ask_size: v.ask_size,
                            bid_count: v.bid_count,
                            ask_count: v.ask_count,
                            local_received_ticks,
                        },
                    ))
                    .map_err(Failure::Latest)?;
                if changed {
                    entry.market.quote(
                        self.date,
                        tick(
                            header,
                            QuoteValue {
                                bid: price(v.bid),
                                ask: price(v.ask),
                                bid_size: v.bid_size,
                                ask_size: v.ask_size,
                                bid_count: v.bid_count,
                                ask_count: v.ask_count,
                                local_received_ticks,
                            },
                        ),
                    );
                }
                Ok(if changed {
                    Update::Quote
                } else {
                    Update::Unchanged
                })
            }
            Observation::Trade(v) => {
                if entry.asset == Asset::Option
                    && v.price != i64::MAX
                    && !matches!(v.action, b'C' | b'R')
                    && let Some(bank) = &self.option_statistics
                {
                    let mut bank = bank.lock().map_err(|_| Failure::OptionStatisticsWriter)?;
                    let stats = bank
                        .get_mut(&header.instrument)
                        .ok_or(Failure::Registration)?;
                    stats
                        .accumulate(v.size, header.event_ns, header.sequence, self.date)
                        .map_err(Failure::OptionStatistics)?;
                    entry.option_statistics = Some(*stats);
                }
                // Session replay input is processed before raw trade ordering,
                // matching current replay accumulation independently of routing.
                if entry.asset == Asset::Futures {
                    entry
                        .sessions
                        .trade(self.date, &v, header.flags & 2 != 0)
                        .map_err(Failure::Statistics)?;
                }
                // Initialization replay seeds aggregates only. It must not make
                // the latest live price appear current before replay completes.
                if header.flags & 2 != 0 {
                    if let Some(vwap) = entry.vwap.as_mut() {
                        if !entry.replay_started {
                            vwap.reset(entry.stream_epoch);
                            entry.replay_started = true;
                        }
                        vwap.replay(vwap_input(v)).map_err(Failure::Vwap)?;
                    }
                    return Ok(Update::Unchanged);
                }
                let changed = entry
                    .reader
                    .update_trade(tick(
                        header,
                        TradeValue {
                            price: v.price,
                            size: v.size,
                        },
                    ))
                    .map_err(Failure::Latest)?;
                if !changed {
                    return Ok(Update::Unchanged);
                }
                let next = entry
                    .ordinal
                    .checked_add(1)
                    .ok_or(Failure::ContinuityOverflow)?;
                let (accepted, price_changed) = entry.market.trade(
                    self.date,
                    tick(
                        header,
                        MarketTrade {
                            value: TradeValue {
                                price: v.price,
                                size: v.size,
                            },
                            action: normalization::action(v.action) as u8,
                            side: normalization::side(v.side) as u8,
                            conditions: normalization::conditions(header.flags, v.dbn_flags),
                            epoch: entry.stream_epoch,
                            ordinal: next,
                        },
                    ),
                );
                if accepted {
                    entry.ordinal = next;
                    if let Some(vwap) = entry.vwap.as_mut() {
                        vwap.live(vwap_input(v), entry.stream_epoch, next)
                            .map_err(Failure::Vwap)?;
                    }
                    Ok(Update::Trade { price_changed })
                } else {
                    Ok(Update::Unchanged)
                }
            }
            Observation::Statistic(v) => {
                if entry.asset != Asset::Futures {
                    if let Some(bank) = &self.option_statistics {
                        let mut bank = bank.lock().map_err(|_| Failure::OptionStatisticsWriter)?;
                        let stats = bank
                            .get_mut(&header.instrument)
                            .ok_or(Failure::Registration)?;
                        stats
                            .provider(&v, self.date)
                            .map_err(Failure::OptionStatistics)?;
                        entry.option_statistics = Some(*stats);
                    }
                    return Ok(Update::Unchanged);
                }
                let (changed, value) = entry
                    .sessions
                    .statistic(self.date, &v)
                    .map_err(Failure::Statistics)?;
                Ok(if changed && header.flags & 2 == 0 {
                    Update::Statistics(value)
                } else {
                    Update::Unchanged
                })
            }
            Observation::Order(_) => Ok(Update::Unchanged),
        }
    }
    /// Completes observed trade replay without manufacturing market prices.
    pub fn complete_replay(
        &mut self,
        generation: u128,
        instrument: u32,
        stream_epoch: u128,
    ) -> Result<Option<Snapshot>, Failure> {
        if !self.active || generation != self.generation {
            return Ok(None);
        }
        let Some(entry) = self.entries.get_mut(&instrument) else {
            return Ok(None);
        };
        if entry.asset != Asset::Futures {
            return Ok(None);
        }
        if stream_epoch == 0 {
            return Err(Failure::Registration);
        }
        entry.stream_epoch = stream_epoch;
        entry.ordinal = 0;
        entry.replay_started = false;
        if let Some(vwap) = entry.vwap.as_mut() {
            vwap.complete_replay(stream_epoch);
        }
        entry
            .sessions
            .complete_replay(self.date)
            .map(Some)
            .map_err(Failure::Statistics)
    }
    /// Immediate authoritative statistics read for an explicitly requested date.
    pub fn statistics(&self, instrument: u32, date: Date) -> Option<Snapshot> {
        if !self.active {
            return None;
        }
        self.entries
            .get(&instrument)
            .and_then(|e| e.sessions.read(date))
            .filter(Snapshot::has_any)
    }
    /// Completes option replay without publishing a synthetic futures trade or
    /// replacing official volume. The retained logical watermark survives reset.
    pub fn complete_option_replay(
        &mut self,
        generation: u128,
        instrument: u32,
    ) -> Result<(), Failure> {
        if !self.active || generation != self.generation {
            return Ok(());
        }
        if let Some(entry) = self.entries.get_mut(&instrument)
            && entry.asset == Asset::Option
            && let Some(bank) = &self.option_statistics
        {
            let mut bank = bank.lock().map_err(|_| Failure::OptionStatisticsWriter)?;
            let stats = bank.get_mut(&instrument).ok_or(Failure::Registration)?;
            stats.complete(self.date);
            entry.option_statistics = Some(*stats);
        }
        Ok(())
    }
    /// All usable replay statistics, preserving the source's retained date order.
    pub fn all_statistics(&self, instrument: u32) -> Vec<Snapshot> {
        if !self.active {
            return Vec::new();
        }
        self.entries
            .get(&instrument)
            .map_or_else(Vec::new, |e| e.sessions.read_all().collect())
    }
    /// Coherent current market-price publication, distinct from raw last prices.
    pub fn market_price(&self, instrument: u32) -> Option<MarketSnapshot> {
        if !self.active {
            return None;
        }
        self.entries
            .get(&instrument)
            .and_then(|e| e.market.snapshot())
    }
    /// Explicit authoritative VWAP source checkpoint; incomplete flags are never
    /// promoted to complete merely because the transport reconnected.
    pub fn vwap(&self, instrument: u32) -> Option<VwapCheckpoint> {
        if !self.active {
            return None;
        }
        self.entries
            .get(&instrument)
            .and_then(|e| e.vwap.as_ref())
            .map(Vwap::snapshot)
    }
    /// Retires all handles before a replacement attempt can become visible.
    pub fn retire(&mut self) -> Result<(), Failure> {
        self.active = false;
        self.latest.retire().map_err(Failure::Latest)
    }
}
impl<G> Drop for Book<G> {
    fn drop(&mut self) {
        self.latest.fence();
    }
}
fn tick<T>(h: Header, value: T) -> Tick<T> {
    Tick {
        sequence: i64::from(h.sequence),
        event_ticks: h.event_ns / 100,
        receive_ticks: h.received_ns / 100,
        value,
    }
}
fn vwap_input(v: super::records::Trade) -> VwapInput {
    VwapInput {
        price: v.price,
        size: i64::from(v.size),
        sequence: i64::from(v.header.sequence),
        at: v.header.event_ns / 100,
        action: normalization::action(v.action),
        conditions: normalization::conditions(v.header.flags, v.dbn_flags),
    }
}
