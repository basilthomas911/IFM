//! Option-chain API boundary and per-Blotter subscription ownership. A failed
//! request can still acquire a backend lease, so ownership is recorded before
//! sending and retained until that owner's release is acknowledged.
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use chrono::NaiveDate;
use rmpv::Value;
use serde_json::Value as Json;
use std::collections::{BTreeMap, BTreeSet};
const ROOT: &str = "TomasAI.IFM.Domain.MarketData.Shared";

/// Display cache only. Missing quotes retain their last visible values, while
/// absent contracts become stale and ineligible. Callers must keep the raw
/// backend snapshot separately for preview/submission eligibility.
pub fn merge_display(previous: Option<&Json>, incoming: &Json) -> Result<Json, String> {
    let frozen = |v: &Json| {
        v["WindowMethod"]
            .as_str()
            .unwrap_or_default()
            .starts_with("Frozen emulator preview")
    };
    let Some(previous) = previous.filter(|p| {
        p["UnderlyingContractId"] == incoming["UnderlyingContractId"]
            && p["ExpiryDate"] == incoming["ExpiryDate"]
            && frozen(p) == frozen(incoming)
    }) else {
        return Ok(incoming.clone());
    };
    let mut rows = previous["Contracts"]
        .as_array()
        .ok_or("Missing previous display contracts")?
        .clone();
    for row in &mut rows {
        row["IsStale"] = true.into();
        row["SelectionValid"] = false.into();
    }
    for next in incoming["Contracts"]
        .as_array()
        .ok_or("Missing incoming display contracts")?
    {
        let Some(index) = rows
            .iter()
            .position(|old| old["ContractId"] == next["ContractId"])
        else {
            rows.push(next.clone());
            continue;
        };
        let mut merged = next.clone();
        for field in [
            "Bid",
            "Ask",
            "BidSize",
            "AskSize",
            "Last",
            "LastSize",
            "ImpliedVolatility",
            "TheoreticalPrice",
            "Delta",
            "Gamma",
            "Vega",
            "Theta",
            "Rho",
            "Volume",
            "OpenInterest",
            "QuoteAtUtc",
            "TradeAtUtc",
            "EvaluatedAtUtc",
            "VolumeValueDate",
            "OpenInterestValueDate",
        ] {
            if merged[field].is_null() {
                merged[field] = rows[index][field].clone();
            }
        }
        rows[index] = merged;
    }
    // Validate before sorting so malformed snapshots produce an explicit error
    // instead of a comparator panic or an approximate floating-point ordering.
    let keys = rows
        .iter()
        .map(super::option_selection::Contract::from_backend)
        .collect::<Result<Vec<_>, _>>()?;
    let mut order: Vec<_> = (0..rows.len()).collect();
    order.sort_by(|a, b| {
        super::option_selection::compare(&keys[*b].strike, &keys[*a].strike)
            .then_with(|| keys[*b].is_call.cmp(&keys[*a].is_call))
    });
    let mut result = incoming.clone();
    result["Contracts"] = Json::Array(order.into_iter().map(|i| rows[i].clone()).collect());
    Ok(result)
}

async fn query(
    backend: &Backend,
    registry: &Registry,
    name: &str,
    fields: BTreeMap<String, Value>,
    deadline: tokio::time::Instant,
) -> Result<Value, String> {
    let kind = format!("{ROOT}.Queries.{name}");
    let (actor, verb) = registry.route(&kind)?;
    let subject = ActorSubject::query(actor, verb, "none");
    let mut fields = fields;
    fields.insert("Subject".into(), subject.wire());
    fields.insert(
        "EntityId".into(),
        Value::Array(vec![0.into(), Value::Array(vec!["none".into()])]),
    );
    let request = registry.encode(&kind, &fields)?;
    let reply = backend
        .request_until(&subject, &request, deadline, None)
        .await?;
    Ok(service_result(&reply)?.clone())
}

pub async fn expirations(
    backend: &Backend,
    registry: &Registry,
    symbol: &str,
    value_date: NaiveDate,
    through: NaiveDate,
    requested: NaiveDate,
    deadline: tokio::time::Instant,
) -> Result<Vec<Json>, String> {
    if symbol.is_empty() || through < value_date {
        return Err("Invalid option maturity range".into());
    }
    let wire = query(
        backend,
        registry,
        "GetDatabentoOptionChainRangeQuery",
        BTreeMap::from([
            ("UnderlyingSymbol".into(), symbol.into()),
            ("FromMaturityDate".into(), date_only_wire(value_date)),
            ("ThroughMaturityDate".into(), date_only_wire(through)),
        ]),
        deadline,
    )
    .await?;
    let named = registry.project(
        &format!("{ROOT}.ViewModels.OptionContractExpiryReadModel[]"),
        &wire,
    )?;
    let values = named
        .as_array()
        .filter(|v| v.len() <= 10000)
        .ok_or("Invalid option expiry collection")?;
    let mut result = vec![];
    for row in values {
        if row["Symbol"].as_str() != Some(symbol) {
            return Err("Option expiries crossed the requested symbol".into());
        }
        let expiry = crate::backend::date_input::parse(
            row["ExpiryDate"].as_str().ok_or("Missing option expiry")?,
        )?;
        if row["ProviderRoot"].as_str().is_none_or(str::is_empty) {
            return Err("Missing provider root".into());
        }
        if expiry >= requested {
            result.push(row.clone());
        }
    }
    result.sort_by(|a, b| a["ExpiryDate"].as_str().cmp(&b["ExpiryDate"].as_str()));
    Ok(result)
}

