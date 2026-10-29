//! Native physical reconstruction identity excludes mutable business watermarks.
//! A delivery/authority revision alone must not rotate a healthy SDK connection.
use super::Book;
use sha2::{Digest, Sha256};
fn text(hash: &mut Sha256, value: &str) {
    hash.update((value.len() as u64).to_le_bytes());
    hash.update(value.as_bytes());
}
impl Book {
    /// Complete physical source/reference/risk-membership identity. Length-prefixed
    /// fields avoid ambiguous concatenation; source watermarks/outbox IDs are not
    /// subscriptions, while every frozen recipe field remains significant.
    ///
    /// # Errors
    /// Invalid typed future metadata cannot be used as reconstruction evidence.
    pub fn physical_signature(&self) -> Result<[u8; 32], String> {
        let mut hash = Sha256::new();
        hash.update(b"native-committed-physical/v1");
        hash.update((self.plans.len() as u64).to_le_bytes());
        for (id, plan) in &self.plans {
            text(&mut hash, id);
            hash.update(plan.maturity.to_le_bytes());
            hash.update([
                u8::from(plan.separate),
                u8::from(plan.independent),
                u8::from(plan.non_utc),
            ]);
            if let Some(reference) = &plan.references {
                hash.update([1]);
                hash.update(reference.signature);
            } else {
                hash.update([0]);
            }
            hash.update((plan.candidates.len() as u64).to_le_bytes());
            for candidate in &plan.candidates {
                for value in [
                    &candidate.contract,
                    &candidate.mapping_version,
                    &candidate.definition_digest,
                    &candidate.dataset,
                    &candidate.raw_symbol,
                    &candidate.underlying,
                ] {
                    text(&mut hash, value);
                }
                hash.update(candidate.publisher.to_le_bytes());
                hash.update(candidate.instrument.to_le_bytes());
                hash.update([candidate.right]);
                for word in candidate.strike.bits {
                    hash.update(word.to_le_bytes());
                }
                match candidate.expiration_ns {
                    Some(expiry) => {
                        hash.update([1]);
                        hash.update(expiry.to_le_bytes());
                    }
                    None => hash.update([0]),
                }
            }
            hash.update((plan.futures.len() as u64).to_le_bytes());
            for future in &plan.futures {
                text(
                    &mut hash,
                    &crate::reference::semantic::compute(&future.semantic()?)?,
                );
            }
            hash.update((plan.routes.len() as u64).to_le_bytes());
            for route in &plan.routes {
                for value in [&route.contract, &route.symbol, &route.root] {
                    text(&mut hash, value);
                }
            }
        }
        let monitored = self
            .sources
            .values()
            .flat_map(|source| &source.leases)
            .filter(|lease| lease.option)
            .map(|lease| lease.contract.as_str())
            .collect::<std::collections::BTreeSet<_>>();
        hash.update((monitored.len() as u64).to_le_bytes());
        for contract in monitored {
            text(&mut hash, contract);
        }
        Ok(hash.finalize().into())
    }
}
