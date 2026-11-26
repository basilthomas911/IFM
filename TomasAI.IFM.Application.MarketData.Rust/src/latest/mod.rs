//! Bounded epoch-local latest prices. Short locks replace the managed seqlock's
//! unbounded read retry: no lock spans I/O, await, managed callbacks or pricing.
//! Readers retain an Arc to the exact registration, so retired handles never read
//! a replacement generation accidentally. All prices retain provider nanounits.
use std::{
    collections::HashMap,
    sync::{
        Arc, Mutex, RwLock,
        atomic::{AtomicBool, Ordering},
    },
};
use time::Date;
pub mod market;
/// Supported registration kind; a canonical contract cannot change kind in place.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Asset {
    /// Futures have raw trade/quote observations.
    Futures,
    /// Futures options additionally accept calculated Greeks.
    Option,
}
/// Order key and immutable source payload. Times are API ticks (100 ns), preserving
/// the managed comparison resolution rather than comparing discarded nanoseconds.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Tick<T> {
    /// Source sequence, ordered before timestamp.
    pub sequence: i64,
    /// Event timestamp in 100 ns ticks since Unix epoch.
    pub event_ticks: i64,
    /// Receive timestamp in the same representation.
    pub receive_ticks: i64,
    /// Exact observation fields.
    pub value: T,
}
/// Exact trade payload.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct TradeValue {
    /// Price in provider nanounits.
    pub price: i64,
    /// Contract quantity.
    pub size: u32,
}
/// Exact quote payload.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct QuoteValue {
    /// Bid in provider nanounits.
    pub bid: Option<i64>,
    /// Ask in provider nanounits.
    pub ask: Option<i64>,
    /// Bid contracts.
    pub bid_size: u32,
    /// Ask contracts.
    pub ask_size: u32,
    /// Bid order count.
    pub bid_count: u32,
    /// Ask order count.
    pub ask_count: u32,
    /// Local ingestion timestamp in API ticks.
    pub local_received_ticks: i64,
}
/// Immutable risk payload attached to one exact source observation.
#[derive(Debug)]
pub struct Priced<T, G> {
    /// Source observation used to calculate the risk.
    pub tick: Tick<T>,
    /// Full immutable risk/context payload; reads clone only the Arc.
    pub greeks: Arc<G>,
}
impl<T: Copy, G> Clone for Priced<T, G> {
    fn clone(&self) -> Self {
        Self {
            tick: self.tick,
            greeks: self.greeks.clone(),
        }
    }
}
/// Explicit registration/storage failures, never hidden as a healthy empty feed.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Failure {
    /// Empty contract, invalid date/epoch or zero capacity.
    InvalidInput,
    /// Fixed contract registration capacity is exhausted.
    Capacity,
    /// Same canonical contract registered with a different kind.
    AssetMismatch,
    /// Epoch was retired.
    Retired,
    /// Writer panicked while holding state; rebuild this epoch.
    InterruptedWriter,
}
struct Values<T, G> {
    raw: Option<Tick<T>>,
    priced: Option<Priced<T, G>>,
}
impl<T, G> Default for Values<T, G> {
    fn default() -> Self {
        Self {
            raw: None,
            priced: None,
        }
    }
}
fn order<T>(tick: &Tick<T>) -> (i64, i64) {
    (tick.sequence, tick.event_ticks)
}
impl<T: Copy, G> Values<T, G> {
    fn raw(&mut self, tick: Tick<T>) -> bool {
        if self
            .raw
            .as_ref()
            .is_some_and(|old| order(&tick) <= order(old))
        {
            return false;
        }
        self.raw = Some(tick);
        // Baseline retains same-sequence risk when only the timestamp advances.
        if self
            .priced
            .as_ref()
            .is_some_and(|old| old.tick.sequence != tick.sequence)
        {
            self.priced = None;
        }
        true
    }
    fn priced(&mut self, value: Priced<T, G>) -> bool {
        if self
            .raw
            .as_ref()
            .is_some_and(|old| order(&value.tick) < order(old))
            || self
                .priced
                .as_ref()
                .is_some_and(|old| order(&value.tick) <= order(&old.tick))
        {
            return false;
        }
        self.raw = Some(value.tick);
        self.priced = Some(value);
        true
    }
}
struct State<G> {
    active: bool,
    trade: Values<TradeValue, G>,
    quote: Values<QuoteValue, G>,
}
/// Stable reader/writer handle to one contract registration in one epoch.
pub struct Contract<G> {
    stopped: tokio_util::sync::CancellationToken,
    retired: Arc<AtomicBool>,
    generation: u128,
    asset: Asset,
    state: Mutex<State<G>>,
}
impl<G> Contract<G> {
    /// Immutable owner identity copied with hot observations across the ABI.
    pub fn generation(&self) -> u128 {
        self.generation
    }
    /// Updates raw trade by the existing sequence/timestamp ordering rules.
    pub fn update_trade(&self, tick: Tick<TradeValue>) -> Result<bool, Failure> {
        if self.stopped.is_cancelled() {
            return Ok(false);
        }
        let mut s = self.state.lock().map_err(|_| Failure::InterruptedWriter)?;
        Ok(!self.stopped.is_cancelled()
            && !self.retired.load(Ordering::Acquire)
            && s.active
            && s.trade.raw(tick))
    }
    /// Updates raw quote independently of raw trade.
    pub fn update_quote(&self, tick: Tick<QuoteValue>) -> Result<bool, Failure> {
        if self.stopped.is_cancelled() {
            return Ok(false);
        }
        let mut s = self.state.lock().map_err(|_| Failure::InterruptedWriter)?;
        Ok(!self.stopped.is_cancelled()
            && !self.retired.load(Ordering::Acquire)
            && s.active
            && s.quote.raw(tick))
    }
    /// Applies risk only to options and never behind the latest raw observation.
    pub fn update_trade_risk(&self, value: Priced<TradeValue, G>) -> Result<bool, Failure> {
        if self.stopped.is_cancelled() {
            return Ok(false);
        }
        let mut s = self.state.lock().map_err(|_| Failure::InterruptedWriter)?;
        Ok(!self.stopped.is_cancelled()
            && !self.retired.load(Ordering::Acquire)
            && s.active
            && self.asset == Asset::Option
            && s.trade.priced(value))
    }
    /// Applies quote risk with the same stale-calculation protection.
    pub fn update_quote_risk(&self, value: Priced<QuoteValue, G>) -> Result<bool, Failure> {
        if self.stopped.is_cancelled() {
            return Ok(false);
        }
        let mut s = self.state.lock().map_err(|_| Failure::InterruptedWriter)?;
        Ok(!self.stopped.is_cancelled()
            && !self.retired.load(Ordering::Acquire)
            && s.active
            && self.asset == Asset::Option
            && s.quote.priced(value))
    }
    /// Non-consuming current trade; retired handles return None.
    pub fn trade(&self) -> Result<Option<Tick<TradeValue>>, Failure> {
        if self.stopped.is_cancelled() {
            return Ok(None);
        }
        let s = self.state.lock().map_err(|_| Failure::InterruptedWriter)?;
        Ok(
            if !self.stopped.is_cancelled() && !self.retired.load(Ordering::Acquire) && s.active {
                s.trade.raw
            } else {
                None
            },
        )
    }
    /// Non-consuming current quote.
    pub fn quote(&self) -> Result<Option<Tick<QuoteValue>>, Failure> {
        if self.stopped.is_cancelled() {
            return Ok(None);
        }
        let s = self.state.lock().map_err(|_| Failure::InterruptedWriter)?;
        Ok(
            if !self.stopped.is_cancelled() && !self.retired.load(Ordering::Acquire) && s.active {
                s.quote.raw
            } else {
                None
            },
        )
    }
    /// Immutable risk read; no payload copy/allocation.
    pub fn trade_risk(&self) -> Result<Option<Priced<TradeValue, G>>, Failure> {
        if self.stopped.is_cancelled() {
            return Ok(None);
        }
        let s = self.state.lock().map_err(|_| Failure::InterruptedWriter)?;
        Ok(
            if !self.stopped.is_cancelled() && !self.retired.load(Ordering::Acquire) && s.active {
                s.trade.priced.clone()
            } else {
                None
            },
        )
    }
    /// Immutable quote risk read.
    pub fn quote_risk(&self) -> Result<Option<Priced<QuoteValue, G>>, Failure> {
        if self.stopped.is_cancelled() {
            return Ok(None);
        }
        let s = self.state.lock().map_err(|_| Failure::InterruptedWriter)?;
        Ok(
            if !self.stopped.is_cancelled() && !self.retired.load(Ordering::Acquire) && s.active {
                s.quote.priced.clone()
            } else {
                None
            },
        )
    }
    fn clear(&self, retire: bool) -> Result<(), Failure> {
        let (mut s, interrupted) = match self.state.lock() {
            Ok(state) => (state, false),
            // Retirement must still invalidate readers if a prior calculation
            // panicked. Ordinary reset never treats interrupted state as healthy.
            Err(poisoned) if retire => (poisoned.into_inner(), true),
            Err(_) => return Err(Failure::InterruptedWriter),
        };
        if retire {
            s.active = false;
        }
        s.trade = Values::default();
        s.quote = Values::default();
        if interrupted {
            Err(Failure::InterruptedWriter)
        } else {
            Ok(())
        }
    }
}
struct Registry<G> {
    active: bool,
    slots: HashMap<String, Arc<Contract<G>>>,
}
/// One explicitly dated and generated bounded latest-price registry.
pub struct Store<G> {
    stopped: tokio_util::sync::CancellationToken,
    retired: Arc<AtomicBool>,
    date: Date,
    generation: u128,
    capacity: usize,
    registry: RwLock<Registry<G>>,
}
impl<G> Store<G> {
    /// Creates an empty epoch. ValueDate is supplied by the API, never the clock.
    pub fn new(date: Date, generation: u128, capacity: usize) -> Result<Self, Failure> {
        Self::with_stop(
            date,
            generation,
            capacity,
            tokio_util::sync::CancellationToken::new(),
        )
    }
    /// Associates detached readers with an independently cancellable source lifetime.
    pub fn with_stop(
        date: Date,
        generation: u128,
        capacity: usize,
        stopped: tokio_util::sync::CancellationToken,
    ) -> Result<Self, Failure> {
        if date.year() < 1
            || date.year() > 9999
            || (date.year() == 1 && date.ordinal() == 1)
            || generation == 0
            || capacity == 0
        {
            return Err(Failure::InvalidInput);
        }
        Ok(Self {
            date,
            generation,
            capacity,
            stopped,
            retired: Arc::new(AtomicBool::new(false)),
            registry: RwLock::new(Registry {
                active: true,
                slots: HashMap::new(),
            }),
        })
    }
    /// Registers once or returns the same handle for an identical kind.
    pub fn register(&self, id: &str, asset: Asset) -> Result<Arc<Contract<G>>, Failure> {
        if id.trim().is_empty() {
            return Err(Failure::InvalidInput);
        }
        let mut r = self
            .registry
            .write()
            .map_err(|_| Failure::InterruptedWriter)?;
        if self.stopped.is_cancelled() || self.retired.load(Ordering::Acquire) || !r.active {
            return Err(Failure::Retired);
        }
        if let Some(slot) = r.slots.get(id) {
            return if slot.asset == asset {
                Ok(slot.clone())
            } else {
                Err(Failure::AssetMismatch)
            };
        }
        if r.slots.len() >= self.capacity {
            return Err(Failure::Capacity);
        }
        let slot = Arc::new(Contract {
            generation: self.generation,
            stopped: self.stopped.clone(),
            retired: self.retired.clone(),
            asset,
            state: Mutex::new(State {
                active: true,
                trade: Values::default(),
                quote: Values::default(),
            }),
        });
        r.slots.insert(id.into(), slot.clone());
        Ok(slot)
    }
    /// Resolves only this exact generation/date; stale publications cannot reach a replacement.
    pub fn lookup(
        &self,
        id: &str,
        date: Date,
        generation: u128,
    ) -> Result<Option<Arc<Contract<G>>>, Failure> {
        let r = self
            .registry
            .read()
            .map_err(|_| Failure::InterruptedWriter)?;
        Ok(
            if !self.stopped.is_cancelled()
                && !self.retired.load(Ordering::Acquire)
                && r.active
                && date == self.date
                && generation == self.generation
            {
                r.slots.get(id).cloned()
            } else {
                None
            },
        )
    }
    /// Clears live values while keeping registrations in the same epoch.
    pub fn reset(&self) -> Result<(), Failure> {
        let r = self
            .registry
            .write()
            .map_err(|_| Failure::InterruptedWriter)?;
        for slot in r.slots.values() {
            slot.clear(false)?;
        }
        Ok(())
    }
    /// Completes retirement before returning; retained reader handles stay retired.
    /// Invalidates every retained handle without acquiring any reader lock.
    /// Copied payload memory is released when its last reader drops.
    pub fn fence(&self) {
        self.retired.store(true, Ordering::Release);
    }

