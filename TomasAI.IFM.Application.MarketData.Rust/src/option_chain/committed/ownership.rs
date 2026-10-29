//! Existing nine-field physical scope identity and exact committed owner checks.
//! These are copied business facts, not mutable provider lease instructions.
use super::{Book, Plan};
use crate::reference::semantic::{self, Value as J};
use serde_json::Value;
use std::collections::{BTreeMap, BTreeSet};
impl Plan {
    /// Matches WorkerOptionChainRuntime.PhysicalDigest: exact financial decimals,
    /// original UTC expiry and reviewed provider identities in CLR ordinal order.
    ///
    /// # Errors
    /// Invalid/incomplete option facts or nonrepresentable provider timestamp.
    pub fn physical_digest(&self) -> Result<String, String> {
        if self.candidates.is_empty() {
            return Err("Physical option identity is empty".into());
        }
        let mut definitions = self.candidates.iter().collect::<Vec<_>>();
        definitions.sort_by_key(|row| row.contract.encode_utf16().collect::<Vec<_>>());
        let values = definitions
            .into_iter()
            .map(|row| {
                let expiration = row
                    .expiration_ns
                    .filter(|ns| ns % 100 == 0)
                    .and_then(|ns| i64::try_from(ns / 100).ok())
                    .ok_or("Invalid physical option expiry")?;
                if !matches!(row.right, 1 | 2) {
                    return Err("Invalid physical option right");
                }
                Ok(J::Object(vec![
                    ("Strike".into(), J::Decimal(row.strike)),
                    ("IsCall".into(), J::Bool(row.right == 1)),
                    ("ContractId".into(), J::String(row.contract.clone())),
                    ("Dataset".into(), J::String(row.dataset.clone())),
                    (
                        "InstrumentId".into(),
                        J::Unsigned(u64::from(row.instrument)),
                    ),
                    ("RawSymbol".into(), J::String(row.raw_symbol.clone())),
                    (
                        "UnderlyingContractId".into(),
                        J::String(row.underlying.clone()),
                    ),
                    ("ExpirationUtc".into(), semantic::utc(expiration)?),
                    (
                        "DefinitionDigest".into(),
                        J::String(row.definition_digest.clone()),
                    ),
                ]))
            })
            .collect::<Result<Vec<_>, &str>>()?;
        semantic::compute(&J::Array(values)).map_err(Into::into)
    }
}
impl Book {
    /// Validates a compatibility ownership message against actual committed facts.
    /// The caller cannot install or erase authoritative leases through this path.
    ///
    /// # Errors
    /// Stale authority/revision, changed physical facts or any owner/member mismatch.
    pub fn validate_ownership(&self, scope: &str, input: &Value) -> Result<bool, String> {
        if input["authority"].as_str() != Some(self.authority.as_str())
            || input["revision"].as_i64() != Some(self.revision)
        {
            return Err("OwnershipConflict".into());
        }
        let owners = input["owners"]
            .as_array()
            .filter(|rows| rows.len() <= 128)
            .ok_or("OwnershipInvalid")?;
        let mut actual = BTreeMap::new();
        for owner in owners {
            let id = super::guid(&owner["id"])?;
            let rows = owner["contracts"]
                .as_array()
                .filter(|rows| !rows.is_empty() && rows.len() <= 128)
                .ok_or("OwnershipInvalid")?;
            let mut contracts = BTreeSet::new();
            for row in rows {
                let contract = super::text(row, 256)?;
                if !contracts.insert(contract) {
                    return Err("OwnershipInvalid".into());
                }
            }
            if actual.insert(id, contracts).is_some() {
                return Err("OwnershipInvalid".into());
            }
        }
        let expected = self
            .sources
            .values()
            .flat_map(|source| &source.leases)
            .filter(|lease| lease.option && lease.plan == scope)
            .map(|lease| {
                (
                    lease.id,
                    [lease.contract.clone()]
                        .into_iter()
                        .collect::<BTreeSet<_>>(),
                )
            })
            .collect::<BTreeMap<_, _>>();
        if actual != expected {
            return Err("OwnershipConflict".into());
        }
        if let Some(plan) = self.plans.get(scope) {
            if input["digest"].as_str() != Some(plan.physical_digest()?.as_str()) {
                return Err("OwnershipInvalid".into());
            }
        } else if !actual.is_empty() {
            return Err("OwnershipConflict".into());
        }
        Ok(!actual.is_empty())
    }
}
