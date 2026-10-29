//! Copied committed-business authority and reconstruction facts. Parsing is
//! independent of provider resources; no partial page or malformed snapshot can
//! change desired ownership. The dataset supplies cancellation and one deadline.
#[path = "committed/state.rs"]
mod state;
use super::{composition::model::Future, discovery::Discovery, universe::Candidate};
use crate::{
    dataset::background::ScopeDemand,
    reference::decimal,
    runtime::{Deadline, owner::Child},
};
use serde_json::Value;
use sha2::{Digest, Sha256};
pub use state::Directory;
#[path = "committed/ack.rs"]
mod ack;
#[path = "committed/ownership.rs"]
mod ownership;
use std::{
    collections::{BTreeMap, BTreeSet},
    sync::Arc,
};
use tokio_util::sync::CancellationToken;
/// One durable ticker lease, not a provider subscription/handle.
#[derive(Clone, PartialEq, Eq)]
pub struct Lease {
    /// Actual persisted CLR Guid bytes.
    pub id: [u8; 16],
    /// Monotonic lease version.
    pub version: i64,
    /// Strategy=2, working order=3, position=4.
    pub purpose: u8,
    /// Exact provider namespace and wire schema copied from the business ticker.
    pub provider: String,
    /// Exact requested wire schema.
    pub schema: String,
    /// Immutable reconstruction identity.
    pub plan: String,
    /// Canonical selected contract.
    pub contract: String,
    /// Options rather than outright futures.
    pub option: bool,
    /// Required canonical underlying for an option.
    pub underlying: Option<String>,
}
/// Authoritative source watermark, including terminal/unknown sources.
#[derive(Clone, PartialEq, Eq)]
pub struct Authority {
    /// Exact persisted source identifier.
    pub id: String,
    /// Committed source version.
    pub version: i64,
    /// Active=0, terminal=1, unknown=2; unknown retains its existing leases.
    pub status: u8,
    /// Persisted authority transition reason.
    pub reason: String,
    /// Original authority event watermark.
    pub event: [u8; 16],
    /// Existing immutable business fact digest.
    pub digest: String,
    /// Exact business workflow kind.
    pub workflow_type: String,
    /// Exact business leg identity.
    pub leg: String,
    /// Copied business workflow identity, never a dataset generation.
    pub workflow: String,
    /// Complete durable ticker ownership.
    pub leases: Vec<Lease>,
}
/// Fixed provider route copied from a sealed business reconstruction plan.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct Route {
    /// Canonical future dependency.
    pub contract: String,
    /// Exact provider symbol; no continuous-contract substitution.
    pub symbol: String,
    /// Original provider root.
    pub root: String,
}
/// Exact immutable reconstruction plan, without live context/rate observations.
#[derive(Clone)]
pub struct Plan {
    /// Existing sealed 64-hex business identity.
    pub id: String,
    /// Explicit option maturity; zero only for a future-only plan.
    pub maturity: i32,
    /// Normalized provider facts; Rust resolves exact reviewed versions.
    pub candidates: Vec<Candidate>,
    /// Outright future facts, never fabricated from option rows.
    pub futures: Vec<Future>,
    /// Exact fixed dependency routes for rebuilding provider subscriptions.
    pub routes: Vec<Route>,
    /// Exact immutable option reference recipe; absent only for outright futures.
    pub references: Option<Arc<crate::reference::inputs::Inputs>>,
    /// Whether a non-UTC future definition must be rejected by capture.
    pub non_utc: bool,
    /// Existing source descriptor.
    pub separate: bool,
    /// Existing background-chain descriptor.
    pub independent: bool,
}
/// Complete bounded committed snapshot. It contains no task, lease resource,
/// connection, native context or managed callback.
#[derive(Clone)]
pub struct Book {
    /// Trusted business journal namespace.
    pub authority: String,
    /// Persisted monotonic global revision.
    pub revision: i64,
    /// Existing business journal digest, used for conditional durable delivery.
    pub journal_digest: String,
    /// Full copied-source identity, independent of physical generation.
    pub signature: [u8; 32],
    /// Complete source watermarks.
    pub sources: BTreeMap<String, Authority>,
    /// Complete immutable reconstruction plans, at most eight.
    pub plans: BTreeMap<String, Plan>,
}
fn text(value: &Value, max: usize) -> Result<String, String> {
    let value = value
        .as_str()
        .filter(|s| {
            !s.trim().is_empty()
                && *s == s.trim()
                && s.encode_utf16().count() <= max
                && !s.chars().any(char::is_control)
        })
        .ok_or("Invalid committed source text")?;
    Ok(value.into())
}
fn hex(value: &Value) -> Result<String, String> {
    let value = text(value, 64)?;
    if value.len() != 64 || !value.bytes().all(|b| b.is_ascii_hexdigit()) {
        return Err("Invalid committed source digest".into());
    }
    Ok(value)
}
fn positive(value: &Value) -> Result<i64, String> {
    value
        .as_i64()
        .filter(|v| *v > 0)
        .ok_or("Invalid committed source version".into())
}
fn guid(value: &Value) -> Result<[u8; 16], String> {
    let value = value
        .as_str()
        .filter(|s| s.len() == 32 && s.bytes().all(|b| b.is_ascii_hexdigit()))
        .ok_or("Invalid committed source Guid")?;
    let mut bytes = u128::from_str_radix(value, 16)
        .map_err(|_| "Invalid committed source Guid")?
        .to_be_bytes();
    bytes[..4].reverse();
    bytes[4..6].reverse();
    bytes[6..8].reverse();
    if bytes == [0; 16] {
        return Err("Empty committed source Guid".into());
    }
    Ok(bytes)
}
fn future(value: &Value) -> Result<(Future, bool), String> {
    let row = value
        .as_array()
        .filter(|r| r.len() == 10)
        .ok_or("Malformed committed future definition")?;
    Ok((
        Future {
            contract: text(&row[0], 1024)?,
            root: text(&row[1], 60)?,
            dataset: text(&row[2], 64)?,
            exchange: text(&row[3], 64)?,
            currency: text(&row[4], 16)?,
            last_trading: row[5].as_i64().ok_or("Invalid committed future expiry")?,
            multiplier: row[7]
                .as_str()
                .and_then(decimal::parse)
                .ok_or("Invalid committed future multiplier")?,
            tick: row[8]
                .as_str()
                .and_then(decimal::parse)
                .ok_or("Invalid committed future tick")?,
            digest: hex(&row[9])?,
        },
        row[6]
            .as_i64()
            .ok_or("Invalid committed future UTC offset")?
            != 0,
    ))
}
impl Book {
    /// Parses complete verified bytes. All shapes, ownership capacities and exact
    /// selected-leg membership are checked before publication to the native owner.
    ///
    /// # Errors
    /// Incomplete/conflicting business identities, malformed source or capacity.
    pub fn parse(bytes: &[u8], expected_authority: &str) -> Result<Self, String> {
        if bytes.len() > 32 * 1024 * 1024 {
            return Err("Committed source byte capacity exceeded".into());
        }
        let value: Value =
            serde_json::from_slice(bytes).map_err(|_| "Malformed committed source snapshot")?;
        if value["schema"].as_u64() != Some(1) || value["dataset"].as_str() != Some("GLBX.MDP3") {
            return Err("Invalid committed source schema/dataset".into());
        }
        let authority = text(&value["authority"], 128)?;
        if authority != expected_authority {
            return Err("Committed authority namespace changed".into());
        }
        let revision = value["revision"]
            .as_i64()
            .filter(|v| *v >= 0)
            .ok_or("Invalid committed journal revision")?;
        let journal_digest = hex(&value["digest"])?;
        let input = value["plans"]
            .as_array()
            .filter(|v| v.len() <= 8)
            .ok_or("Committed plan capacity exceeded")?;
        let mut plans = BTreeMap::new();
        for plan in input {
            let id = hex(&plan["id"])?;
            let maturity = plan["maturity"]
                .as_i64()
                .and_then(|v| i32::try_from(v).ok())
                .filter(|v| (0..=3652058).contains(v))
                .ok_or("Invalid committed maturity")?;
            if plan["schema"].as_u64() != Some(1) || plan["dataset"].as_str() != Some("GLBX.MDP3") {
                return Err("Invalid committed reconstruction schema/dataset".into());
            }
            let separate = plan["separate"]
                .as_bool()
                .ok_or("Invalid committed source descriptor")?;
            let independent = plan["independent"]
                .as_bool()
                .ok_or("Invalid committed source descriptor")?;
            let candidates = plan["candidates"]
                .as_array()
                .filter(|v| v.len() <= 2048)
                .ok_or("Committed option capacity exceeded")?;
            let definitions = if candidates.is_empty() {
                vec![]
            } else {
                ScopeDemand::from_command(&serde_json::json!({"owner":[1,0],"maturity":maturity,"complete":true,"separate":separate,"independent":independent,"candidates":candidates}))?.candidates
            };
            let references = if definitions.is_empty() {
                None
            } else {
                Some(Arc::new(crate::reference::inputs::Inputs::from_value(
                    &plan["references"],
                )?))
            };
            let raw = plan["futures"]
                .as_array()
                .filter(|v| v.len() <= 16)
                .ok_or("Committed future capacity exceeded")?;
            let mut futures = Vec::new();
            let mut non_utc = false;
            for row in raw {
                let (model, offset) = future(row)?;
                futures.push(model);
                non_utc |= offset;
            }
            if definitions.is_empty() == futures.is_empty()
                || separate && (definitions.len() != 1 || independent)
                || independent && definitions.is_empty()
            {
                return Err("Invalid committed reconstruction asset membership".into());
            }
            let routes = plan["routes"]
                .as_array()
                .filter(|v| !v.is_empty() && v.len() <= 16)
                .ok_or("Committed dependency route capacity exceeded")?;
            let mut dependencies = BTreeSet::new();
            let mut copied_routes = Vec::new();
            for route in routes {
                let row = route
                    .as_array()
                    .filter(|r| r.len() == 7)
                    .ok_or("Malformed committed dependency route")?;
                let contract = text(&row[0], 256)?;
                let symbol = text(&row[1], 128)?;
                let root = text(&row[3], 60)?;
                if row[2].as_u64() != Some(1)
                    || row[4].as_str() != Some("GLBX.MDP3")
                    || row[5].as_bool() != Some(false)
                    || row[6].as_bool() != Some(false)
                    || !dependencies.insert(contract.clone())
                {
                    return Err("Invalid committed dependency route".into());
                }
                copied_routes.push(Route {
                    contract,
                    symbol,
                    root,
                });
            }
            if definitions
                .iter()
                .any(|d| !dependencies.contains(&d.underlying))
                || futures.iter().any(|d| !dependencies.contains(&d.contract))
            {
                return Err("Incomplete committed dependency routes".into());
            }
            if plans
                .insert(
                    id.clone(),
                    Plan {
                        id,
                        maturity,
                        candidates: definitions,
                        futures,
                        routes: copied_routes,
                        references,
                        non_utc,
                        separate,
                        independent,
                    },
                )
                .is_some()
            {
                return Err("Duplicate committed reconstruction identity".into());
            }
        }
        let input = value["authorities"]
            .as_array()
            .filter(|v| v.len() <= 10000)
            .ok_or("Committed authority capacity exceeded")?;
        let mut sources = BTreeMap::new();
        let mut ids = BTreeSet::new();
        let mut used_plans = BTreeSet::new();
        let mut owners = BTreeSet::new();
        for source in input {
            let id = text(&source["id"], 256)?;
            let version = positive(&source["version"])?;
            let event = guid(&source["event_id"])?;
            let digest = hex(&source["digest"])?;
            let status = source["status"]
                .as_u64()
                .filter(|v| *v <= 2)
                .ok_or("Invalid committed authority status")? as u8;
            let reason = text(&source["reason"], 128)?;
            let workflow_type = text(&source["workflow_type"], 64)?;
            let workflow = text(&source["workflow_id"], 128)?;
            let leg = text(&source["leg_id"], 128)?;
            if !owners.insert((workflow_type.clone(), workflow.clone(), leg.clone())) {
                return Err("Duplicate committed business owner".into());
            }
            let input = source["leases"]
                .as_array()
                .filter(|v| v.len() <= 128)
                .ok_or("Committed owner lease capacity exceeded")?;
            if status == 1 && !input.is_empty() {
                return Err("Terminal authority retains committed leases".into());
            }
            let mut leases = Vec::new();
            let mut targets = BTreeSet::new();
            for lease in input {
                let row = lease
                    .as_array()
                    .filter(|v| v.len() == 10)
                    .ok_or("Malformed committed lease")?;
                let lease_id = guid(&row[0])?;
                let lease_version = positive(&row[1])?;
                let purpose = row[2]
                    .as_u64()
                    .filter(|v| (2..=4).contains(v))
                    .ok_or("Invalid committed lease purpose")? as u8;
                let provider = text(&row[3], 128)?;
                if row[4].as_str() != Some("GLBX.MDP3") {
                    return Err("Committed ticker dataset changed".into());
                }
                let contract = text(&row[5], 256)?;
                let schema = text(&row[6], 64)?;
                let asset = row[7]
                    .as_u64()
                    .filter(|v| *v <= 1)
                    .ok_or("Invalid committed ticker asset")?;
                let underlying = if row[8].is_null() {
                    None
                } else {
                    Some(text(&row[8], 256)?)
                };
                let plan = hex(&row[9])?;
                if !ids.insert(lease_id)
                    || ids.len() > 10000
                    || !targets.insert((
                        purpose,
                        provider.clone(),
                        schema.clone(),
                        asset,
                        contract.clone(),
                        underlying.clone(),
                        plan.clone(),
                    ))
                {
                    return Err("Duplicate/excess committed owner target".into());
                }
                let recipe = plans
                    .get(&plan)
                    .ok_or("Committed ticker reconstruction is missing")?;
                if asset == 1 {
                    if !recipe.candidates.iter().any(|candidate| {
                        candidate.contract == contract
                            && Some(candidate.underlying.as_str()) == underlying.as_deref()
                    }) {
                        return Err("Committed option differs from exact reconstruction".into());
                    }
                } else if underlying.is_some()
                    || !recipe
                        .futures
                        .iter()
                        .any(|future| future.contract == contract)
                {
                    return Err("Committed future differs from exact reconstruction".into());
                }
                used_plans.insert(plan.clone());
                leases.push(Lease {
                    id: lease_id,
                    version: lease_version,
                    purpose,
                    provider,
                    schema,
                    plan,
                    contract,
                    option: asset == 1,
                    underlying,
                });
            }
            if sources
                .insert(
                    id.clone(),
                    Authority {
                        id,
                        version,
                        status,
                        event,
                        digest,
                        reason,
                        workflow_type,
                        workflow,
                        leg,
                        leases,
                    },
                )
                .is_some()
            {
                return Err("Duplicate committed source authority".into());
            }
        }
        if used_plans.len() != plans.len() {
            return Err("Unused committed reconstruction plan".into());
        }
        Ok(Self {
            authority,
            revision,
            journal_digest,
            signature: Sha256::digest(bytes).into(),
            sources,
            plans,
        })
    }
    /// Rejects old or conflicting snapshots before a desired-state mutation.
    ///
    /// # Errors
    /// Namespace, journal revision or equal-revision source-content conflict.
    pub fn validate_successor(&self, prior: &Self) -> Result<(), String> {
        if self.authority != prior.authority
            || self.revision < prior.revision
            || self.revision == prior.revision && self.signature != prior.signature
        {
            return Err("Committed ownership revision conflicts with accepted native state".into());
        }
        for (id, old) in &prior.sources {
            let next = self
                .sources
                .get(id)
                .ok_or("Committed snapshot removed an authority watermark")?;
            if next.version < old.version || next.version == old.version && next != old {
                return Err(
                    "Committed authority watermark conflicts with accepted native state".into(),
                );
            }
        }
        Ok(())
    }
}
impl Discovery {
    /// Reads one complete committed snapshot using fixed 32 KiB source chunks
    /// over the existing 4 KiB raw pages. Every byte shares the dataset deadline.
    ///
    /// # Errors
    /// Bad header/count/hash, incomplete page, business validation or cancellation.
    pub async fn read_committed(
        &self,
        authority: &str,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Book, String> {
        if authority.trim().is_empty() || authority.len() > 128 {
            return Err("Invalid committed source namespace".into());
        }
        let args =
            serde_json::to_vec(&serde_json::json!({"authority":authority,"dataset":"GLBX.MDP3"}))
                .map_err(|_| "Invalid committed source arguments")?;
        let header = crate::messaging::raw_io::read(
            self.broker.clone(),
            self.scope.clone(),
            parent.clone(),
            9,
            &args,
            stop,
            deadline,
        )
        .await?;
        let header: Value =
            serde_json::from_slice(&header).map_err(|_| "Malformed committed source header")?;
        let id = header["id"]
            .as_str()
            .filter(|v| v.len() == 32 && v.bytes().all(|b| b.is_ascii_hexdigit()))
            .ok_or("Invalid committed snapshot identity")?;
        let count = header["total"]
            .as_u64()
            .filter(|v| *v > 0 && *v <= 32 * 1024 * 1024)
            .ok_or("Invalid committed source byte count")? as usize;
        let digest = hex(&header["digest"])?;
        if header["page_bytes"].as_u64() != Some(32768) {
            return Err("Invalid committed source page size".into());
        }
        let mut bytes = Vec::with_capacity(count.min(65536));
        let mut hash = Sha256::new();
        for offset in (0..count).step_by(32768) {
            let args = serde_json::to_vec(&serde_json::json!({"snapshot":id,"offset":offset}))
                .map_err(|_| "Invalid committed source page arguments")?;
            let page = crate::messaging::raw_io::read(
                self.broker.clone(),
                self.scope.clone(),
                parent.clone(),
                9,
                &args,
                stop,
                deadline,
            )
            .await?;
            if page.len() != (count - offset).min(32768) {
                return Err("Incomplete committed source page".into());
            }
            hash.update(&page);
            bytes.extend_from_slice(&page);
        }
        if !format!("{:X}", hash.finalize()).eq_ignore_ascii_case(&digest) {
            return Err("Committed source digest mismatch".into());
        }
        if stop.is_cancelled() || deadline.remaining().is_zero() {
            return Err("Committed source cancelled/original deadline expired".into());
        }
        Book::parse(&bytes, authority)
    }
}

#[path = "committed/physical.rs"]
mod physical;
