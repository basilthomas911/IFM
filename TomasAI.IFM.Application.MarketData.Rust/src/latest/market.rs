//! Current market-price publication cache, distinct from raw last observations.
//! Every accepted trade advances continuity; publication price changes only at
//! the configured tick threshold. Quotes preserve the last published trade.
use super::{QuoteValue, Tick, TradeValue, order};
use time::Date;
/// Normalized trade payload and application continuity evidence.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Trade {
    /// Raw price/size.
    pub value: TradeValue,
    /// Existing normalized action ID.
    pub action: u8,
    /// Existing normalized side ID.
    pub side: u8,
    /// Existing normalized condition bits.
    pub conditions: u16,
    /// Application stream continuity epoch.
    pub epoch: u128,
    /// Consecutive accepted live trade ordinal.
    pub ordinal: i64,
}
/// Coherent provider-neutral price, with original independent quote/trade times.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Snapshot {
    /// API ValueDate, never derived from the observation timestamp.
    pub date: Date,
    /// Last accepted quote.
    pub quote: Option<Tick<QuoteValue>>,
    /// Last published trade price; continuity may have advanced further.
    pub trade: Option<Tick<Trade>>,
}
/// One single-writer price cache. The enclosing hot reader supplies synchronization.
#[derive(Default)]
pub struct Market {
    current: Option<Snapshot>,
    last_accepted: Option<Tick<Trade>>,
    tick_size: Option<i64>,
}
impl Market {
    /// Sets exact provider tick size; absent/nonpositive preserves identical-price
    /// suppression, as in the unchanged current cache.
    pub fn new(tick_size: Option<i64>) -> Self {
        Self {
            tick_size,
            ..Default::default()
        }
    }
    /// Accepts newer source quote and returns whether a new snapshot was written.
    pub fn quote(&mut self, date: Date, quote: Tick<QuoteValue>) -> bool {
        if self
            .current
            .is_some_and(|s| s.date == date && s.quote.is_some_and(|q| order(&quote) <= order(&q)))
        {
            return false;
        }
        let trade = self
            .current
            .filter(|s| s.date == date)
            .and_then(|s| s.trade);
        self.current = Some(Snapshot {
            date,
            quote: Some(quote),
            trade,
        });
        true
    }
    /// Returns (source accepted, publication price changed). A same-price accepted
    /// trade deliberately preserves the previous published timestamp/ordinal.
    pub fn trade(&mut self, date: Date, trade: Tick<Trade>) -> (bool, bool) {
        if self.current.is_some_and(|s| s.date == date)
            && self
                .last_accepted
                .is_some_and(|old| order(&trade) <= order(&old))
        {
            return (false, false);
        }
        self.last_accepted = Some(trade);
        if let Some(s) = self.current
            && s.date == date
            && let Some(old) = s.trade
        {
            let difference =
                (i128::from(trade.value.value.price) - i128::from(old.value.value.price)).abs();
            if if let Some(tick) = self.tick_size.filter(|t| *t > 0) {
                difference < i128::from(tick)
            } else {
                difference == 0
            } {
                return (true, false);
            }
        }
        let quote = self
            .current
            .filter(|s| s.date == date)
            .and_then(|s| s.quote);
        self.current = Some(Snapshot {
            date,
            quote,
            trade: Some(trade),
        });
        (true, true)
    }
    /// Reads the immutable current snapshot without altering source continuity.
    pub fn snapshot(&self) -> Option<Snapshot> {
        self.current
    }
    /// Captures every accepted execution for the ordered bar stream. Display
    /// suppression must never remove executions or their volume/continuity.
    pub fn execution_snapshot(&self) -> Option<Snapshot> {
        self.current.map(|mut snapshot| {
            snapshot.trade = self.last_accepted;
            snapshot
        })
    }
    /// Clears both publication and accepted-source state at explicit epoch reset.
    pub fn reset(&mut self) {
        self.current = None;
        self.last_accepted = None;
    }
}

impl Market {
    pub(crate) fn checkpoint(&self)->rmpv::Value{
        use rmpv::Value as V;let trade=|v:&Trade|V::Array(vec![super::checkpoint_trade(&v.value),v.action.into(),v.side.into(),v.conditions.into(),crate::checkpoint::wide(v.epoch),v.ordinal.into()]);
        let current=self.current.map_or(V::Nil,|s|V::Array(vec![crate::checkpoint::day(s.date),s.quote.as_ref().map_or(V::Nil,|v|super::checkpoint_tick(v,super::checkpoint_quote)),s.trade.as_ref().map_or(V::Nil,|v|super::checkpoint_tick(v,trade))]));
        V::Array(vec![1.into(),current,self.last_accepted.as_ref().map_or(V::Nil,|v|super::checkpoint_tick(v,trade)),self.tick_size.map_or(V::Nil,Into::into)])
    }
    pub(crate) fn restore_checkpoint(value:&rmpv::Value)->Result<Self,String>{
        use crate::checkpoint as c;let f=c::fields(value,4)?;if c::unsigned(&f[0])?!=1{return Err("Market checkpoint version".into());}
        let trade=|v:&rmpv::Value|->Result<Trade,String>{let p=c::fields(v,6)?;Ok(Trade{value:super::restore_trade(&p[0])?,action:c::unsigned(&p[1])?.try_into().map_err(|_|"Market action overflow")?,side:c::unsigned(&p[2])?.try_into().map_err(|_|"Market side overflow")?,conditions:c::unsigned(&p[3])?.try_into().map_err(|_|"Market conditions overflow")?,epoch:c::read_wide(&p[4])?,ordinal:c::number(&p[5])?})};
        let current=if f[1].is_nil(){None}else{let s=c::fields(&f[1],3)?;Some(Snapshot{date:c::date(&s[0])?,quote:if s[1].is_nil(){None}else{Some(super::restore_tick(&s[1],super::restore_quote)?)},trade:if s[2].is_nil(){None}else{Some(super::restore_tick(&s[2],trade)?)}})};
        Ok(Self{current,last_accepted:if f[2].is_nil(){None}else{Some(super::restore_tick(&f[2],trade)?)},tick_size:if f[3].is_nil(){None}else{Some(c::number(&f[3])?)}})
    }
}
