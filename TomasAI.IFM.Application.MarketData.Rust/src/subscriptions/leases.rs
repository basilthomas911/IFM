//! Native lease lifetime authority for one independently owned dataset.
//!
//! Persisted business intent is imported by command; ephemeral discovery owners
//! expire using Tokio's monotonic clock. No managed timer, acknowledgement,
//! callback or database task participates in renewal or expiry. The dataset
//! supervisor sleeps until `next_expiration`, rather than polling a timer.

use super::{Change, Failure, Owners, Token};
use std::{collections::BTreeMap, time::Duration};
use tokio::time::Instant;

/// Explicit desired-owner lifetime, separate from a physical socket generation.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Lifetime {
    /// Imported durable intent lives until an explicit release or session close.
    Durable,
    /// Discovery/display intent expires unless a native command renews it.
    Ephemeral(Duration),
}

#[derive(Clone)]
struct Lease {
    token: Token,
    expires: Option<Instant>,
}

/// Single native authority for lease state and physical subscription refcounts.
/// Mutations are bounded in-memory work; provider I/O happens outside this object.
#[derive(Clone)]
pub struct Leases {
    owners: Owners,
    leases: BTreeMap<u128, Lease>,
    maximum_ttl: Duration,
}

impl Leases {
    /// Constructs explicit capacity/lifetime bounds before any owner allocation.
    ///
    /// # Errors
    /// Rejects invalid owner capacities, zero identity and TTL above 120 seconds.
    pub fn new(
        generation: u128,
        maximum_owners: usize,
        maximum_contracts: usize,
        maximum_memberships: usize,
        maximum_ttl: Duration,
    ) -> Result<Self, Failure> {
        if maximum_ttl.is_zero() || maximum_ttl > Duration::from_secs(120) {
            return Err(Failure::InvalidInput);
        }
        Ok(Self {
            owners: Owners::new(
                generation,
                maximum_owners,
                maximum_contracts,
                maximum_memberships,
            )?,
            leases: BTreeMap::new(),
            maximum_ttl,
        })
    }

    fn expiration(&self, lifetime: Lifetime, now: Instant) -> Result<Option<Instant>, Failure> {
        match lifetime {
            Lifetime::Durable => Ok(None),
            Lifetime::Ephemeral(ttl) if !ttl.is_zero() && ttl <= self.maximum_ttl => {
                now.checked_add(ttl).map(Some).ok_or(Failure::InvalidInput)
            }
            Lifetime::Ephemeral(_) => Err(Failure::InvalidInput),
        }
    }

    /// Imports/replaces an owner's complete desired contract set atomically.
    ///
    /// # Errors
    /// Returns generation/input/capacity failure without partial mutation.
    /// Call `expire` before admitting work at a new instant; an expired token
    /// cannot be renewed, but fresh explicit acquisition can establish new intent.
    pub fn acquire(
        &mut self,
        generation: u128,
        owner: u128,
        instruments: &[u32],
        lifetime: Lifetime,
        now: Instant,
    ) -> Result<Change, Failure> {
        let expires = self.expiration(lifetime, now)?;
        if self
            .leases
            .get(&owner)
            .is_some_and(|lease| lease.expires.is_some_and(|at| at <= now))
        {
            return Err(Failure::StaleGeneration);
        }
        let change = self.owners.acquire(generation, owner, instruments)?;
        let token = change.token.ok_or(Failure::InvalidInput)?;
        self.leases.insert(owner, Lease { token, expires });
        Ok(change)
    }

    /// Renews an exact still-active token without provider/control admission.
    /// The token generation/serial fences delayed renewals after reset or release.
    /// Durable owners cannot accidentally become ephemeral through renewal.
    ///
    /// # Errors
    /// Rejects ended, expired or stale tokens and invalid TTL; never revives them.
    pub fn renew(&mut self, token: Token, ttl: Duration, now: Instant) -> Result<(), Failure> {
        let expires = self.expiration(Lifetime::Ephemeral(ttl), now)?;
        let lease = self
            .leases
            .get_mut(&token.owner)
            .ok_or(Failure::StaleGeneration)?;
        if lease.token != token || lease.expires.is_none_or(|at| at <= now) {
            return Err(Failure::StaleGeneration);
        }
        lease.expires = expires;
        Ok(())
    }

    /// Releases only the matching current token. A delayed old-generation release
    /// is harmless and cannot remove a replacement owner's dependencies.
    pub fn release(&mut self, token: Token) -> Change {
        if self
            .leases
            .get(&token.owner)
            .is_some_and(|lease| lease.token == token)
        {
            self.leases.remove(&token.owner);
        }
        self.owners.release(token)
    }

    /// Retires expired temporary owners; returns the exact physical deltas to
    /// realize outside mutation. Durable intent never expires on a timer.
    pub fn expire(&mut self, now: Instant) -> Vec<Change> {
        let expired: Vec<_> = self
            .leases
            .values()
            .filter(|lease| lease.expires.is_some_and(|at| at <= now))
            .map(|lease| lease.token)
            .collect();
        expired
            .into_iter()
            .map(|token| self.release(token))
            .collect()
    }

    /// Earliest temporary expiry. The supervisor can select this against command
    /// arrival and cancellation without a maintenance polling interval.
    pub fn next_expiration(&self) -> Option<Instant> {
        self.leases.values().filter_map(|lease| lease.expires).min()
    }

    /// Rotates runtime identities after the supervisor proves old resources joined.
    /// Desired owners and their original expirations are preserved, not renewed.
    ///
    /// # Errors
    /// Rejects zero/unchanged generation and monotonic identity exhaustion.
    pub fn replace_generation(&mut self, generation: u128) -> Result<Vec<Token>, Failure> {
        let tokens = self.owners.replace_generation(generation)?;
        for token in &tokens {
            if let Some(lease) = self.leases.get_mut(&token.owner) {
                lease.token = *token;
            }
        }
        Ok(tokens)
    }

    /// Returns current desired instruments in canonical deduplicated order.
    /// Checks exact active ownership, including monotonic expiry, without renewal.
    pub fn owns(&self, owner: u128, instrument: u32, now: Instant) -> bool {
        self.leases
            .get(&owner)
            .is_some_and(|lease| lease.expires.is_none_or(|until| until > now))
            && self.owners.owns(owner, instrument)
    }
    /// Copies the complete current desired physical membership.
    pub fn instruments(&self) -> Vec<u32> {
        self.owners.instruments()
    }

    /// Reads the current token without extending its lifetime.
    pub fn token(&self, owner: u128) -> Option<Token> {
        self.leases.get(&owner).map(|lease| lease.token)
    }
}
