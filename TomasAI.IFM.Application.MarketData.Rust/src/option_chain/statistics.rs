//! Exact option-chain volume/open-interest state from OptionChainStateStore.
//! Provider replay updates statistics before price ordering, independently of
//! calculated Greeks. A physical reconnect can retain this small logical state.
use crate::dataset::records::Statistic;
use time::{Date, OffsetDateTime};
/// Logical option statistics retained across physical book replacements. The
/// containing LiveData/Book limits membership to its qualified contract capacity.
pub type Bank = std::sync::Arc<std::sync::Mutex<std::collections::BTreeMap<u32, Statistics>>>;

/// Checked accumulation failure. No partial watermark/state update is committed.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Failure {
    /// Deduplicated observed session volume exceeded signed Int64 capacity.
    VolumeOverflow,
    /// Provider reference timestamp could not identify a representable UTC date.
    ReferenceDate,
}
/// Saved application statistics, without inventing an official-volume flag or
/// watermark that was not persisted by the existing snapshot contract.
#[derive(Clone, Copy, Debug, Default)]
pub struct Seed {
    /// Optional saved volume.
    pub volume: Option<i64>,
    /// Trading date represented by saved volume.
    pub volume_date: Option<Date>,
    /// Optional saved open interest.
    pub open_interest: Option<i64>,
    /// Reference date represented by saved open interest.
    pub open_interest_date: Option<Date>,
}
/// Finite per-contract logical statistics. Timestamps are API UTC ticks, while
/// the trade deduplication watermark preserves complete provider nanoseconds.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Statistics {
    /// Displayable observed/seeded/official volume for VolumeDate.
    pub volume: Option<i64>,
    /// Latest accepted open interest.
    pub open_interest: Option<i64>,
    /// Latest received supported statistics event, including older reference days.
    pub statistics_at: Option<i64>,
    /// Provider officially cleared the displayed volume for its date.
    pub official: bool,
    /// Reference date represented by displayed volume.
    pub volume_date: Option<Date>,
    /// Reference date represented by open interest.
    pub open_interest_date: Option<Date>,
    /// Event time of the accepted official volume statistic.
    pub volume_at: Option<i64>,
    /// Event time of the accepted open-interest statistic.
    pub open_interest_at: Option<i64>,
    /// Deduplicated replay/live trade size accumulated by this logical owner.
    pub observed: i64,
    /// Last accepted volume trade timestamp, before nanosecond truncation.
    pub last_event_ns: i64,
    /// Source sequence at that exact timestamp.
    pub last_sequence: u32,
    /// All trading-session replay contributions have been seen.
    pub replay_complete: bool,
}
impl Default for Statistics {
    fn default() -> Self {
        Self {
            volume: None,
            open_interest: None,
            statistics_at: None,
            official: false,
            volume_date: None,
            open_interest_date: None,
            volume_at: None,
            open_interest_at: None,
            observed: 0,
            last_event_ns: i64::MIN,
            last_sequence: 0,
            replay_complete: false,
        }
    }
}
impl Statistics {
    /// Accumulates exactly newer timestamp/sequence contributions. During replay,
    /// a saved total is a lower bound until completion; official totals take
    /// precedence. Checked overflow leaves every field, including watermark, intact.
    pub fn accumulate(
        &mut self,
        size: u32,
        event_ns: i64,
        sequence: u32,
        value_date: Date,
    ) -> Result<bool, Failure> {
        if (event_ns, sequence) <= (self.last_event_ns, self.last_sequence) {
            return Ok(false);
        }
        let observed = self
            .observed
            .checked_add(i64::from(size))
            .ok_or(Failure::VolumeOverflow)?;
        let saved = if self.volume_date == Some(value_date) {
            self.volume.unwrap_or(0)
        } else {
            0
        };
        self.observed = observed;
        self.last_event_ns = event_ns;
        self.last_sequence = sequence;
        self.official = self.official && self.volume_date == Some(value_date);
        self.volume = Some(if self.official {
            saved
        } else if self.replay_complete {
            observed
        } else {
            saved.max(observed)
        });
        self.volume_date = Some(value_date);
        Ok(true)
    }
    /// Ends replay without overriding an official total for the current date.
    pub fn complete(&mut self, value_date: Date) {
        if !self.official || self.volume_date != Some(value_date) {
            self.volume = Some(self.observed)
        }
        self.official = self.official && self.volume_date == Some(value_date);
        self.volume_date = Some(value_date);
        self.replay_complete = true;
    }
    /// Applies only nonnegative saved observations at a strictly newer reference
    /// date, never a future date. Cache hits are not retimestamped or made official.
    pub fn seed(&mut self, seed: Seed, value_date: Date) {
        if let (Some(volume), Some(date)) = (seed.volume, seed.volume_date)
            && volume >= 0
            && date <= value_date
            && self.volume_date.is_none_or(|old| date > old)
        {
            self.volume = Some(volume);
            self.volume_date = Some(date);
            self.official = false;
        }
        if let (Some(interest), Some(date)) = (seed.open_interest, seed.open_interest_date)
            && interest >= 0
            && date <= value_date
            && self.open_interest_date.is_none_or(|old| date > old)
        {
            self.open_interest = Some(interest);
            self.open_interest_date = Some(date);
        }
    }
    /// Applies supported already-qualified statistics with date-first ordering
    /// and inclusive event-time ties. StatisticsAt advances independently from a
    /// stale reference date, exactly as in the existing store.
    pub fn update(
        &mut self,
        volume: Option<i64>,
        open_interest: Option<i64>,
        reference_date: Date,
        at: i64,
    ) {
        if let Some(volume) = volume
            && volume >= 0
            && newer(reference_date, at, self.volume_date, self.volume_at)
        {
            self.volume = Some(volume);
            self.volume_date = Some(reference_date);
            self.volume_at = Some(at);
            self.official = true;
        }
        if let Some(interest) = open_interest
            && interest >= 0
            && newer(
                reference_date,
                at,
                self.open_interest_date,
                self.open_interest_at,
            )
        {
            self.open_interest = Some(interest);
            self.open_interest_date = Some(reference_date);
            self.open_interest_at = Some(at);
        }
        if self.statistics_at.is_none_or(|old| at > old) {
            self.statistics_at = Some(at)
        }
    }
    /// Applies the current manager's exact provider filters: New action, finite
    /// nonnegative quantity, volume (6)/OI (9), and no future UTC reference date.
    /// CLR truncates nanoseconds toward zero before constructing its UTC date.
    pub fn provider(&mut self, record: &Statistic, value_date: Date) -> Result<bool, Failure> {
        if record.action != 1
            || record.quantity < 0
            || record.quantity == i64::MAX
            || !matches!(record.kind, 6 | 9)
        {
            return Ok(false);
        }
        let reference =
            OffsetDateTime::from_unix_timestamp_nanos(i128::from(record.reference_ns / 100) * 100)
                .map_err(|_| Failure::ReferenceDate)?
                .date();
        if reference > value_date {
            return Ok(false);
        }
        let before = *self;
        self.update(
            if record.kind == 6 {
                Some(record.quantity)
            } else {
                None
            },
            if record.kind == 9 {
                Some(record.quantity)
            } else {
                None
            },
            reference,
            record.header.event_ns / 100,
        );
        Ok(*self != before)
    }
}
fn newer(date: Date, at: i64, prior: Option<Date>, observed: Option<i64>) -> bool {
    prior.is_none_or(|old| date > old || date == old && observed.is_none_or(|old| at >= old))
}
