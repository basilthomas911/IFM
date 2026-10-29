//! Bounded copied evidence after emergency retirement. Old economic records are
//! never relabelled as current, and retaining them never delays dataset rebuilding.
use super::evidence_outbox::Outbox;
use std::{collections::VecDeque, sync::Arc};

/// Explicit accounting for eviction of stale market-data evidence. These records
/// are provider trade prints, not executed user orders or trade instructions.
#[derive(Clone, Copy, Default, Debug, PartialEq, Eq)]
pub struct Accounting {
    /// Number of old outboxes evicted at the fixed retention boundary.
    pub generations: u64,
    /// Exact retained records removed from the native directory at eviction.
    pub records: u64,
    /// Exact encoded bytes removed from the native directory at eviction.
    pub bytes: u64,
}
/// At most four retired outboxes / 64 MiB; the active outbox is separate.
/// Detached external copies have independent bounded consumers and no feed handles.
#[derive(Default)]
pub struct Archive {
    entries: VecDeque<Arc<Outbox>>,
    accounting: Accounting,
}
impl Archive {
    /// Retains pending copied data without waiting for database or broker work.
    ///
    /// # Errors
    /// Interrupted outbox state or an oversized single retired outbox is explicit.
    pub fn retain(&mut self, outbox: Arc<Outbox>) -> Result<(), &'static str> {
        let (_, bytes) = outbox.retained_counts()?;
        if bytes == 0 || self.entries.iter().any(|v| Arc::ptr_eq(v, &outbox)) {
            return Ok(());
        }
        if bytes > 16 * 1024 * 1024 {
            return Err("Retired evidence exceeds dataset outbox limit");
        }
        // Completed detached outboxes need no archive slot. Examine all entries
        // before mutation so interrupted accounting cannot silently discard data.
        let counts = self
            .entries
            .iter()
            .map(|v| v.retained_counts())
            .collect::<Result<Vec<_>, _>>()?;
        let mut entries = std::mem::take(&mut self.entries);
        for count in counts {
            let entry = entries
                .pop_front()
                .ok_or("Retired evidence accounting mismatch")?;
            if count.1 != 0 {
                self.entries.push_back(entry);
            }
        }
        if self.entries.len() == 4 {
            let old = self
                .entries
                .front()
                .ok_or("Retired evidence accounting mismatch")?;
            let (records, bytes) = old.retained_counts()?;
            self.accounting.generations = self.accounting.generations.saturating_add(1);
            self.accounting.records = self.accounting.records.saturating_add(records as u64);
            self.accounting.bytes = self.accounting.bytes.saturating_add(bytes as u64);
            self.entries.pop_front();
        }
        self.entries.push_back(outbox);
        Ok(())
    }
    /// Latest retired immutable directory, never replayed into a new generation.
    pub fn latest(&self) -> Option<Arc<Outbox>> {
        self.entries.back().cloned()
    }
    /// Copied retention and cumulative eviction counts; no recovery gate or IO.
    pub fn status(&self) -> (usize, Accounting) {
        (self.entries.len(), self.accounting)
    }
}
