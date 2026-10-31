//! Option-chain API boundary and per-Blotter subscription ownership. A failed
//! request can still acquire a backend lease, so ownership is recorded before
//! sending and retained until that owner's release is acknowledged.
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use crate::models::{
    trade_contracts::{EvaluatedOptionChainReadModel, OptionContractExpiryReadModel},
    wire_types::FromBackend,
};
use chrono::NaiveDate;
use rmpv::Value;
use std::collections::{BTreeMap, BTreeSet};
const ROOT: &str = "TomasAI.IFM.Domain.MarketData.Shared";

/// Display cache only. Missing quotes retain their last visible values, while
/// absent contracts become stale and ineligible. Callers must keep the raw
/// backend snapshot separately for preview/submission eligibility.
pub fn same_display_scope(
    previous: &EvaluatedOptionChainReadModel,
    incoming: &EvaluatedOptionChainReadModel,
) -> bool {
    let frozen = |value: &EvaluatedOptionChainReadModel| {
        value
            .window_method
            .as_deref()
            .unwrap_or_default()
            .starts_with("Frozen emulator preview")
    };
    previous.underlying_contract_id == incoming.underlying_contract_id
        && previous.expiry_date == incoming.expiry_date
        && frozen(previous) == frozen(incoming)
}
pub fn merge_display(
    previous: Option<&EvaluatedOptionChainReadModel>,
    incoming: &EvaluatedOptionChainReadModel,
) -> Result<EvaluatedOptionChainReadModel, String> {
    let Some(previous) = previous.filter(|previous| same_display_scope(previous, incoming)) else {
        return Ok(incoming.clone());
    };
    let mut rows = previous
        .contracts
        .as_ref()
        .ok_or("Missing previous display contracts")?
        .clone();
    for row in &mut rows {
        row.is_stale = Some(true);
        row.selection_valid = Some(false);
    }
    for next in incoming
        .contracts
        .as_ref()
        .ok_or("Missing incoming display contracts")?
    {
        let Some(index) = rows
            .iter()
            .position(|old| old.contract_id == next.contract_id)
        else {
            rows.push(next.clone());
            continue;
        };
        let mut merged = next.clone();
        macro_rules! retain {($($field:ident),*)=>{$(if merged.$field.is_none(){merged.$field=rows[index].$field.clone();})*};}
        retain!(
            bid,
            ask,
            bid_size,
            ask_size,
            last,
            last_size,
            implied_volatility,
            theoretical_price,
            delta,
            gamma,
            vega,
            theta,
            rho,
            volume,
            open_interest,
            quote_at_utc,
            trade_at_utc,
            evaluated_at_utc,
            volume_value_date,
            open_interest_value_date
        );
        rows[index] = merged;
    }
    let keys = rows
        .iter()
        .map(super::option_selection::Contract::from_backend)
        .collect::<Result<Vec<_>, _>>()?;
    let mut order: Vec<_> = (0..rows.len()).collect();
    order.sort_by(|a, b| {
        super::option_selection::compare(&keys[*b].strike, &keys[*a].strike)
            .then_with(|| keys[*b].is_call.cmp(&keys[*a].is_call))
    });
    // Move the sorted concrete records; no second clone of every retained quote.
    Ok(EvaluatedOptionChainReadModel {
        underlying_contract_id: incoming.underlying_contract_id.clone(),
        expiry_date: incoming.expiry_date.clone(),
        underlying_price: incoming.underlying_price.clone(),
        lower_strike_bound: incoming.lower_strike_bound.clone(),
        upper_strike_bound: incoming.upper_strike_bound.clone(),
        window_method: incoming.window_method.clone(),
        as_of_utc: incoming.as_of_utc.clone(),
        contracts: Some(
            order
                .into_iter()
                .map(|index| std::mem::take(&mut rows[index]))
                .collect(),
        ),
        bollinger_standard_deviation20: incoming.bollinger_standard_deviation20.clone(),
        previous_session_close: incoming.previous_session_close.clone(),
        iv_rank: incoming.iv_rank.clone(),
        iv_rank_status: incoming.iv_rank_status.clone(),
        bollinger_as_of_utc: incoming.bollinger_as_of_utc.clone(),
        last_underlying_price: incoming.last_underlying_price.clone(),
        last_underlying_trade_at_utc: incoming.last_underlying_trade_at_utc.clone(),
        atm_implied_volatility: incoming.atm_implied_volatility.clone(),
        daily_price_change: incoming.daily_price_change.clone(),
        daily_price_change_fraction: incoming.daily_price_change_fraction.clone(),
        days_to_expiry: incoming.days_to_expiry.clone(),
    })
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
    into_service_result(reply)
}

pub async fn expirations(
    backend: &Backend,
    registry: &Registry,
    symbol: &str,
    value_date: NaiveDate,
    through: NaiveDate,
    requested: NaiveDate,
    deadline: tokio::time::Instant,
) -> Result<Vec<OptionContractExpiryReadModel>, String> {
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
    if wire.as_array().is_none_or(|values| values.len() > 10000) {
        return Err("Invalid option expiry collection".into());
    }
    let values = Vec::<OptionContractExpiryReadModel>::from_backend(&wire)?;
    let mut result = vec![];
    for row in values {
        if row.symbol.as_deref() != Some(symbol) {
            return Err("Option expiries crossed the requested symbol".into());
        }
        let expiry = crate::backend::date_input::parse(
            row.expiry_date
                .as_ref()
                .ok_or("Missing option expiry")?
                .as_str(),
        )?;
        if row.provider_root.as_deref().is_none_or(str::is_empty) {
            return Err("Missing provider root".into());
        }
        if expiry >= requested {
            result.push(row);
        }
    }
    result.sort_by(|a, b| {
        a.expiry_date
            .as_ref()
            .map(|value| value.as_str())
            .cmp(&b.expiry_date.as_ref().map(|value| value.as_str()))
    });
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
    ) -> Result<EvaluatedOptionChainReadModel, String> {
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
        let result = EvaluatedOptionChainReadModel::from_backend(&wire)?;
        if result.underlying_contract_id.as_deref() != Some(spec.contract.as_str())
            || result.expiry_date.as_ref().map(|value| value.as_str())
                != Some(spec.expiry.to_string().as_str())
        {
            return Err("Option chain crossed the requested contract or expiry".into());
        }
        let rows = result
            .contracts
            .as_ref()
            .filter(|values| values.len() <= 10000)
            .ok_or("Invalid evaluated option collection")?;
        let mut ids = BTreeSet::new();
        for row in rows {
            let contract = super::option_selection::Contract::from_backend(row)?;
            if !ids.insert(contract.id) {
                return Err("Duplicate evaluated option identity".into());
            }
        }
        chrono::DateTime::parse_from_rfc3339(
            result
                .as_of_utc
                .as_ref()
                .ok_or("Missing option-chain as-of")?
                .as_str(),
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
