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

pub(crate) fn statistics_checkpoint(s:&crate::option_chain::statistics::Statistics)->rmpv::Value{
    use rmpv::Value as V;V::Array(vec![s.volume.map_or(V::Nil,Into::into),s.open_interest.map_or(V::Nil,Into::into),s.statistics_at.map_or(V::Nil,Into::into),s.official.into(),s.volume_date.map_or(V::Nil,crate::checkpoint::day),s.open_interest_date.map_or(V::Nil,crate::checkpoint::day),s.volume_at.map_or(V::Nil,Into::into),s.open_interest_at.map_or(V::Nil,Into::into),s.observed.into(),s.last_event_ns.into(),s.last_sequence.into(),s.replay_complete.into()])
}
pub(crate) fn restore_statistics(v:&rmpv::Value)->Result<crate::option_chain::statistics::Statistics,String>{
    use crate::checkpoint as c;let f=c::fields(v,12)?;let n=|v:&rmpv::Value|if v.is_nil(){Ok(None)}else{c::number(v).map(Some)};let d=|v:&rmpv::Value|if v.is_nil(){Ok(None)}else{c::date(v).map(Some)};
    Ok(crate::option_chain::statistics::Statistics{volume:n(&f[0])?,open_interest:n(&f[1])?,statistics_at:n(&f[2])?,official:c::boolean(&f[3])?,volume_date:d(&f[4])?,open_interest_date:d(&f[5])?,volume_at:n(&f[6])?,open_interest_at:n(&f[7])?,observed:c::number(&f[8])?,last_event_ns:c::number(&f[9])?,last_sequence:c::unsigned(&f[10])?.try_into().map_err(|_|"Statistics sequence overflow")?,replay_complete:c::boolean(&f[11])?})
}
impl Book<crate::pricing::Values>{
    /// Captures exact private latest/book/session/VWAP state under the enclosing source freeze.
    pub(crate) fn checkpoint(&self)->Result<rmpv::Value,String>{
        use rmpv::Value as V;
        let entries=self.entries.iter().map(|(instrument,e)|Ok(V::Array(vec![(*instrument).into(),e.id.clone().into(),(if e.asset==Asset::Futures{1}else{2}).into(),e.reader.checkpoint()?,e.sessions.checkpoint(),e.market.checkpoint(),e.ordinal.into(),crate::checkpoint::wide(e.stream_epoch),e.vwap.as_ref().map_or(V::Nil,Vwap::checkpoint_value),e.replay_started.into(),e.option_statistics.as_ref().map_or(V::Nil,statistics_checkpoint)]))).collect::<Result<Vec<_>,String>>()?;
        let bank=match &self.option_statistics{None=>V::Nil,Some(bank)=>V::Array(bank.lock().map_err(|_|"Statistics writer interrupted")?.iter().map(|(id,s)|V::Array(vec![(*id).into(),statistics_checkpoint(s)])).collect())};
        Ok(V::Array(vec![1.into(),self.active.into(),crate::checkpoint::day(self.date),crate::checkpoint::wide(self.generation),(self.capacity as u64).into(),V::Array(entries),bank]))
    }
    /// Rebuilds new detached reader handles from complete original private state, without source publication.
    pub(crate) fn restore_checkpoint(value:&rmpv::Value,stop:tokio_util::sync::CancellationToken)->Result<Self,String>{
        use crate::checkpoint as c;let f=c::fields(value,7)?;
        let capacity=usize::try_from(c::unsigned(&f[4])?).map_err(|_|"Book capacity overflow")?;
        if c::unsigned(&f[0])?!=1||capacity==0||capacity>65536{return Err("Book checkpoint version/capacity invalid".into());}
        let rows=f[5].as_array().filter(|v|v.len()<=capacity).ok_or("Book entries exceed bounds")?;
        let mut book=Self::with_stop(c::date(&f[2])?,c::read_wide(&f[3])?,capacity,stop).map_err(|_|"Book owner identity invalid")?;
        for row in rows{
            let e=c::fields(row,11)?;let instrument=c::unsigned(&e[0])?.try_into().map_err(|_|"Book instrument overflow")?;
            let id=c::string(&e[1],256)?;let asset=match c::unsigned(&e[2])?{1=>Asset::Futures,2=>Asset::Option,_=>return Err("Book asset unknown".into())};
            if instrument==0||id.trim().is_empty()||book.entries.contains_key(&instrument)||!book.ids.insert(id.clone()){return Err("Duplicate/invalid book registration".into());}
            let reader=book.latest.register(&id,asset).map_err(|_|"Book latest registration invalid")?;reader.restore_checkpoint(&e[3])?;
            let vwap=if e[8].is_nil(){None}else{Some(Vwap::restore_checkpoint(&e[8])?)};
            if asset==Asset::Option&&vwap.is_some(){return Err("Option VWAP policy invalid".into());}
            book.entries.insert(instrument,Entry{id,asset,reader,sessions:Sessions::restore_checkpoint(&e[4])?,market:Market::restore_checkpoint(&e[5])?,ordinal:c::number(&e[6])?,stream_epoch:c::read_wide(&e[7])?,vwap,replay_started:c::boolean(&e[9])?,option_statistics:if e[10].is_nil(){None}else{Some(restore_statistics(&e[10])?)}});
        }
        if !f[6].is_nil(){let mut bank=BTreeMap::new();for row in f[6].as_array().filter(|v|v.len()<=capacity).ok_or("Statistics bank exceeds bound")?{let s=c::fields(row,2)?;let id=c::unsigned(&s[0])?.try_into().map_err(|_|"Bank instrument overflow")?;if !book.entries.get(&id).is_some_and(|e|e.asset==Asset::Option)||bank.insert(id,restore_statistics(&s[1])?).is_some(){return Err("Invalid statistics bank registration".into());}}book.option_statistics=Some(Arc::new(std::sync::Mutex::new(bank)));}
        book.active=c::boolean(&f[1])?;if !book.active{book.latest.retire().map_err(|_|"Book retirement failed")?;}Ok(book)
    }
}