#[derive(Clone, Debug)]
pub struct Specification {
    pub contract: String,
    pub symbol: String,
    pub expiry: NaiveDate,
    pub roots: Vec<String>,
    pub required: Vec<String>,
    pub wing_width: ExactDecimal,
    pub frozen: bool,
}
impl Specification {
    fn fields(&self, owner: &str, release: bool) -> Result<BTreeMap<String, Value>, String> {
        if self.contract.is_empty()
            || self.symbol.is_empty()
            || owner.is_empty()
            || self.roots.len() > 1000
            || self.required.len() > 10000
        {
            return Err("Invalid option-chain ownership or scope".into());
        }
        Ok(BTreeMap::from([
            ("UnderlyingContractId".into(), self.contract.clone().into()),
            ("UnderlyingSymbol".into(), self.symbol.clone().into()),
            ("ExpiryDate".into(), date_only_wire(self.expiry)),
            (
                "ProviderRoots".into(),
                Value::Array(if release {
                    vec![]
                } else {
                    self.roots.iter().cloned().map(Value::from).collect()
                }),
            ),
            (
                "RequiredContractIds".into(),
                Value::Array(if release {
                    vec![]
                } else {
                    self.required.iter().cloned().map(Value::from).collect()
                }),
            ),
            ("SubscriptionOwnerId".into(), owner.into()),
            ("StandardDeviationMultiplier".into(), Value::F64(2.5)),
            ("ReleaseOnly".into(), release.into()),
            (
                "AllowFrozenEmulatorPreview".into(),
                (!release && self.frozen).into(),
            ),
            (
                "FrozenEmulatorPreviewOnly".into(),
                (!release && self.frozen).into(),
            ),
            (
                "SpreadWingWidth".into(),
                if release {
                    "0".into()
                } else {
                    self.wing_width.wire()
                },
            ),
        ]))
    }
}

pub struct Lease {
    owner: String,
    scope: Option<Specification>,
}
impl Default for Lease {
    fn default() -> Self {
        Self {
            owner: uuid::Uuid::new_v4().simple().to_string(),
            scope: None,
        }
    }
}
impl Lease {
    pub fn owner(&self) -> &str {
        &self.owner
    }
    pub fn may_be_active(&self) -> bool {
        self.scope.is_some()
    }
    pub async fn load(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        spec: Specification,
        deadline: tokio::time::Instant,
    ) -> Result<Json, String> {
        if self.scope.as_ref().is_some_and(|old| {
            old.contract != spec.contract || old.expiry != spec.expiry || old.frozen != spec.frozen
        }) {
            self.release(backend, registry, deadline).await?;
        }
        let fields = spec.fields(&self.owner, false)?;
        // A deadline/cancellation cannot erase the only identity able to release
        // a subscription that the server may already have installed.
        self.scope = Some(spec.clone());
        let wire = query(
            backend,
            registry,
            "GetEvaluatedOptionChainQuery",
            fields,
            deadline,
        )
        .await?;
        let result = super::parameters::document(registry,
            &format!("{ROOT}.ViewModels.EvaluatedOptionChainReadModel"),
            &wire,
            true,
        )?;
        if result["UnderlyingContractId"] != spec.contract
            || result["ExpiryDate"] != spec.expiry.to_string()
        {
            return Err("Option chain crossed the requested contract or expiry".into());
        }
        let rows = result["Contracts"]
            .as_array()
            .filter(|v| v.len() <= 10000)
            .ok_or("Invalid evaluated option collection")?;
        let mut ids = BTreeSet::new();
        for row in rows {
            let contract = super::option_selection::Contract::from_backend(row)?;
            if !ids.insert(contract.id) {
                return Err("Duplicate evaluated option identity".into());
            }
        }
        // AsOfUtc is API evidence; callers must never substitute local time.
        chrono::DateTime::parse_from_rfc3339(
            result["AsOfUtc"]
                .as_str()
                .ok_or("Missing option-chain as-of")?,
        )
        .map_err(|_| "Invalid option-chain as-of")?;
        Ok(result)
    }
    pub async fn release(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let Some(scope) = &self.scope else {
            return Ok(());
        };
        query(
            backend,
            registry,
            "GetEvaluatedOptionChainQuery",
            scope.fields(&self.owner, true)?,
            deadline,
        )
        .await?;
        self.scope = None;
        Ok(())
    }
}
