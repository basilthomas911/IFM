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
