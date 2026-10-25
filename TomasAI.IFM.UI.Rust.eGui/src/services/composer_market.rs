//! Market-selection lifecycle for a single native Blotter incarnation. Applied
//! startup defaults and business dates are API inputs; there is no local clock,
//! pricing engine, global subscription stop or guessed parameter fallback.
use super::{business_context::BusinessContext, option_chain, option_draft, parameters};
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use serde_json::Value as Json;
use std::collections::BTreeMap;

pub struct Defaults {
    pub call_delta: ExactDecimal,
    pub put_delta: ExactDecimal,
    pub call_width: ExactDecimal,
    pub put_width: ExactDecimal,
}
fn number(row: &Json, key: &str) -> Result<ExactDecimal, String> {
    ExactDecimal::parse(
        &row[key]
            .as_str()
            .map(str::to_string)
            .unwrap_or_else(|| row[key].to_string()),
    )
}
/// Resolve the same latest applied run, enabled component and exact referenced
/// version as the hybrid. Ambiguous assignments/symbols fail explicitly.
pub fn applied_defaults(
    runs: &[Json],
    strategy: &str,
    symbol: &str,
) -> Result<Option<Defaults>, String> {
    let condor = strategy.ends_with("IronCondor");
    let component = if condor {
        "option-spread-strategy.iron-condor-defaults"
    } else {
        "option-spread-strategy.vertical-spread-defaults"
    };
    let mut matching = vec![];
    for run in runs {
        let scopes = run["Scopes"]
            .as_array()
            .ok_or("Missing applied startup scopes")?;
        if scopes
            .iter()
            .any(|a| a["Enabled"] == true && a["Scope"]["ComponentCode"] == component)
        {
            let at = chrono::DateTime::parse_from_rfc3339(
                run["CreatedAtUtc"]
                    .as_str()
                    .ok_or("Missing startup timestamp")?,
            )
            .map_err(|_| "Invalid startup timestamp")?;
            matching.push((at, run));
        }
    }
    matching.sort_by(|a, b| b.0.cmp(&a.0));
    let Some((_, run)) = matching.first() else {
        return Ok(None);
    };
    let assignments: Vec<_> = run["Scopes"]
        .as_array()
        .unwrap()
        .iter()
        .filter(|a| a["Enabled"] == true && a["Scope"]["ComponentCode"] == component)
        .collect();
    if assignments.len() != 1 {
        return Err("Applied market defaults have ambiguous component assignments".into());
    }
    let reference = &assignments[0]["Reference"];
    let versions: Vec<_> = run["Versions"]
        .as_array()
        .ok_or("Missing applied startup versions")?
        .iter()
        .filter(|v| v["Reference"] == *reference)
        .collect();
    if versions.is_empty() {
        return Ok(None);
    }
    if versions.len() != 1 {
        return Err("Applied market defaults have duplicate versions".into());
    }
    let version = versions[0];
    let historical;
    let values = if version["Parameters"].is_null() {
        historical = serde_json::from_str::<Json>(
            version["PayloadJson"]
                .as_str()
                .ok_or("Missing historical market defaults")?,
        )
        .map_err(|_| "Invalid historical market defaults")?;
        &historical
    } else {
        &version["Parameters"]["Value"]
    };
    let rows: Vec<_> = values["Symbols"]
        .as_array()
        .ok_or("Missing market-default symbols")?
        .iter()
        .filter(|v| {
            v["Symbol"]
                .as_str()
                .is_some_and(|v| v.eq_ignore_ascii_case(symbol))
        })
        .collect();
    if rows.len() != 1 {
        return Err("Applied market defaults require exactly one matching symbol".into());
    }
    let row = rows[0];
    Ok(Some(if condor {
        Defaults {
            call_delta: number(row, "ShortCallDelta")?,
            put_delta: number(row, "ShortPutDelta")?,
            call_width: number(row, "CallSpreadWidth")?,
            put_width: number(row, "PutSpreadWidth")?,
        }
    } else {
        Defaults {
            call_delta: number(row, "ShortLegDelta")?,
            put_delta: number(row, "ShortLegDelta")?,
            call_width: number(row, "SpreadWidth")?,
            put_width: number(row, "SpreadWidth")?,
        }
    }))
}
pub struct Market {
    pub lease: option_chain::Lease,
    pub draft: option_draft::Draft,
    pub expirations: Vec<Json>,
    pub expiry: chrono::NaiveDate,
    pub defaults: Option<Defaults>,
    pub underlying: Json,
    pub strategy: String,
    pub live: bool,
    pub editable: bool,
    pub development: bool,
    pub status: String,
}
impl Market {
    pub fn new(
        underlying: Json,
        strategy: String,
        context: &BusinessContext,
        editable: bool,
        development: bool,
    ) -> Self {
        Self {
            lease: Default::default(),
            draft: Default::default(),
            expirations: vec![],
            expiry: context.value_date,
            defaults: None,
            underlying,
            strategy,
            live: false,
            editable,
            development,
            status: String::new(),
        }
    }
    pub async fn initialize(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        requested: chrono::NaiveDate,
        through: chrono::NaiveDate,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let wire = parameters::query(
            backend,
            registry,
            "GetParameterStartupRunsQuery",
            BTreeMap::new(),
            deadline,
        )
        .await?;
        let runs = registry.project(&parameters::kind("ParameterStartupRun[]"), &wire)?;
        self.defaults = applied_defaults(
            runs.as_array()
                .filter(|r| r.len() <= 20)
                .ok_or("Invalid applied startup runs")?,
            &self.strategy,
            self.underlying["Symbol"]
                .as_str()
                .ok_or("Missing underlying symbol")?,
        )?;
        self.status=if self.defaults.is_none(){"No applied startup defaults are available for this strategy".into()}else{String::new()};
        self.expirations = option_chain::expirations(
            backend,
            registry,
            self.underlying["Symbol"].as_str().unwrap(),
            context.value_date,
            through,
            requested,
            deadline,
        )
        .await?;
        if let Some(first) = self.expirations.first() {
            self.expiry = crate::backend::date_input::parse(
                first["ExpiryDate"].as_str().ok_or("Missing expiry")?,
            )?;
            self.refresh(backend, registry, deadline).await?;
        }
        Ok(())
    }
    pub async fn refresh(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        // Preserve the hybrid's initial 16/50 display inputs when configuration
        // is missing, but never enable automatic selection or order entry from
        // these placeholders. The raw market grid is still useful read-only.
        let placeholder=Defaults{call_delta:ExactDecimal::parse("16")?,put_delta:ExactDecimal::parse("16")?,call_width:ExactDecimal::parse("50")?,put_width:ExactDecimal::parse("50")?};
        let defaults=self.defaults.as_ref().unwrap_or(&placeholder);
        let contract = self.underlying["ContractId"]
            .as_str()
            .ok_or("Missing underlying identity")?;
        let symbol = self.underlying["Symbol"]
            .as_str()
            .ok_or("Missing underlying symbol")?;
        let width = if super::option_selection::compare(&defaults.call_width, &defaults.put_width)
            .is_ge()
        {
            &defaults.call_width
        } else {
            &defaults.put_width
        };
        let roots: std::collections::BTreeSet<_> = self
            .expirations
            .iter()
            .filter(|r| r["ExpiryDate"] == self.expiry.to_string())
            .filter_map(|r| r["ProviderRoot"].as_str().map(str::to_string))
            .collect();
        let spec = option_chain::Specification {
            contract: contract.into(),
            symbol: symbol.into(),
            expiry: self.expiry,
            roots: roots.into_iter().collect(),
            required: self
                .draft
                .legs
                .iter()
                .map(|l| l.quote.contract.clone())
                .collect(),
            wing_width: width.clone(),
            frozen: !self.live && self.development,
        };
        let incoming = self.lease.load(backend, registry, spec, deadline).await?;
        let multiplier = number(&self.underlying, "Multiplier")?;
        self.draft.accept(
            incoming,
            &option_draft::Settings {
                strategy: &self.strategy,
                call_delta: &defaults.call_delta,
                put_delta: &defaults.put_delta,
                call_width: &defaults.call_width,
                put_width: &defaults.put_width,
                multiplier: &multiplier,
                live: self.live,
                development: self.development,
                editable: self.editable && self.defaults.is_some(),
            },
        )
    }
    pub async fn change(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        expiry: chrono::NaiveDate,
        live: bool,
        defaults: Defaults,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        if !self.editable {
            return Err("Market selection is read-only".into());
        }
        if self.defaults.is_none(){return Err("Applied startup defaults are unavailable; configure them and reopen the Blotter".into());}
        let multiplier = number(&self.underlying, "Multiplier")?;
        option_draft::validate(&option_draft::Settings {
            strategy: &self.strategy,
            call_delta: &defaults.call_delta,
            put_delta: &defaults.put_delta,
            call_width: &defaults.call_width,
            put_width: &defaults.put_width,
            multiplier: &multiplier,
            live,
            development: self.development,
            editable: self.editable,
        })?;
        if !self
            .expirations
            .iter()
            .any(|r| r["ExpiryDate"] == expiry.to_string())
        {
            return Err("Select an available expiry".into());
        }
        if live
            && context.market_state != 2
            && !(self.development && context.allow_development_emulator)
        {
            return Err("Live Feed is unavailable outside the permitted trading session or Development emulator".into());
        }
        if expiry != self.expiry || live != self.live {
            self.lease.release(backend, registry, deadline).await?;
            self.draft = Default::default();
        }
        self.expiry = expiry;
        self.live = live;
        self.defaults = Some(defaults);
        self.refresh(backend, registry, deadline).await
    }
    pub async fn close(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        self.lease.release(backend, registry, deadline).await?;
        self.draft = Default::default();
        Ok(())
    }
}
