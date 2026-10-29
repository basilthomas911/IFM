//! Single-writer port of the current futures session accumulator. Prices stay in
//! exact 1e-9 wire units; no binary floating point enters official OHLC/volume.
//! ValueDate is supplied by the API. Only official cleared-volume records derive
//! a UTC reference date, matching the unchanged C# accumulator.
use crate::dataset::records::{Statistic, Trade};
use std::collections::BTreeMap;
use time::{Date, OffsetDateTime};
/// Session volume completeness, preserving the existing numeric identities.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
#[repr(u8)]
pub enum VolumeQuality {
    /// No complete observed volume.
    #[default]
    Unknown = 0,
    /// Initialization replay has not completed.
    Bootstrapping = 1,
    /// Full observed replay plus live continuation.
    ObservedComplete = 2,
    /// Provider cleared volume is authoritative and final.
    OfficialFinal = 3,
}
/// Exact native session statistics; the managed boundary supplies contract identity.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Snapshot {
    /// API-selected trading date, except official volume's UTC reference date.
    pub value_date: Date,
    /// Official open in 1e-9 price units, zero if unavailable.
    pub open: i64,
    /// Official high in the same units.
    pub high: i64,
    /// Official low in the same units.
    pub low: i64,
    /// Maximum trade/OHLC source sequence; not the cleared-volume sequence.
    pub sequence: u32,
    /// Last accepted changed record's source timestamp.
    pub event_ns: i64,
    /// Observed or final contract volume.
    pub volume: i64,
    /// Completeness of volume.
    pub quality: VolumeQuality,
}
impl Snapshot {
    /// Matches current OHLC validity, including open inside high/low.
    pub fn has_prices(&self) -> bool {
        self.open > 0
            && self.high > 0
            && self.low > 0
            && self.high >= self.low
            && self.open >= self.low
            && self.open <= self.high
    }
    /// Matches complete observed or official volume semantics.
    pub fn has_volume(&self) -> bool {
        self.volume >= 0
            && matches!(
                self.quality,
                VolumeQuality::ObservedComplete | VolumeQuality::OfficialFinal
            )
    }
    /// Partial OHLC alone is not a publicly usable snapshot in the baseline.
    pub fn has_any(&self) -> bool {
        self.has_prices() || self.has_volume()
    }
}
#[derive(Default)]
struct State {
    prices: [i64; 3],
    sequences: [u32; 3],
    trade: Option<u32>,
    volume: i64,
    quality: VolumeQuality,
    event_ns: i64,
}
impl State {
    fn snapshot(&self, value_date: Date) -> Snapshot {
        Snapshot {
            value_date,
            open: self.prices[0],
            low: self.prices[1],
            high: self.prices[2],
            sequence: self
                .trade
                .unwrap_or(0)
                .max(*self.sequences.iter().max().unwrap_or(&0)),
            event_ns: self.event_ns,
            volume: self.volume,
            quality: self.quality,
        }
    }
}
/// Explicit bounded-state failures. The owner must report processing errors.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Failure {
    /// Configured number of retained trading dates exceeded.
    Capacity,
    /// Volume cannot fit the published signed 64-bit contract.
    VolumeOverflow,
}
/// One contract's bounded date-indexed statistics, owned by its processing lane.
pub struct Sessions {
    dates: BTreeMap<Date, State>,
    order: Vec<Date>,
    capacity: usize,
}
impl Sessions {
    /// Creates a bounded owner; zero capacity is rejected before admission.
    pub fn new(capacity: usize) -> Result<Self, Failure> {
        if capacity == 0 {
            Err(Failure::Capacity)
        } else {
            Ok(Self {
                dates: BTreeMap::new(),
                order: Vec::new(),
                capacity,
            })
        }
    }
    fn state(&mut self, date: Date) -> Result<&mut State, Failure> {
        if !self.dates.contains_key(&date) && self.dates.len() >= self.capacity {
            return Err(Failure::Capacity);
        }
        if !self.dates.contains_key(&date) {
            self.order.push(date);
        }
        Ok(self.dates.entry(date).or_default())
    }
    /// Applies a trade in source-sequence order. Replay and live overlap is
    /// idempotent; official final volume rejects subsequent observed trades.
    pub fn trade(
        &mut self,
        date: Date,
        record: &Trade,
        replay: bool,
    ) -> Result<(bool, Snapshot), Failure> {
        let state = self.state(date)?;
        if state.quality == VolumeQuality::OfficialFinal
            || state.trade.is_some_and(|seq| record.header.sequence <= seq)
        {
            return Ok((false, state.snapshot(date)));
        }
        // Preserve baseline mutation order even when checked volume addition fails.
        state.trade = Some(record.header.sequence);
        state.volume = state
            .volume
            .checked_add(i64::from(record.size))
            .ok_or(Failure::VolumeOverflow)?;
        state.quality = if replay {
            VolumeQuality::Bootstrapping
        } else {
            VolumeQuality::ObservedComplete
        };
        state.event_ns = record.header.event_ns;
        Ok((true, state.snapshot(date)))
    }
    /// Completes initialization replay without overriding official final volume.
    pub fn complete_replay(&mut self, date: Date) -> Result<Snapshot, Failure> {
        let state = self.state(date)?;
        if state.quality != VolumeQuality::OfficialFinal {
            state.quality = VolumeQuality::ObservedComplete;
        }
        Ok(state.snapshot(date))
    }
    /// Applies official open/low/high or cleared volume. Sequence advances on an
    /// identical price even though changed=false, matching the C# TrySet behavior.
    pub fn statistic(
        &mut self,
        current_date: Date,
        record: &Statistic,
    ) -> Result<(bool, Snapshot), Failure> {
        let date = if record.kind == 6 && record.reference_ns > 0 {
            OffsetDateTime::from_unix_timestamp_nanos(i128::from(record.reference_ns))
                .map(|v| v.date())
                .unwrap_or(current_date)
        } else {
            current_date
        };
        let state = self.state(date)?;
        let before = state.snapshot(date);
        if record.action != 1 {
            return Ok((false, state.snapshot(date)));
        }
        let changed = if record.kind == 6 {
            if record.quantity < 0 || record.quantity == i64::MAX {
                return Ok((false, state.snapshot(date)));
            }
            let changed =
                state.volume != record.quantity || state.quality != VolumeQuality::OfficialFinal;
            state.volume = record.quantity;
            state.quality = VolumeQuality::OfficialFinal;
            changed
        } else {
            if record.header.flags & 4 != 0 || record.price <= 0 {
                return Ok((false, state.snapshot(date)));
            }
            let index = match record.kind {
                1 => 0,
                4 => 1,
                5 => 2,
                _ => return Ok((false, state.snapshot(date))),
            };
            if state.sequences[index] != 0 && record.header.sequence <= state.sequences[index] {
                false
            } else {
                state.sequences[index] = record.header.sequence;
                let changed = state.prices[index] != record.price;
                state.prices[index] = record.price;
                changed
            }
        };
        if !changed {
            // C# returns its pre-call out snapshot even when TrySet advanced
            // an internal sequence for an identical price.
            return Ok((false, before));
        }
        state.event_ns = record.header.event_ns;
        let snapshot = state.snapshot(date);
        Ok((snapshot.has_any(), snapshot))
    }
    /// Returns even an incomplete retained date; availability is has_any().
    pub fn read(&self, date: Date) -> Option<Snapshot> {
        self.dates.get(&date).map(|s| s.snapshot(date))
    }
    /// Usable replay snapshots in the current C# dictionary's insertion order,
    /// including prior reference dates. Incomplete OHLC/volume is not published.
    pub fn read_all(&self) -> impl Iterator<Item = Snapshot> + '_ {
        self.order
            .iter()
            .filter_map(|date| self.read(*date))
            .filter(Snapshot::has_any)
    }
    /// Drops all old session state on generation reset.
    pub fn reset(&mut self) {
        self.dates.clear();
        self.order.clear();
    }
}
