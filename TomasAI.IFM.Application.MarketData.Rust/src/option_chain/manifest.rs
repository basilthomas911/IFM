//! Bounded immutable application-context manifests retained by queued trades.
//! Tick ingress clones an Arc, never serializes metadata. The last queued/source
//! owner releases its charge through RAII, including reset/cancellation exits.
use std::sync::{
    Arc, Mutex, Weak,
    atomic::{AtomicUsize, Ordering},
};

/// Runtime-wide retained-manifest budget. Payloads are opaque application binary
/// contracts; native code never treats them as a price or trusted parsed context.
pub struct Budget {
    limit: usize,
    used: AtomicUsize,
    manifests: Mutex<std::collections::BTreeMap<u128, Weak<Manifest>>>,
}
/// Immutable binary context paired with its qualified native context identity.
/// A queued trade can retain this version after a later reference refresh.
pub struct Manifest {
    identity: u128,
    bytes: Vec<u8>,
    budget: Arc<Budget>,
}
impl std::fmt::Debug for Manifest {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("Manifest")
            .field("identity", &self.identity)
            .field("length", &self.bytes.len())
            .finish_non_exhaustive()
    }
}
impl Budget {
    /// Creates a hard retained-byte bound, at most sixteen MiB per runtime.
    pub fn new(limit: usize) -> Result<Arc<Self>, &'static str> {
        if limit == 0 || limit > 16 * 1024 * 1024 {
            return Err("Invalid trade manifest budget");
        }
        Ok(Arc::new(Self {
            limit,
            used: AtomicUsize::new(0),
            manifests: Mutex::new(Default::default()),
        }))
    }
    /// Current charged payload bytes, including versions retained by queued trades.
    pub fn used(&self) -> usize {
        self.used.load(Ordering::Acquire)
    }
    /// Copies one cold binary manifest using fallible bulk allocation. Reservation
    /// happens before copying; all failures and final-owner drops release the charge.
    pub fn retain(
        self: &Arc<Self>,
        identity: u128,
        bytes: &[u8],
    ) -> Result<Arc<Manifest>, &'static str> {
        if identity == 0 || bytes.is_empty() || bytes.len() > 4 * 1024 * 1024 {
            return Err("Invalid trade context manifest");
        }
        let mut manifests = self
            .manifests
            .lock()
            .map_err(|_| "Trade manifest owner interrupted")?;
        if let Some(existing) = manifests.get(&identity).and_then(Weak::upgrade) {
            if existing.bytes() != bytes {
                return Err("Trade manifest identity reused with changed metadata");
            }
            return Ok(existing);
        }
        if manifests.len() >= 16_384 {
            manifests.retain(|_, value| value.strong_count() != 0);
            if manifests.len() >= 16_384 {
                return Err("Trade context manifest count exhausted");
            }
        }
        let mut used = self.used.load(Ordering::Acquire);
        loop {
            let next = used
                .checked_add(bytes.len())
                .filter(|n| *n <= self.limit)
                .ok_or("Trade context manifest capacity exhausted")?;
            match self
                .used
                .compare_exchange_weak(used, next, Ordering::AcqRel, Ordering::Acquire)
            {
                Ok(_) => break,
                Err(actual) => used = actual,
            }
        }
        let mut copy = Vec::new();
        if copy.try_reserve_exact(bytes.len()).is_err() {
            self.used.fetch_sub(bytes.len(), Ordering::Release);
            return Err("Trade context manifest allocation failed");
        }
        copy.extend_from_slice(bytes);
        let manifest = Arc::new(Manifest {
            identity,
            bytes: copy,
            budget: self.clone(),
        });
        manifests.insert(identity, Arc::downgrade(&manifest));
        Ok(manifest)
    }
}
impl Manifest {
    /// Exact immutable native pricing-context identity.
    pub fn identity(&self) -> u128 {
        self.identity
    }
    /// Borrowed bytes are valid only while this owned manifest remains alive.
    pub fn bytes(&self) -> &[u8] {
        &self.bytes
    }
}
impl Drop for Manifest {
    fn drop(&mut self) {
        self.budget
            .used
            .fetch_sub(self.bytes.len(), Ordering::Release);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn exact_capacity_arc_retention_failures_and_last_drop_release_charges() {
        let budget = Budget::new(16).unwrap();
        let first = budget.retain(1, &[1; 8]).unwrap();
        let repeated = budget.retain(1, &[1; 8]).unwrap();
        assert!(Arc::ptr_eq(&first, &repeated));
        drop(repeated);
        assert!(budget.retain(1, &[2; 8]).is_err());
        assert_eq!(budget.used(), 8);
        let queued = first.clone();
        drop(first);
        assert_eq!(budget.used(), 8);
        let second = budget.retain(2, &[2; 8]).unwrap();
        assert_eq!(budget.used(), 16);
        assert!(budget.retain(3, &[3]).is_err());
        assert_eq!(budget.used(), 16);
        assert!(budget.retain(0, &[3]).is_err());
        assert!(budget.retain(3, &[]).is_err());
        assert_eq!(queued.identity(), 1);
        assert_eq!(queued.bytes(), &[1; 8]);
        drop(queued);
        assert_eq!(budget.used(), 8);
        drop(second);
        assert_eq!(budget.used(), 0);
        assert!(Budget::new(0).is_err());
        assert!(Budget::new(16 * 1024 * 1024 + 1).is_err());
    }
    #[test]
    fn concurrent_context_replacement_never_overcommits_or_leaves_charged_bytes() {
        let budget = Budget::new(4096).unwrap();
        std::thread::scope(|scope| {
            for worker in 1..=8 {
                let budget = budget.clone();
                scope.spawn(move || {
                    for n in 0..2000 {
                        if let Ok(value) = budget.retain(worker * 10000 + n + 1, &[7; 512]) {
                            let queued = value.clone();
                            assert!(budget.used() <= 4096);
                            drop(value);
                            assert_eq!(queued.bytes().len(), 512);
                        }
                    }
                });
            }
        });
        assert_eq!(budget.used(), 0);
    }
    #[test]
    fn manifest_count_and_payload_limits_are_independent_and_reclaimed() {
        let budget = Budget::new(16 * 1024 * 1024).unwrap();
        let mut retained = Vec::new();
        for identity in 1..=16_384 {
            retained.push(budget.retain(identity, &[1]).unwrap());
        }
        assert_eq!(budget.used(), 16_384);
        assert!(budget.retain(16_385, &[1]).is_err());
        assert!(Arc::ptr_eq(&retained[0], &budget.retain(1, &[1]).unwrap()));
        drop(retained);
        assert_eq!(budget.used(), 0);
        let maximum = budget.retain(16_385, &vec![2; 4 * 1024 * 1024]).unwrap();
        assert_eq!(budget.used(), 4 * 1024 * 1024);
        assert!(
            budget
                .retain(16_386, &vec![2; 4 * 1024 * 1024 + 1])
                .is_err()
        );
        drop(maximum);
        assert_eq!(budget.used(), 0);
    }
}
