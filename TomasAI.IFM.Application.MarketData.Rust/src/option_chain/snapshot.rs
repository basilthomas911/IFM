//! Bounded snapshot semantics ported from `OptionChainSnapshotStore<T>`.
//!
//! A short RwLock makes generation/state updates linearizable. It is never held
//! across I/O or asynchronous work. Published payloads are immutable `Arc`s, so a
//! read clones a reference without copying/allocating the full option chain.

use std::{
    collections::HashMap,
    sync::{Arc, RwLock},
};

/// Why a snapshot operation could not be performed.
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum StoreError {
    /// Capacity, generation, scope or publication metadata is invalid.
    InvalidInput,
    /// A prior panic interrupted a store writer; retire and rebuild this owner.
    InterruptedWriter,
}

/// One immutable prepared payload and its monotonic publication identity.
#[derive(Debug)]
pub struct Publication<T> {
    /// Positive version within this generation/scope.
    pub version: i64,
    /// Explicit UTC timestamp ticks supplied by the application boundary.
    pub published_at_utc_ticks: i64,
    /// Immutable prepared snapshot.
    pub snapshot: T,
}

struct Epoch<T> {
    generation: u128,
    admitted: bool,
    scopes: HashMap<String, Arc<Publication<T>>>,
}

/// Thread-safe prepared snapshot storage for one current session generation.
pub struct SnapshotStore<T> {
    maximum_scopes: usize,
    epoch: RwLock<Epoch<T>>,
}

impl<T> SnapshotStore<T> {
    /// Creates a bounded store without admitting any generation.
    ///
    /// # Errors
    /// Returns `InvalidInput` for zero capacity.
    pub fn new(maximum_scopes: usize) -> Result<Self, StoreError> {
        if maximum_scopes == 0 {
            return Err(StoreError::InvalidInput);
        }
        Ok(Self {
            maximum_scopes,
            epoch: RwLock::new(Epoch {
                generation: 0,
                admitted: false,
                scopes: HashMap::new(),
            }),
        })
    }

    /// Admits a new nonzero generation and retires previous publications.
    ///
    /// # Errors
    /// Rejects zero identity or a writer interrupted by panic.
    pub fn admit(&self, generation: u128) -> Result<(), StoreError> {
        if generation == 0 {
            return Err(StoreError::InvalidInput);
        }
        let mut epoch = self
            .epoch
            .write()
            .map_err(|_| StoreError::InterruptedWriter)?;
        if epoch.generation == generation && epoch.admitted {
            return Ok(());
        }
        epoch.generation = generation;
        epoch.admitted = true;
        epoch.scopes.clear();
        Ok(())
    }

    /// Retires only the specified generation, preserving a newer replacement.
    ///
    /// # Errors
    /// Reports a writer interrupted by panic.
    pub fn fence(&self, generation: u128) -> Result<(), StoreError> {
        let mut epoch = self
            .epoch
            .write()
            .map_err(|_| StoreError::InterruptedWriter)?;
        if epoch.generation == generation && epoch.admitted {
            epoch.admitted = false;
            epoch.scopes.clear();
        }
        Ok(())
    }

    /// Publishes only current-generation, strictly newer scope observations.
    ///
    /// # Errors
    /// Rejects empty/whitespace scopes, nonpositive versions, negative timestamps
    /// or a writer interrupted by panic. False means stale/fenced/capacity refusal.
    /// No old or partial publication is replaced on refusal.
    pub fn publish(
        &self,
        generation: u128,
        scope: &str,
        publication: Publication<T>,
    ) -> Result<bool, StoreError> {
        if scope.trim().is_empty()
            || publication.version <= 0
            || publication.published_at_utc_ticks < 0
        {
            return Err(StoreError::InvalidInput);
        }
        let mut epoch = self
            .epoch
            .write()
            .map_err(|_| StoreError::InterruptedWriter)?;
        if !epoch.admitted || epoch.generation != generation {
            return Ok(false);
        }
        if let Some(previous) = epoch.scopes.get(scope) {
            if previous.version >= publication.version
                || previous.published_at_utc_ticks > publication.published_at_utc_ticks
            {
                return Ok(false);
            }
        } else if epoch.scopes.len() >= self.maximum_scopes {
            return Ok(false);
        }
        epoch.scopes.insert(scope.into(), Arc::new(publication));
        Ok(true)
    }

    /// Reads an admitted immutable snapshot; zero generation means current epoch.
    ///
    /// # Errors
    /// Reports a writer interrupted by panic. No I/O or provider acquisition occurs.
    pub fn read(
        &self,
        generation: u128,
        scope: &str,
    ) -> Result<Option<Arc<Publication<T>>>, StoreError> {
        let epoch = self
            .epoch
            .read()
            .map_err(|_| StoreError::InterruptedWriter)?;
        Ok(
            if epoch.admitted && (generation == 0 || generation == epoch.generation) {
                epoch.scopes.get(scope).cloned()
            } else {
                None
            },
        )
    }

    /// Removes one scope only if its owning generation is still current.
    ///
    /// # Errors
    /// Reports a writer interrupted by panic.
    pub fn remove(&self, generation: u128, scope: &str) -> Result<(), StoreError> {
        let mut epoch = self
            .epoch
            .write()
            .map_err(|_| StoreError::InterruptedWriter)?;
        if epoch.generation == generation {
            epoch.scopes.remove(scope);
        }
        Ok(())
    }
}
