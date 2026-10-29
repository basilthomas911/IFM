//! Dataset-owned current-futures discovery. Definition HTTP, current-contract
//! selection and live-plan materialization share the supervisor's original
//! recovery deadline; no managed lookup/lease coordinator is required.
use super::{
    live::{Registration, Start},
    transport::{Lane, SessionPlan, SubscriptionSpec},
};
use crate::{
    catalog::{Catalog, Definition, current},
    historical::History,
    latest::Asset,
    runtime::Deadline,
};
use databento::dbn::{SType, Schema};
use std::sync::Arc;
use tokio_util::sync::CancellationToken;
/// One configured futures root and required front/term structure coverage.
#[derive(Clone)]
pub struct Root {
    /// Canonical configured root.
    pub symbol: String,
    /// Number of eligible futures, not a calendar-month guess.
    pub count: usize,
    /// Existing explicit currency fallback.
    pub fallback: Option<String>,
    /// Existing futures VWAP publication selection.
    pub vwap: bool,
}
/// Immutable raw configuration for rebuilding a dataset from provider metadata.
#[derive(Clone)]
pub struct Discovery {
    /// Native cancellable provider HTTP configuration, with no managed callbacks.
    pub history: Arc<History>,
    /// Complete desired root set for this dataset.
    pub roots: Vec<Root>,
    /// Exact committed dependencies, qualified against native provider facts.
    /// This list is accepted from the native business journal, never inferred
    /// by selecting today's front contract for an older fixed position.
    pub fixed: Vec<crate::option_chain::committed::Route>,
    /// Rebuild trade/statistics replay from the API ValueDate's session start.
    pub session_replay: bool,
}
impl Discovery {
    /// Validates command capacity without network I/O.
    ///
    /// # Errors
    /// Invalid roots, duplicate roots or unsupported dataset-role coverage.
    pub fn validate(&self, lane: Lane) -> Result<(), &'static str> {
        if lane == Lane::OptionsGlbx || self.roots.is_empty() || self.roots.len() > 16 {
            return Err("Invalid futures bootstrap roots");
        }
        let mut seen = std::collections::BTreeSet::new();
        let mut total = 0usize;
        for root in &self.roots {
            if root.symbol.is_empty()
                || root.symbol.len() > 60
                || !root
                    .symbol
                    .bytes()
                    .all(|v| v.is_ascii_uppercase() || v.is_ascii_digit())
                || !(1..=64).contains(&root.count)
                || !seen.insert(&root.symbol)
                || root.fallback.as_ref().is_some_and(|c| c.len() > 32)
            {
                return Err("Invalid futures bootstrap root");
            }
            total += root.count;
        }
        if total > 64 {
            return Err("Futures bootstrap contract capacity exceeded");
        }
        if self.fixed.len() > 128 {
            return Err("Fixed futures route capacity exceeded");
        }
        let mut contracts = std::collections::BTreeSet::new();
        for route in &self.fixed {
            if route.contract.trim().is_empty()
                || route.contract.len() > 256
                || route.symbol.trim().is_empty()
                || route.symbol.len() > 128
                || route.root.is_empty()
                || !contracts.insert(&route.contract)
                || !self.roots.iter().any(|root| root.symbol == route.root)
            {
                return Err("Invalid fixed futures reconstruction route");
            }
        }
        Ok(())
    }
    /// Acquires raw definitions and builds the complete live plan inside its dataset
    /// child. A stalled provider is cancelled by the original owner deadline.
    ///
    /// # Errors
    /// Provider, cancellation, missing reference or conflicting identity failures.
    pub async fn resolve(
        &self,
        template: &Start,
        date: time::Date,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Start, String> {
        self.resolve_with_contracts(template, date, stop, deadline)
            .await
            .map(|v| v.0)
    }
    /// Resolves the source plan and exact existing thirty-two-key contract DTOs
    /// together. The detached DTO directory lets startup/read queries reuse the
    /// accepted discovery, without a second managed provider lookup.
    ///
    /// # Errors
    /// The same bounded source/reference/cancellation failures as `resolve`.
    pub async fn resolve_with_contracts(
        &self,
        template: &Start,
        date: time::Date,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(Start, Vec<rmpv::Value>), String> {
        self.resolve_with_models(template, date, stop, deadline)
            .await
            .map(|(source, current, _)| (source, current))
    }
    /// Resolves current/rollover DTOs separately from public configured-catalog
    /// DTOs. Both use the same acquired provider definitions; no second lookup
    /// or managed metadata reconstruction is required.
    ///
    /// # Errors
    /// Invalid provider data, projection failure or original deadline expiry.
    pub async fn resolve_with_models(
        &self,
        template: &Start,
        date: time::Date,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(Start, Vec<rmpv::Value>, Vec<rmpv::Value>), String> {
        self.validate(template.plan.lane)?;
        let mut public_models = Vec::new();
        let mut registrations = Vec::new();
        let mut contracts = Vec::new();
        for root in &self.roots {
            let mut catalog = Catalog::new(100000, 64 * 1024 * 1024)
                .map_err(|_| "Futures definition capacity invalid")?;
            self.history
                .latest_definitions(
                    template.plan.lane.dataset(),
                    &[format!("{}.FUT", root.symbol)],
                    stop,
                    deadline,
                    |line| {
                        let definition = Definition::parse(template.plan.lane.dataset(), line)
                            .map_err(|_| "Invalid native futures definition".to_string())?;
                        catalog
                            .insert(definition)
                            .map_err(|_| "Native futures definition capacity exceeded".to_string())
                    },
                )
                .await
                .map_err(|e| format!("Native futures definition acquisition failed: {e:?}"))?;
            let rows: Vec<_> = catalog.definitions().collect();
            let selected = current::select(
                &rows,
                &root.symbol,
                date,
                root.count,
                root.fallback.as_deref(),
            )?;
            if let Some(code) = selected.failure {
                return Err(format!("{code}: {}", selected.identity));
            }
            registrations.extend(materialize_selected(&rows, root, &selected.contracts)?);
            let fixed = self
                .fixed
                .iter()
                .filter(|route| route.root == root.symbol)
                .cloned()
                .collect::<Vec<_>>();
            registrations.extend(materialize_fixed(&rows, &fixed, root.vwap, stop, deadline)?);
            public_models.extend(public_models_selected(&rows, root, &selected.contracts)?);
            contracts.extend(selected.contracts);
        }
        // Identical front/term and fixed demand share one physical mapping.
        // Conflicting aliases/mappings remain errors, never last-writer wins.
        let mut canonical = std::collections::BTreeMap::new();
        for row in registrations {
            if let Some(prior) = canonical.insert(row.contract.clone(), row.clone())
                && prior != row
            {
                return Err("Conflicting configured/fixed futures mapping".into());
            }
        }
        let registrations = canonical.into_values().collect::<Vec<_>>();
        let unique: std::collections::BTreeSet<_> =
            registrations.iter().map(|r| r.instrument).collect();
        if unique.len() != registrations.len() {
            return Err("Conflicting native futures source identities".into());
        }
        let symbols = registrations
            .iter()
            .map(|r| r.source.clone())
            .collect::<Vec<_>>();
        let replay = if self.session_replay {
            Some(session_start(date)?)
        } else {
            None
        };
        let subscriptions = template
            .plan
            .subscriptions
            .iter()
            .map(|s| SubscriptionSpec {
                symbols: symbols.clone(),
                schema: s.schema,
                symbology: SType::RawSymbol,
                replay_start_ns: if matches!(s.schema, Schema::Trades | Schema::Statistics) {
                    replay
                } else {
                    None
                },
            })
            .collect();
        let source = Start {
            plan: SessionPlan {
                lane: template.plan.lane,
                subscriptions,
            },
            registrations,
            key: template.key.clone(),
            gateway: template.gateway.clone(),
            evidence: template.evidence,
        };
        source
            .plan
            .validate()
            .map_err(|e| format!("Invalid native bootstrap subscription: {e:?}"))?;
        Ok((source, contracts, public_models))
    }
}
/// Materializes fixed committed identities without front-contract substitution.
/// Source facts must contain one exact, live futures symbol for the stated root.
///
/// # Errors
/// Missing/conflicting/deleted source, invalid route or original cancellation.
pub fn materialize_fixed(
    rows: &[Arc<Definition>],
    routes: &[crate::option_chain::committed::Route],
    vwap: bool,
    stop: &CancellationToken,
    deadline: Deadline,
) -> Result<Vec<Registration>, String> {
    if routes.len() > 128 {
        return Err("Fixed futures route capacity exceeded".into());
    }
    let mut registrations = Vec::new();
    let mut seen = std::collections::BTreeSet::new();
    for route in routes {
        if stop.is_cancelled() || deadline.remaining().is_zero() {
            return Err("Fixed futures original deadline/cancellation".into());
        }
        if route.contract.trim().is_empty()
            || route.contract.len() > 256
            || route.symbol.trim().is_empty()
            || route.symbol.len() > 128
            || !seen.insert(&route.contract)
        {
            return Err("Invalid/duplicate fixed futures route".into());
        }
        let mut matching = rows.iter().filter(|row| {
            row.dataset == "GLBX.MDP3"
                && row.symbol == route.symbol
                && row.asset == route.root
                && row.kind == 1
        });
        let row = matching
            .next()
            .ok_or("Exact fixed futures definition unavailable")?;
        if matching.next().is_some() || row.deleted || row.instrument == 0 || row.publisher == 0 {
            return Err("Conflicting/unusable fixed futures definition".into());
        }
        registrations.push(Registration {
            instrument: row.instrument,
            publisher: row.publisher,
            contract: route.contract.clone(),
            source: route.symbol.clone(),
            asset: Asset::Futures,
            tick: row.tick.filter(|tick| *tick > 0),
            vwap,
            required: true,
        });
    }
    Ok(registrations)
}
/// Exact provider mappings for a current-contract result. The same native
/// resolver used by copied queries selects the roots and term structure.
///
/// # Errors
/// Missing contracts, conflicting definition identity or cancellation.
pub fn materialize(
    rows: &[Arc<Definition>],
    root: &Root,
    date: time::Date,
    stop: &CancellationToken,
    deadline: Deadline,
) -> Result<Vec<Registration>, String> {
    if stop.is_cancelled() || deadline.remaining().is_zero() {
        return Err("Futures bootstrap original deadline expired".into());
    }
    let result = current::select(
        rows,
        &root.symbol,
        date,
        root.count,
        root.fallback.as_deref(),
    )?;
    if let Some(code) = result.failure {
        return Err(format!("{code}: {}", result.identity));
    }
    materialize_selected(rows, root, &result.contracts)
}
/// Projects the selected current-contract DTOs exactly as the existing
/// configured catalog does, while preserving the resolver DTOs independently.
///
/// # Errors
/// Missing selected source or a configured public mapping failure.
pub fn public_models_selected(
    rows: &[Arc<Definition>],
    root: &Root,
    contracts: &[rmpv::Value],
) -> Result<Vec<rmpv::Value>, String> {
    contracts
        .iter()
        .map(|model| {
            let fields = model
                .as_array()
                .ok_or("Malformed native current-contract model")?;
            let id = fields[0]
                .as_str()
                .ok_or("Missing current-contract identity")?;
            let raw = fields[3]
                .as_str()
                .ok_or("Missing current-contract source")?;
            let row = rows
                .iter()
                .find(|d| d.kind == 1 && d.symbol == raw)
                .ok_or("Missing selected catalog source")?;
            crate::catalog::option_models::future(
                row,
                id,
                root.fallback.as_deref(),
                fields[9].as_bool().ok_or("Invalid on-the-run flag")?,
                fields[10].as_bool().ok_or("Invalid rollover flag")?,
            )
            .map_err(Into::into)
        })
        .collect()
}
fn materialize_selected(
    rows: &[Arc<Definition>],
    root: &Root,
    contracts: &[rmpv::Value],
) -> Result<Vec<Registration>, String> {
    let mut registrations = Vec::new();
    for contract in contracts {
        let fields = contract
            .as_array()
            .ok_or("Malformed native current-contract projection")?;
        let id = fields[0]
            .as_str()
            .ok_or("Missing native canonical futures ID")?;
        let raw = fields[3].as_str().ok_or("Missing native futures source")?;
        let row = rows
            .iter()
            .find(|row| row.kind == 1 && row.symbol == raw)
            .ok_or("Selected futures source definition unavailable")?;
        if row.instrument == 0 || row.publisher == 0 || row.deleted {
            return Err("Unusable native current-futures source identity".into());
        }
        registrations.push(Registration {
            instrument: row.instrument,
            publisher: row.publisher,
            contract: id.into(),
            source: raw.into(),
            asset: Asset::Futures,
            tick: row.tick.filter(|t| *t > 0),
            vwap: root.vwap,
            required: true,
        });
    }
    Ok(registrations)
}
/// Existing 18:00 US Eastern session start for an explicit API ValueDate. Pinned
/// timezone rules handle DST; no machine timezone or local clock participates.
///
/// # Errors
/// Unsupported calendar dates, local-zone ambiguity or DBN timestamp overflow.
pub fn session_start(date: time::Date) -> Result<i64, &'static str> {
    use chrono::TimeZone;
    let prior = date
        .previous_day()
        .filter(|d| (1..=9999).contains(&d.year()))
        .ok_or("Invalid futures replay date")?;
    let local = chrono_tz::America::New_York
        .with_ymd_and_hms(
            prior.year(),
            prior.month() as u32,
            prior.day() as u32,
            18,
            0,
            0,
        )
        .single()
        .ok_or("Invalid futures replay timezone instant")?;
    let ns = local
        .timestamp()
        .checked_mul(1_000_000_000)
        .filter(|n| *n >= 0)
        .ok_or("Futures replay outside DBN timestamp range")?;
    Ok(ns)
}
