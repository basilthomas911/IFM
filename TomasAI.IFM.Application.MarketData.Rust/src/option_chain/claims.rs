//! Bounded native scope intent over the shared monotonic lease authority.
//! Scope strings map to native slots; neither provider connections nor managed
//! handles are stored in this registry. Mutations perform no I/O or async waits.

use crate::subscriptions::{
    Failure, Token,
    leases::{Leases, Lifetime},
};
use std::{
    collections::{BTreeMap, BTreeSet},
    time::Duration,
};
use tokio::time::Instant;

/// Native desired scope claims, independent of physical connection attempts.
#[derive(Clone)]
pub struct Claims {
    leases: Leases,
    slots: BTreeMap<String, u32>,
    retiring: BTreeSet<u32>,
}
impl Claims {
    /// Creates one bounded owner registry for a fresh dataset generation.
    ///
    /// # Errors
    /// Rejects invalid generation before allocating any intent.
    pub fn new(generation: u128) -> Result<Self, Failure> {
        Ok(Self {
            leases: Leases::new(generation, 4096, 64, 16384, Duration::from_secs(120))?,
            slots: BTreeMap::new(),
            retiring: BTreeSet::new(),
        })
    }
    /// Replaces the complete desired scope set for one native business owner.
    ///
    /// # Errors
    /// Rejects invalid/capacity/stale input without partially changing mappings.
    pub fn acquire(
        &mut self,
        generation: u128,
        owner: u128,
        scopes: &[String],
        lifetime: Lifetime,
        now: Instant,
    ) -> Result<Token, Failure> {
        if scopes.is_empty()
            || scopes.len() > 64
            || scopes.iter().any(|s| s.is_empty() || s.len() > 512)
            || scopes.iter().collect::<BTreeSet<_>>().len() != scopes.len()
        {
            return Err(Failure::InvalidInput);
        }
        // At most 64 short IDs: the bounded cold command can prepare atomically.
        let mut slots = self.slots.clone();
        let mut instruments = Vec::with_capacity(scopes.len());
        for scope in scopes {
            let slot = if let Some(slot) = slots.get(scope) {
                *slot
            } else {
                let slot = (1..=64)
                    .find(|s| !slots.values().any(|used| used == s))
                    .ok_or(Failure::Capacity)?;
                slots.insert(scope.clone(), slot);
                slot
            };
            instruments.push(slot);
        }
        let change = self
            .leases
            .acquire(generation, owner, &instruments, lifetime, now)?;
        self.slots = slots;
        self.retiring.extend(change.unsubscribe);
        for slot in instruments {
            self.retiring.remove(&slot);
        }
        change.token.ok_or(Failure::InvalidInput)
    }
    /// Extends only the exact current temporary claim. No provider gate is used.
    ///
    /// # Errors
    /// Ended, expired and old-generation tokens cannot be revived.
    pub fn renew(&mut self, token: Token, ttl: Duration, now: Instant) -> Result<(), Failure> {
        self.leases.renew(token, ttl, now)
    }
    /// Ends an exact current claim and queues unreferenced physical scope cleanup.
    pub fn release(&mut self, token: Token) {
        self.retiring.extend(self.leases.release(token).unsubscribe);
    }
    /// Expires native temporary claims and returns scopes no longer referenced.
    /// The supervisor must mark them retiring before releasing its state lock.
    pub fn retire(&mut self, now: Instant) -> Vec<String> {
        for change in self.leases.expire(now) {
            self.retiring.extend(change.unsubscribe);
        }
        let used = self.leases.instruments();
        let expired: Vec<_> = self
            .slots
            .iter()
            .filter(|(_, slot)| self.retiring.contains(slot) && !used.contains(slot))
            .map(|(id, slot)| (id.clone(), *slot))
            .collect();
        for (id, slot) in &expired {
            self.slots.remove(id);
            self.retiring.remove(slot);
        }
        expired.into_iter().map(|(id, _)| id).collect()
    }
    /// Exact next monotonic expiry; no C# maintenance timer participates.
    pub fn next_expiration(&self) -> Option<Instant> {
        self.leases.next_expiration()
    }
    /// Checks native intent without renewing it or acquiring provider resources.
    pub fn contains(&self, id: &str) -> bool {
        self.slots
            .get(id)
            .is_some_and(|slot| self.leases.instruments().contains(slot))
    }
    /// Checks exact business ownership without renewing an expired claim.
    pub fn owns(&self, owner: u128, id: &str, now: Instant) -> bool {
        self.slots
            .get(id)
            .is_some_and(|slot| self.leases.owns(owner, *slot, now))
    }
    /// Copies the current token for a native business-owner command. It does not
    /// renew intent or expose any physical lease, connection or task.
    pub fn token(&self, owner: u128) -> Option<Token> {
        self.leases.token(owner)
    }
}
