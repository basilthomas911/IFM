//! Bounded native desired-to-physical subscription ownership. The native
//! `leases` authority adds monotonic ephemeral expiry and explicit durable intent;
//! managed code supplies commands, never a renewal timer or socket lifetime.
//! Each generation issues fresh tokens; ended tokens cannot release a replacement.
/// Native expiry/renewal authority independent of managed callers and provider I/O.
pub mod leases;
use std::collections::{BTreeMap, BTreeSet};
/// Fresh runtime identity, separate from its stable desired/business owner.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Token {
    /// Dataset generation; stale releases cannot affect a replacement.
    pub generation: u128,
    /// Monotonic identity within this owner registry lifetime.
    pub serial: u64,
    /// Durable/application owner identity.
    pub owner: u128,
}
/// Explicit input/capacity/revision failures; no partially applied owner batch.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Failure {
    /// Zero identity/instrument, duplicate/empty request, or invalid limits.
    InvalidInput,
    /// Owner, per-owner, total membership or physical contract capacity exceeded.
    Capacity,
    /// Delayed input belongs to a retired generation.
    StaleGeneration,
    /// A monotonic token identity was exhausted; never wrap into an old lease.
    IdentityExhausted,
}
/// Physical changes after a successful atomic mutation. Subscription operations
/// happen outside registry mutation; failures are repaired from snapshot intent.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Change {
    /// Current owner token when acquiring/replacing; none on release.
    pub token: Option<Token>,
    /// Instruments newly required by at least one owner.
    pub subscribe: Vec<u32>,
    /// Instruments no longer required by any owner.
    pub unsubscribe: Vec<u32>,
}
#[derive(Clone)]
struct Entry {
    token: Token,
    instruments: BTreeSet<u32>,
}
/// Bounded desired-to-physical registry for one lane. No clocks/timers, provider
/// calls, persistence or second renewal authority live in this object.
#[derive(Clone)]
pub struct Owners {
    generation: u128,
    next: u64,
    maximum_owners: usize,
    maximum_contracts: usize,
    maximum_memberships: usize,
    memberships: usize,
    owners: BTreeMap<u128, Entry>,
    references: BTreeMap<u32, usize>,
}
impl Owners {
    /// Creates explicit owner/physical/total-membership limits before allocation.
    pub fn new(
        generation: u128,
        maximum_owners: usize,
        maximum_contracts: usize,
        maximum_memberships: usize,
    ) -> Result<Self, Failure> {
        if generation == 0
            || maximum_owners == 0
            || maximum_contracts == 0
            || maximum_memberships == 0
        {
            return Err(Failure::InvalidInput);
        }
        Ok(Self {
            generation,
            next: 0,
            maximum_owners,
            maximum_contracts,
            maximum_memberships,
            memberships: 0,
            owners: BTreeMap::new(),
            references: BTreeMap::new(),
        })
    }
    /// Atomically acquires/replaces one complete owner. Repeated identical intent
    /// returns the same active token. Changed intent gets a fresh identity so a
    /// delayed release of the old token cannot remove its new dependencies.
    pub fn acquire(
        &mut self,
        generation: u128,
        owner: u128,
        instruments: &[u32],
    ) -> Result<Change, Failure> {
        if generation != self.generation {
            return Err(Failure::StaleGeneration);
        }
        if owner == 0 || instruments.is_empty() || instruments.contains(&0) {
            return Err(Failure::InvalidInput);
        }
        if instruments.len() > self.maximum_contracts {
            return Err(Failure::Capacity);
        }
        let desired: BTreeSet<_> = instruments.iter().copied().collect();
        if desired.len() != instruments.len() {
            return Err(Failure::InvalidInput);
        }
        let prior = self.owners.get(&owner);
        if let Some(prior) = prior
            && prior.instruments == desired
        {
            return Ok(Change {
                token: Some(prior.token),
                subscribe: Vec::new(),
                unsubscribe: Vec::new(),
            });
        }
        if prior.is_none() && self.owners.len() >= self.maximum_owners {
            return Err(Failure::Capacity);
        }
        let memberships = self
            .memberships
            .checked_sub(prior.map_or(0, |p| p.instruments.len()))
            .and_then(|n| n.checked_add(desired.len()))
            .filter(|n| *n <= self.maximum_memberships)
            .ok_or(Failure::Capacity)?;
        let removed: Vec<_> = prior
            .into_iter()
            .flat_map(|p| p.instruments.iter().copied())
            .filter(|id| !desired.contains(id))
            .collect();
        let added: Vec<_> = desired
            .iter()
            .copied()
            .filter(|id| prior.is_none_or(|p| !p.instruments.contains(id)))
            .collect();
        let unsubscribe: Vec<_> = removed
            .iter()
            .copied()
            .filter(|id| self.references.get(id) == Some(&1))
            .collect();
        let subscribe: Vec<_> = added
            .iter()
            .copied()
            .filter(|id| !self.references.contains_key(id))
            .collect();
        if self.references.len() - unsubscribe.len() + subscribe.len() > self.maximum_contracts {
            return Err(Failure::Capacity);
        }
        let serial = self.next.checked_add(1).ok_or(Failure::IdentityExhausted)?;
        for id in removed {
            let count = self.references.get_mut(&id).expect("registered membership");
            *count -= 1;
            if *count == 0 {
                self.references.remove(&id);
            }
        }
        for id in added {
            *self.references.entry(id).or_default() += 1;
        }
        self.next = serial;
        self.memberships = memberships;
        let token = Token {
            generation,
            serial,
            owner,
        };
        self.owners.insert(
            owner,
            Entry {
                token,
                instruments: desired,
            },
        );
        Ok(Change {
            token: Some(token),
            subscribe,
            unsubscribe,
        })
    }
    /// Idempotently releases only the exact active token. Stale/ended tokens do
    /// nothing, preserving a newer generation or replacement owner acquisition.
    pub fn release(&mut self, token: Token) -> Change {
        if token.generation != self.generation
            || self
                .owners
                .get(&token.owner)
                .is_none_or(|e| e.token != token)
        {
            return Change {
                token: None,
                subscribe: Vec::new(),
                unsubscribe: Vec::new(),
            };
        }
        let entry = self.owners.remove(&token.owner).expect("validated owner");
        self.memberships -= entry.instruments.len();
        let mut unsubscribe = Vec::new();
        for id in entry.instruments {
            let count = self.references.get_mut(&id).expect("registered membership");
            *count -= 1;
            if *count == 0 {
                self.references.remove(&id);
                unsubscribe.push(id);
            }
        }
        Change {
            token: None,
            subscribe: Vec::new(),
            unsubscribe,
        }
    }
    /// Reacquires all desired owners in a new generation, preserving business
    /// intent but replacing every runtime identity. The caller first proves old
    /// session cleanup; this mutation alone is never cleanup/readiness evidence.
    pub fn replace_generation(&mut self, generation: u128) -> Result<Vec<Token>, Failure> {
        if generation == 0 || generation == self.generation {
            return Err(Failure::InvalidInput);
        }
        let last = self
            .next
            .checked_add(self.owners.len() as u64)
            .ok_or(Failure::IdentityExhausted)?;
        let mut tokens = Vec::with_capacity(self.owners.len());
        for (&owner, entry) in &mut self.owners {
            self.next += 1;
            entry.token = Token {
                generation,
                serial: self.next,
                owner,
            };
            tokens.push(entry.token);
        }
        self.next = last;
        self.generation = generation;
        Ok(tokens)
    }
    /// Current active runtime token, never a disposed token from a prior attempt.
    pub fn token(&self, owner: u128) -> Option<Token> {
        self.owners.get(&owner).map(|e| e.token)
    }
    /// Checks one exact owner's current desired membership without allocation.
    pub fn owns(&self, owner: u128, instrument: u32) -> bool {
        self.owners
            .get(&owner)
            .is_some_and(|entry| entry.instruments.contains(&instrument))
    }
    /// Copies the complete current desired physical membership.
    pub fn instruments(&self) -> Vec<u32> {
        self.references.keys().copied().collect()
    }
    /// Physical owner count for diagnostics and exact dependency qualification.
    pub fn references(&self, instrument: u32) -> usize {
        self.references.get(&instrument).copied().unwrap_or(0)
    }
}