#[cfg(test)]
mod checkpoint_tests {
    use super::*;
    #[test]
    fn checkpoint_restores_exact_private_book_and_continuation(){
        let date=Date::from_calendar_date(2026,time::Month::October,1).unwrap();
        let mut book=Book::<crate::pricing::Values>::new(date,17,4).unwrap();
        let future=book.register_with_policy(1,"ESZ6",Asset::Futures,3,Some(250_000_000),true).unwrap();
        let option=book.register(2,"ESZ6C6000",Asset::Option,3).unwrap();
        let trade=Tick{sequence:81,event_ticks:900,receive_ticks:901,value:TradeValue{price:6_000_000_000_000,size:3}};
        future.update_trade(trade).unwrap();
        let quote=Tick{sequence:82,event_ticks:902,receive_ticks:903,value:QuoteValue{bid:None,ask:None,bid_size:0,ask_size:0,bid_count:0,ask_count:0,local_received_ticks:904}};
        option.update_quote(quote).unwrap();
        option.update_quote_risk(crate::latest::Priced{tick:quote,greeks:Arc::new(crate::pricing::Values{price:1.2,volatility:0.25,delta:-0.0,gamma:0.003,vega:4.0,theta:-3.0,rho:0.1})}).unwrap();
        let entry=book.entries.get_mut(&1).unwrap();entry.ordinal=47;entry.stream_epoch=18;entry.replay_started=true;
        let market_trade=Tick{sequence:80,event_ticks:890,receive_ticks:891,value:MarketTrade{value:trade.value,action:2,side:1,conditions:7,epoch:18,ordinal:46}};
        entry.market.trade(date,market_trade);let mut next=market_trade;next.sequence=81;next.value.ordinal=47;next.value.value.price+=1;entry.market.trade(date,next);
        let mut stats=crate::option_chain::statistics::Statistics::default();stats.accumulate(5,50,6,date).unwrap();
        let bank=Arc::new(std::sync::Mutex::new(BTreeMap::from([(2,stats)])));book.bind_option_statistics(bank).unwrap();
        let encoded=book.checkpoint().unwrap();let bytes=crate::checkpoint::encode(&encoded).unwrap();
        let mut restored=Book::<crate::pricing::Values>::restore_checkpoint(&crate::checkpoint::decode(&bytes).unwrap(),tokio_util::sync::CancellationToken::new()).unwrap();
        assert_eq!(encoded,restored.checkpoint().unwrap());assert_eq!(restored.market_price(1).unwrap().trade.unwrap().sequence,80);
        assert_eq!(restored.entries.get(&1).unwrap().market.execution_snapshot().unwrap().trade.unwrap().sequence,81);
        assert!(!restored.reader("ESZ6").unwrap().unwrap().update_trade(trade).unwrap());
        let mut newer=trade;newer.sequence+=1;assert!(future.update_trade(newer).unwrap());assert!(restored.reader("ESZ6").unwrap().unwrap().update_trade(newer).unwrap());
        assert_eq!(book.checkpoint().unwrap(),restored.checkpoint().unwrap());
        restored.retire().unwrap();let retired=restored.checkpoint().unwrap();let cold=Book::<crate::pricing::Values>::restore_checkpoint(&retired,tokio_util::sync::CancellationToken::new()).unwrap();assert_eq!(retired,cold.checkpoint().unwrap());assert!(cold.reader("ESZ6").unwrap().is_none());
    }
    #[test]
    fn checkpoint_cannot_resurrect_cancelled_latest_readers(){
        let date=Date::from_calendar_date(2026,time::Month::October,1).unwrap();let stop=tokio_util::sync::CancellationToken::new();let mut book=Book::<crate::pricing::Values>::with_stop(date,17,1,stop.clone()).unwrap();let reader=book.register(1,"ESZ6",Asset::Futures,2).unwrap();reader.update_trade(Tick{sequence:1,event_ticks:2,receive_ticks:3,value:TradeValue{price:5000,size:1}}).unwrap();stop.cancel();assert!(reader.trade().unwrap().is_none());let image=book.checkpoint().unwrap();let restored=Book::<crate::pricing::Values>::restore_checkpoint(&image,tokio_util::sync::CancellationToken::new()).unwrap();assert!(restored.raw_trade(1).unwrap().is_none());assert_eq!(image,restored.checkpoint().unwrap());
    }
    #[test]
    fn malformed_book_checkpoint_is_rejected_before_exposure(){
        let date=Date::from_calendar_date(2026,time::Month::October,1).unwrap();let mut book=Book::<crate::pricing::Values>::new(date,17,4).unwrap();book.register(1,"ESZ6",Asset::Futures,2).unwrap();let mut value=book.checkpoint().unwrap();
        let duplicate=value.as_array().unwrap()[5].as_array().unwrap()[0].clone();if let rmpv::Value::Array(fields)=&mut value {if let rmpv::Value::Array(rows)=&mut fields[5]{rows.push(duplicate);}}
        assert!(Book::<crate::pricing::Values>::restore_checkpoint(&value,tokio_util::sync::CancellationToken::new()).is_err());
    }
}

impl Book<crate::pricing::Values>{
    /// Reattaches the restored logical statistics authority without overwriting a divergent owner.
    pub(crate) fn rebind_checkpoint_statistics(&mut self,bank:crate::option_chain::statistics::Bank)->Result<(),String>{
        let Some(restored)=self.option_statistics.as_ref() else {return Err("Checkpoint has no logical option-statistics authority".into());};
        if Arc::ptr_eq(restored,&bank){return Ok(());}
        let snapshot=restored.lock().map_err(|_|"Restored statistics writer interrupted")?.clone();
        {let mut target=bank.lock().map_err(|_|"Original statistics writer interrupted")?;if !target.is_empty()&&*target!=snapshot{return Err("Original logical statistics differs from checkpoint".into());}*target=snapshot;}
        self.option_statistics=Some(bank);Ok(())
    }
}
