//! Bounded native latest-value catch-up, detached from live dataset resources.
//! Queries copy frames. Retaining a query result can never retain a socket,
//! native lease, task, hot reader or provider generation owner.

use super::wire::Frame;
use std::{collections::BTreeMap, sync::Mutex};

struct State {
    frames: BTreeMap<(String, u8), Frame>,
    bytes: usize,
}
/// Checked latest-event storage, independent of broker/persistence availability.
pub struct Current {
    maximum_contracts: usize,
    maximum_bytes: usize,
    state: Mutex<State>,
}
impl Current {
    /// Creates explicit item/byte limits before accepting any event allocation.
    ///
    /// # Errors
    /// Rejects zero/unbounded limits; futures use 128 and 16 MiB as today.
    pub fn new(maximum_contracts: usize, maximum_bytes: usize) -> Result<Self, &'static str> {
        if !(1..=4096).contains(&maximum_contracts)
            || !(1..=64 * 1024 * 1024).contains(&maximum_bytes)
        {
            return Err("Invalid retained publication capacity");
        }
        Ok(Self {
            maximum_contracts,
            maximum_bytes,
            state: Mutex::new(State {
                frames: BTreeMap::new(),
                bytes: 0,
            }),
        })
    }
    /// Stores one complete copied frame after producer identity validation.
    /// Retention is latest per contract/kind, not an event replay backlog.
    ///
    /// # Errors
    /// Capacity, stale sequence and interrupted state fail without partial write.
    pub fn retain(&self, contract: &str, frame: &Frame) -> Result<(), &'static str> {
        frame.validate()?;
        if contract.is_empty() || contract.len() > 1024 {
            return Err("Invalid retained contract");
        }
        if frame.kind == 4 {
            // Official cleared-volume updates can refer to yesterday. They still
            // stream to their dated consumers, but cannot replace today's catch-up
            // slot. The active epoch's API ValueDate is the only selection authority.
            let mut input = std::io::Cursor::new(&frame.payload);
            let event = rmpv::decode::read_value_with_max_depth(&mut input, 16)
                .map_err(|_| "Invalid retained session statistics")?;
            let fields = event.as_array().filter(|v| v.len() == 9)
                .ok_or("Invalid retained session statistics")?;
            let statistics = fields[8].as_array().filter(|v| v.len() == 9)
                .ok_or("Invalid retained session statistics snapshot")?;
            if input.position() != frame.payload.len() as u64
                || statistics[0].as_str() != Some(contract)
            {
                return Err("Invalid retained session statistics identity");
            }
            let date = statistics[1].as_i64().filter(|n| (0..=3_652_058).contains(n))
                .ok_or("Invalid retained session statistics ValueDate")?;
            if date != i64::from(frame.value_date) { return Ok(()); }
        }
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Retained publication writer interrupted")?;
        let key = (contract.to_owned(), frame.kind);
        let previous = state.frames.get(&key);
        if previous.is_none() && state.frames.len() >= self.maximum_contracts {
            return Err("Retained publication item capacity exceeded");
        }
        if previous.is_some_and(|old| {
            old.worker != frame.worker
                || old.generation != frame.generation
                || old.value_date != frame.value_date
                || old.manifest_revision > frame.manifest_revision
                || old.sequence >= frame.sequence
        }) {
            return Err("Stale retained publication; replacement requires retirement");
        }
        let bytes = state.bytes - previous.map_or(0, |old| old.payload.len()) + frame.payload.len();
        if bytes > self.maximum_bytes {
            return Err("Retained publication byte capacity exceeded");
        }
        state.frames.insert(key, frame.clone());
        state.bytes = bytes;
        Ok(())
    }
    /// Copies one retained contract/kind without exposing its lock or resources.
    /// Missing or retired values are absent; this query never consumes a frame.
    ///
    /// # Errors
    /// Invalid identity or an interrupted retained-data writer.
    pub fn latest(&self, contract: &str, kind: u8) -> Result<Option<Frame>, &'static str> {
        if contract.is_empty() || contract.len() > 1024 {
            return Err("Invalid retained contract");
        }
        let state = self
            .state
            .lock()
            .map_err(|_| "Retained publication writer interrupted")?;
        Ok(state.frames.get(&(contract.to_owned(), kind)).cloned())
    }
    /// Copies one complete bounded latest-value set for explicit startup catch-up.
    /// An oversized complete set is an error, never a silently truncated snapshot.
    ///
    /// # Errors
    /// Invalid byte limit, interrupted state or complete result capacity overflow.
    pub fn capture_all(&self, maximum_bytes: usize) -> Result<Vec<Frame>, &'static str> {
        if maximum_bytes == 0 || maximum_bytes > 64 * 1024 * 1024 {
            return Err("Invalid complete publication capacity");
        }
        let state = self
            .state
            .lock()
            .map_err(|_| "Retained publication writer interrupted")?;
        let bytes = state
            .bytes
            .checked_add(state.frames.len() * 256)
            .ok_or("Complete publication size overflow")?;
        if bytes > maximum_bytes {
            return Err("Complete current publication capacity exceeded");
        }
        let mut frames = state.frames.values().cloned().collect::<Vec<_>>();
        frames.sort_by_key(|f| f.sequence);
        Ok(frames)
    }
    /// Copies only the requested event contract under the complete-result bound.
    /// A quote catch-up caller must not receive durable evidence with another DTO.
    /// Returned copies own no dataset resource; nonmatching frames remain retained.
    ///
    /// # Errors
    /// Invalid kind/byte bound, interrupted writer or complete result overflow.
    pub fn capture_kind(&self, kind: u8, maximum_bytes: usize) -> Result<Vec<Frame>, &'static str> {
        if !(1..=7).contains(&kind) || maximum_bytes == 0 || maximum_bytes > 64 * 1024 * 1024 {
            return Err("Invalid complete publication contract query");
        }
        let state = self
            .state
            .lock()
            .map_err(|_| "Retained publication writer interrupted")?;
        let mut bytes = 0usize;
        for frame in state.frames.values().filter(|frame| frame.kind == kind) {
            bytes = bytes
                .checked_add(frame.payload.len())
                .and_then(|n| n.checked_add(256))
                .ok_or("Complete publication size overflow")?;
            if bytes > maximum_bytes {
                return Err("Complete current publication capacity exceeded");
            }
        }
        let mut frames = state
            .frames
            .values()
            .filter(|frame| frame.kind == kind)
            .cloned()
            .collect::<Vec<_>>();
        frames.sort_by_key(|frame| frame.sequence);
        Ok(frames)
    }
    /// Copies a bounded sequence-ordered page for an explicit snapshot query.
    /// This does not consume data or schedule a managed streaming receiver.
    ///
    /// # Errors
    /// Rejects oversized query pages or interrupted ownership.
    pub fn capture(&self, after: i64, maximum: usize) -> Result<Vec<Frame>, &'static str> {
        self.capture_page(after, maximum, 4 * 1024 * 1024)
    }
    /// Copies a page with both an item limit and an encoded-byte bound. A
    /// conservative 256-byte allowance per envelope covers its fixed identities,
    /// numeric keys and binary framing before cloning any event payload.
    ///
    /// # Errors
    /// Invalid bounds, an undersized first-frame budget or interrupted state fail
    /// explicitly, rather than returning an empty non-progressing continuation.
    pub fn capture_page(
        &self,
        after: i64,
        maximum: usize,
        maximum_bytes: usize,
    ) -> Result<Vec<Frame>, &'static str> {
        if maximum == 0 || maximum > 256 || maximum_bytes == 0 || maximum_bytes > 4 * 1024 * 1024 {
            return Err("Invalid retained publication query capacity");
        }
        let state = self
            .state
            .lock()
            .map_err(|_| "Retained publication writer interrupted")?;
        let mut frames: Vec<_> = state
            .frames
            .values()
            .filter(|frame| frame.sequence > after)
            .collect();
        frames.sort_by_key(|frame| frame.sequence);
        let mut page = Vec::new();
        let mut bytes = 5usize;
        for frame in frames.into_iter().take(maximum) {
            let next = bytes
                .checked_add(frame.payload.len() + 256)
                .ok_or("Retained query size overflow")?;
            if next > maximum_bytes {
                if page.is_empty() {
                    return Err("Retained query cannot fit its first frame");
                }
                break;
            }
            bytes = next;
            page.push(frame.clone());
        }
        Ok(page)
    }
    /// Releases obsolete latest values at joined native owner replacement.
    /// Existing detached query copies remain valid but carry their old identity.
    ///
    /// # Errors
    /// An interrupted writer is an explicit unhealthy condition.
    pub fn retire(&self) -> Result<(), &'static str> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Retained publication writer interrupted")?;
        state.frames.clear();
        state.bytes = 0;
        Ok(())
    }
    /// Reports exact retained event payload bytes (excluding bounded map overhead).
    ///
    /// # Errors
    /// Reports an interrupted state lock.
    pub fn bytes(&self) -> Result<usize, &'static str> {
        Ok(self
            .state
            .lock()
            .map_err(|_| "Retained publication writer interrupted")?
            .bytes)
    }
}
