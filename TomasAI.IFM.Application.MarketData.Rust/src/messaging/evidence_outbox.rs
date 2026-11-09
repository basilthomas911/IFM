//! Bounded detached evidence handoff. External persistence works on immutable
//! bytes and non-owning receipts; it cannot retain a dataset connection or make
//! native cancellation wait for its acknowledgment.
use super::option_evidence::Evidence;
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
};
use tokio::sync::watch;

/// Immutable first attempt retained for external durable persistence and later
/// actor publication. No native feed or pricing resource is referenced here.
pub struct Record {
    /// Canonical contract copied with evidence; never a subscription handle.
    pub contract: String,
    /// Original source ValueDate as CLR day number, never a later session date.
    pub value_date: i32,
    /// Original logical source generation; replacement cannot relabel evidence.
    pub generation: [u8; 16],
    /// Stable economic source identity used by the existing database key.
    pub identity: String,
    /// Immutable economic-content digest used by the existing conflict check.
    pub digest: String,
    /// Original complete evidence bytes, preserved through duplicate submissions.
    pub bytes: Arc<[u8]>,
    /// Handoff identity, independent of dataset generations and resource handles.
    pub receipt: u64,
    /// Monotonic first submission time; retry cannot extend recovery allowance.
    pub submitted: tokio::time::Instant,
}
struct Item {
    record: Arc<Record>,
    persisted: bool,
}
struct State {
    items: BTreeMap<String, Item>,
    bytes: usize,
    next: u64,
}
/// Global bounded copied-data staging. This object can outlive a dataset reset,
/// but never its native resource ownership. Capacity exhaustion is an explicit
/// recovery fault: authoritative trade attempts are not overwritten by quotes.
pub struct Outbox {
    state: Mutex<State>,
    maximum: usize,
    bytes: usize,
    changed: watch::Sender<u64>,
}
impl Outbox {
    /// Creates explicit record and encoded-byte limits before admission.
    ///
    /// # Errors
    /// Rejects zero or capacities beyond 4,096 records / 64 MiB.
    pub fn new(maximum: usize, bytes: usize) -> Result<Self, &'static str> {
        if !(1..=4096).contains(&maximum) || !(1..=64 * 1024 * 1024).contains(&bytes) {
            return Err("Invalid evidence outbox capacity");
        }
        let (changed, _) = watch::channel(0);
        Ok(Self {
            state: Mutex::new(State {
                items: BTreeMap::new(),
                bytes: 0,
                next: 0,
            }),
            maximum,
            bytes,
            changed,
        })
    }
    /// Admits validated copied bytes, preserving the first complete pricing attempt
    /// for duplicate source keys, including copies with different content.
    ///
    /// # Errors
    /// Invalid evidence, interrupted writer or exhausted bounded
    /// storage fail without partial mutation or waiting for external I/O.
    pub fn retain(&self, evidence: &Evidence) -> Result<Arc<Record>, &'static str> {
        let identity = evidence.source.identity()?;
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Evidence outbox writer interrupted")?;
        if let Some(existing) = state.items.get(&identity) {
            return Ok(existing.record.clone());
        }
        let digest = evidence.source.source_digest()?;
        let bytes = evidence.encode()?;
        if state.items.len() >= self.maximum || bytes.len() > self.bytes.saturating_sub(state.bytes)
        {
            return Err("Native evidence outbox capacity exhausted");
        }
        let receipt = state
            .next
            .checked_add(1)
            .ok_or("Evidence receipt identity exhausted")?;
        let record = Arc::new(Record {
            contract: evidence.source.contract.clone(),
            value_date: evidence.source.date.to_julian_day() - 1_721_426,
            generation: evidence.source.generation,
            identity: identity.clone(),
            digest,
            bytes: bytes.into(),
            receipt,
            submitted: tokio::time::Instant::now(),
        });
        state.next = receipt;
        state.bytes += record.bytes.len();
        state.items.insert(
            identity,
            Item {
                record: record.clone(),
                persisted: false,
            },
        );
        self.changed.send_modify(|v| *v = v.saturating_add(1));
        Ok(record)
    }
    /// Applies a durable external acknowledgment to the exact retained record.
    /// This is distinct from NATS PONG, transport flush and transient enqueue.
    ///
    /// # Errors
    /// Unknown/replaced receipts or economic conflicts never authorize publication.
    pub fn acknowledge(
        &self,
        identity: &str,
        digest: &str,
        receipt: u64,
    ) -> Result<(), &'static str> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Evidence outbox writer interrupted")?;
        let item = state
            .items
            .get_mut(identity)
            .ok_or("Unknown evidence acknowledgment")?;
        if item.record.receipt != receipt || item.record.digest != digest {
            return Err("Stale or conflicting evidence acknowledgment");
        }
        item.persisted = true;
        self.changed.send_modify(|v| *v = v.saturating_add(1));
        Ok(())
    }
    /// Returns a bounded copied page of pending persistence or acknowledged actor
    /// publications. Retaining this page cannot pin any dataset or outbox mutex.
    ///
    /// # Errors
    /// Invalid page size or interrupted state is explicit.
    pub fn capture(
        &self,
        persisted: bool,
        maximum: usize,
    ) -> Result<Vec<Arc<Record>>, &'static str> {
        if !(1..=64).contains(&maximum) {
            return Err("Invalid evidence handoff page size");
        }
        let state = self
            .state
            .lock()
            .map_err(|_| "Evidence outbox writer interrupted")?;
        let mut ordered: Vec<_> = state
            .items
            .values()
            .filter(|item| item.persisted == persisted)
            .map(|item| &item.record)
            .collect();
        ordered.sort_by_key(|item| item.receipt);
        Ok(ordered.into_iter().take(maximum).cloned().collect())
    }
    /// Releases copied staging only after durable persistence and actor publication
    /// were independently confirmed. Repeated/stale completion cannot remove a
    /// newer attempt reusing the same economic source identity.
    ///
    /// # Errors
    /// Missing, unpersisted or replaced attempts cannot be completed.
    pub fn complete(&self, identity: &str, receipt: u64) -> Result<(), &'static str> {
        self.remove(identity, receipt, None)
    }

    /// Releases a pending copy when storage confirms it already owns a different attempt.
    ///
    /// # Errors
    /// Stale, mismatched or already acknowledged copies cannot be discarded.
    pub fn discard_duplicate(
        &self,
        identity: &str,
        digest: &str,
        receipt: u64,
    ) -> Result<(), &'static str> {
        self.remove(identity, receipt, Some(digest))
    }

    fn remove(
        &self,
        identity: &str,
        receipt: u64,
        duplicate_digest: Option<&str>,
    ) -> Result<(), &'static str> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Evidence outbox writer interrupted")?;
        let item = state
            .items
            .get(identity)
            .ok_or("Unknown evidence completion")?;
        let settled = match duplicate_digest {
            Some(digest) => !item.persisted && item.record.digest == digest,
            None => item.persisted,
        };
        if !settled || item.record.receipt != receipt {
            return Err("Unacknowledged or stale evidence completion");
        }
        let size = item.record.bytes.len();
        state.items.remove(identity);
        state.bytes -= size;
        self.changed.send_modify(|v| *v = v.saturating_add(1));
        Ok(())
    }
    /// Native event-driven handoff notification; no polling timer is needed.
    pub fn changes(&self) -> watch::Receiver<u64> {
        self.changed.subscribe()
    }
    /// Exact pending record and byte counts, captured under one short lock.
    /// No persistence callback or dataset resource is consulted.
    ///
    /// # Errors
    /// An interrupted writer is explicit.
    pub fn retained_counts(&self) -> Result<(usize, usize), &'static str> {
        let state = self
            .state
            .lock()
            .map_err(|_| "Evidence outbox writer interrupted")?;
        Ok((state.items.len(), state.bytes))
    }
    /// Samples actual pending evidence, durable receipts and configured bounds.
    pub fn counters(&self) -> Result<BTreeMap<String, i64>, &'static str> {
        let state = self.state.lock().map_err(|_| "Evidence diagnostics interrupted")?;
        Ok([
            ("Evidence pending records", state.items.len() as i64),
            ("Evidence durable records pending publication", state.items.values().filter(|item| item.persisted).count() as i64),
            ("Evidence record capacity", self.maximum as i64),
            ("Evidence retained bytes", state.bytes as i64),
            ("Evidence byte capacity", self.bytes as i64),
            ("Evidence records admitted", state.next.min(i64::MAX as u64) as i64),
        ].into_iter().map(|(name, value)| (name.into(), value)).collect())
    }
    /// Exact bytes owned by this bounded staging directory. Detached external
    /// copies have no relationship to dataset ownership or joining.
    ///
    /// # Errors
    /// An interrupted writer is explicit.
    pub fn retained_bytes(&self) -> Result<usize, &'static str> {
        Ok(self
            .state
            .lock()
            .map_err(|_| "Evidence outbox writer interrupted")?
            .bytes)
    }
}
