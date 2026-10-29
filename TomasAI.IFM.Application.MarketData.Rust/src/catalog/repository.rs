//! Atomic, bounded catalog hydration. A failed or cancelled refresh never
//! replaces the last usable universe. Only one staging universe is admitted;
//! queries continue against the committed universe while it is populated.
use super::{Catalog, Definition, Failure, selection};
use std::sync::Arc;

/// A refresh token or observation revision no longer identifies current state.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Error {
    /// Invalid source data or noncanonical contract identity.
    Invalid,
    /// Configured source/row limit would be exceeded.
    Capacity,
    /// Another refresh is active or the caller's token/revision is stale.
    Conflict,
    /// More than one provider record resolves to the same domain identity.
    Ambiguous,
}
impl From<Failure> for Error {
    fn from(value: Failure) -> Self {
        match value {
            Failure::Capacity => Self::Capacity,
            Failure::InvalidDefinition => Self::Invalid,
        }
    }
}

/// Date-scoped catalog owner with monotonically increasing refresh identities.
/// The active and staging universes each have explicit limits; peak retained
/// raw-source storage is therefore at most twice the configured byte limit.
pub struct Repository {
    cancelled: std::sync::Arc<std::sync::atomic::AtomicU64>,
    active: Catalog,
    staging: Option<(u64, Catalog)>,
    capacity: usize,
    bytes: usize,
    next: u64,
    revision: u64,
}
impl Repository {
    /// Establishes limits before acquiring or parsing any provider definitions.
    pub fn new(capacity: usize, bytes: usize) -> Result<Self, Error> {
        Ok(Self {
            cancelled: std::sync::Arc::new(std::sync::atomic::AtomicU64::new(0)),
            active: Catalog::new(capacity, bytes)?,
            staging: None,
            capacity,
            bytes,
            next: 0,
            revision: 0,
        })
    }
    /// Starts a complete replacement, preserving the currently readable catalog.
    /// Nonblocking rollback signal used when an acquisition future is dropped.
    pub fn cancellation_marker(&self) -> std::sync::Arc<std::sync::atomic::AtomicU64> {
        self.cancelled.clone()
    }
    /// Opens a new bounded replacement after reclaiming any cancelled staging.
    pub fn begin(&mut self) -> Result<u64, Error> {
        if self
            .staging
            .as_ref()
            .is_some_and(|(id, _)| *id <= self.cancelled.load(std::sync::atomic::Ordering::Acquire))
        {
            self.staging = None;
        }
        if self.staging.is_some() {
            return Err(Error::Conflict);
        }
        let next = self.next.checked_add(1).ok_or(Error::Capacity)?;
        self.staging = Some((next, Catalog::new(self.capacity, self.bytes)?));
        self.next = next;
        Ok(next)
    }
    /// Parses one bounded source line; caller memory is never retained. Errors
    /// discard the entire staging universe so partial refreshes cannot commit.
    pub fn append(&mut self, token: u64, dataset: &str, json: &str) -> Result<(), Error> {
        if token <= self.cancelled.load(std::sync::atomic::Ordering::Acquire)
            || !self.staging.as_ref().is_some_and(|(id, _)| *id == token)
        {
            return Err(Error::Conflict);
        }
        let result = Definition::parse(dataset, json)
            .and_then(|value| self.staging.as_mut().unwrap().1.insert(value));
        if result.is_err() {
            self.staging = None;
        }
        result.map_err(Into::into)
    }
    /// Atomically publishes the fully hydrated universe. Empty replacements are
    /// explicit and valid; no implicit merge retains obsolete provider rows.
    pub fn commit(&mut self, token: u64) -> Result<u64, Error> {
        if token <= self.cancelled.load(std::sync::atomic::Ordering::Acquire)
            || !self.staging.as_ref().is_some_and(|(id, _)| *id == token)
        {
            return Err(Error::Conflict);
        }
        let revision = self.revision.checked_add(1).ok_or(Error::Capacity)?;
        self.active = self.staging.take().unwrap().1;
        self.revision = revision;
        Ok(revision)
    }
    /// Discards only the caller's refresh. A delayed cancellation cannot abort
    /// a newer refresh, and abort never clears already committed definitions.
    pub fn abort(&mut self, token: u64) -> Result<(), Error> {
        if !self.staging.as_ref().is_some_and(|(id, _)| *id == token) {
            return Err(Error::Conflict);
        }
        self.staging = None;
        Ok(())
    }
    /// Reads a unique canonical contract at the supplied immutable revision.
    /// Returning an error for duplicate identities prevents arbitrary mappings.
    pub fn resolve(&self, revision: u64, id: &str) -> Result<Option<Arc<Definition>>, Error> {
        if revision != self.revision {
            return Err(Error::Conflict);
        }
        let identity = selection::parse(id).map_err(Error::from)?;
        let mut candidates = self
            .active
            .definitions()
            .filter(|d| selection::matches(&identity, d));
        let value = candidates.next();
        if candidates.next().is_some() {
            return Err(Error::Ambiguous);
        }
        Ok(value)
    }
    /// Current committed revision, unchanged by staging work or its failure.
    pub fn revision(&self) -> u64 {
        self.revision
    }
    /// Pins the committed universe for a bounded cold query. Rows remain
    /// immutable and source-exact even when the repository refreshes afterward.
    pub fn snapshot(&self) -> Vec<Arc<Definition>> {
        self.active.definitions().collect()
    }
}