    /// Fences every retained handle before clearing copied payloads.
    pub fn retire(&self) -> Result<(), Failure> {
        self.fence();
        let (mut r, mut interrupted) = match self.registry.write() {
            Ok(registry) => (registry, false),
            Err(poisoned) => (poisoned.into_inner(), true),
        };
        r.active = false;
        for slot in r.slots.values() {
            // Invalidate every contract even if an earlier one reports an
            // interrupted writer; never leave later retained readers live.
            if slot.clear(true).is_err() {
                interrupted = true;
            }
        }
        if interrupted {
            Err(Failure::InterruptedWriter)
        } else {
            Ok(())
        }
    }
}

#[cfg(test)]
mod retirement_tests {
    use super::*;
    #[test]
    fn an_interrupted_contract_does_not_prevent_retiring_other_readers() {
        let date = Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
        let store = Store::<()>::new(date, 1, 2).unwrap();
        let broken = store.register("broken", Asset::Option).unwrap();
        let healthy = store.register("healthy", Asset::Futures).unwrap();
        healthy
            .update_trade(Tick {
                sequence: 1,
                event_ticks: 1,
                receive_ticks: 1,
                value: TradeValue { price: 1, size: 1 },
            })
            .unwrap();
        let interrupted = broken.clone();
        assert!(
            std::thread::spawn(move || {
                let _guard = interrupted.state.lock().unwrap();
                panic!("qualification writer interruption");
            })
            .join()
            .is_err()
        );
        assert_eq!(store.retire(), Err(Failure::InterruptedWriter));
        assert!(broken.trade().is_err());
        assert!(healthy.trade().unwrap().is_none());
        assert!(store.lookup("healthy", date, 1).unwrap().is_none());
    }
}

/// Native application mark/freshness policy over detached raw observations.
pub mod price_policy;
#[cfg(test)]
mod source_cancellation_tests {
    use super::*;
    #[test]
    fn cancelled_source_fences_retained_reader_without_waiting_for_its_writer() {
        let stop = tokio_util::sync::CancellationToken::new();
        let store = Store::<()>::with_stop(
            Date::from_calendar_date(2026, time::Month::October, 28).unwrap(),
            1,
            2,
            stop.clone(),
        )
        .unwrap();
        let reader = store.register("ES", Asset::Futures).unwrap();
        let writer = reader.state.lock().unwrap();
        stop.cancel();
        assert!(reader.trade().unwrap().is_none());
        assert!(reader.quote().unwrap().is_none());
        assert!(
            !reader
                .update_trade(Tick {
                    sequence: 1,
                    event_ticks: 1,
                    receive_ticks: 1,
                    value: TradeValue { price: 1, size: 1 }
                })
                .unwrap()
        );
        drop(writer);
    }
}

// Checkpoint codecs preserve raw source watermarks independently of priced observations.
pub(crate) fn checkpoint_tick<T>(tick: &Tick<T>, encode: impl Fn(&T)->rmpv::Value)->rmpv::Value {
    rmpv::Value::Array(vec![tick.sequence.into(),tick.event_ticks.into(),tick.receive_ticks.into(),encode(&tick.value)])
}
pub(crate) fn restore_tick<T>(value:&rmpv::Value, decode:impl Fn(&rmpv::Value)->Result<T,String>)->Result<Tick<T>,String>{
    let f=crate::checkpoint::fields(value,4)?;Ok(Tick{sequence:crate::checkpoint::number(&f[0])?,event_ticks:crate::checkpoint::number(&f[1])?,receive_ticks:crate::checkpoint::number(&f[2])?,value:decode(&f[3])?})
}
pub(crate) fn checkpoint_trade(value:&TradeValue)->rmpv::Value{rmpv::Value::Array(vec![value.price.into(),value.size.into()])}
pub(crate) fn restore_trade(value:&rmpv::Value)->Result<TradeValue,String>{let f=crate::checkpoint::fields(value,2)?;Ok(TradeValue{price:crate::checkpoint::number(&f[0])?,size:crate::checkpoint::unsigned(&f[1])?.try_into().map_err(|_|"Trade size overflow")?})}
pub(crate) fn checkpoint_quote(value:&QuoteValue)->rmpv::Value{use rmpv::Value as V;V::Array(vec![value.bid.map_or(V::Nil,Into::into),value.ask.map_or(V::Nil,Into::into),value.bid_size.into(),value.ask_size.into(),value.bid_count.into(),value.ask_count.into(),value.local_received_ticks.into()])}
pub(crate) fn restore_quote(value:&rmpv::Value)->Result<QuoteValue,String>{use crate::checkpoint as c;let f=c::fields(value,7)?;let optional=|v:&rmpv::Value|if v.is_nil(){Ok(None)}else{c::number(v).map(Some)};let small=|v:&rmpv::Value|->Result<u32,String>{c::unsigned(v)?.try_into().map_err(|_|"Quote count overflow".into())};Ok(QuoteValue{bid:optional(&f[0])?,ask:optional(&f[1])?,bid_size:small(&f[2])?,ask_size:small(&f[3])?,bid_count:small(&f[4])?,ask_count:small(&f[5])?,local_received_ticks:c::number(&f[6])?})}
impl Contract<crate::pricing::Values>{
    pub(crate) fn checkpoint(&self)->Result<rmpv::Value,String>{
        use rmpv::Value as V;let s=self.state.lock().map_err(|_|"Latest writer interrupted")?;
        fn values<T>(v:&Values<T,crate::pricing::Values>,encode:impl Fn(&T)->V)->V{
            let raw=v.raw.as_ref().map_or(V::Nil,|t|checkpoint_tick(t,&encode));
            let priced=v.priced.as_ref().map_or(V::Nil,|p|{let g=&p.greeks;V::Array(vec![checkpoint_tick(&p.tick,&encode),V::Array([g.price,g.volatility,g.delta,g.gamma,g.vega,g.theta,g.rho].iter().map(|v|V::from(v.to_bits())).collect())])});V::Array(vec![raw,priced])
        }
        Ok(V::Array(vec![1.into(),(s.active&&!self.stopped.is_cancelled()&&!self.retired.load(Ordering::Acquire)).into(),values(&s.trade,checkpoint_trade),values(&s.quote,checkpoint_quote)]))
    }
    pub(crate) fn restore_checkpoint(&self,value:&rmpv::Value)->Result<(),String>{
        use crate::checkpoint as c;let f=c::fields(value,4)?;if c::unsigned(&f[0])?!=1{return Err("Latest checkpoint version".into());}
        fn values<T>(v:&rmpv::Value,decode:impl Fn(&rmpv::Value)->Result<T,String>)->Result<Values<T,crate::pricing::Values>,String>{
            use crate::checkpoint as c;let f=c::fields(v,2)?;let raw=if f[0].is_nil(){None}else{Some(restore_tick(&f[0],&decode)?)};
            let priced=if f[1].is_nil(){None}else{let p=c::fields(&f[1],2)?;let g=c::fields(&p[1],7)?;let bits=g.iter().map(|v|c::unsigned(v).map(f64::from_bits)).collect::<Result<Vec<_>,_>>()?;Some(Priced{tick:restore_tick(&p[0],decode)?,greeks:Arc::new(crate::pricing::Values{price:bits[0],volatility:bits[1],delta:bits[2],gamma:bits[3],vega:bits[4],theta:bits[5],rho:bits[6]})})};Ok(Values{raw,priced})
        }
        let state=State{active:c::boolean(&f[1])?,trade:values(&f[2],restore_trade)?,quote:values(&f[3],restore_quote)?};
        *self.state.lock().map_err(|_|"Latest writer interrupted")?=state;Ok(())
    }
}
