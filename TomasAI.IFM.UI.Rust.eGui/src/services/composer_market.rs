//! Market-selection lifecycle for a single native Blotter incarnation. Applied
//! startup defaults and business dates are API inputs; there is no local clock,
//! pricing engine or global subscription stop. Development emulator sessions
//! can start with explicit editable delta/width inputs without an applied run.
use super::{business_context::BusinessContext, option_chain, option_draft, parameters};
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use crate::models::trade_contracts::{FuturesContractV3ReadModel, OptionContractExpiryReadModel};
use crate::models::wire_types::FromBackend;
use serde_json::Value as Json;
use std::collections::BTreeMap;

/// Finds the next listed futures cycle regardless of the Fund's forward-cycle setting.
pub fn next_contract_expiry(base: &FuturesContractV3ReadModel, catalog: &[FuturesContractV3ReadModel])
    -> Result<chrono::NaiveDate, String> {
    let maturity = base.last_trade_date.as_ref().ok_or("Base contract has no expiry")?;
    let maturity = crate::backend::date_input::parse(maturity.as_str())?;
    catalog.iter().filter(|row| row.symbol == base.symbol && row.exchange == base.exchange)
        .filter_map(|row| row.last_trade_date.as_ref()?.as_str().parse::<chrono::NaiveDate>().ok())
        .filter(|date| *date > maturity).min().ok_or("No next futures contract is available".into())
}

/// Groups provider roots by actual futures identity, retaining one selection per series.
fn expiry_choices(rows: &[OptionContractExpiryReadModel]) -> BTreeMap<String, (chrono::NaiveDate, String)> {
    let mut by_date = BTreeMap::<_, std::collections::BTreeSet<_>>::new();
    for row in rows {
        if let (Some(date), Some(contract)) = (&row.expiry_date, &row.contract_id)
            && let Ok(date) = date.as_str().parse::<chrono::NaiveDate>() {
            by_date.entry(date).or_default().insert(contract.clone());
        }
    }
    by_date.into_iter().flat_map(|(date, contracts)| {
        let ambiguous = contracts.len() > 1;
        contracts.into_iter().map(move |contract| {
            let label = if ambiguous { format!("{date} / {contract}") } else { date.to_string() };
            (label, (date, contract))
        })
    }).collect()
}

/// Defaults used by the composer market component.
pub struct Defaults {
    /// Call delta associated with this defaults.
    pub call_delta: ExactDecimal,
    /// Put delta associated with this defaults.
    pub put_delta: ExactDecimal,
    /// Call width associated with this defaults.
    pub call_width: ExactDecimal,
    /// Put width associated with this defaults.
    pub put_width: ExactDecimal,
}
impl Defaults {
    /// Provides emulator for the defaults implementation.
    pub fn emulator() -> Result<Self, String> {
        Ok(Self {
            call_delta: ExactDecimal::parse("16")?,
            put_delta: ExactDecimal::parse("16")?,
            call_width: ExactDecimal::parse("50")?,
            put_width: ExactDecimal::parse("50")?,
        })
    }
}

/// Reads the numeric representation supported by the supplied value.
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
    if rows.is_empty() {
        return Ok(None);
    }
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

/// Market used by the composer market component.
pub struct Market {
    /// Lease associated with this market.
    pub lease: option_chain::Lease,
    /// Draft associated with this market.
    pub draft: option_draft::Draft,
    /// Ordered expirations retained by Market.
    pub expirations: Vec<OptionContractExpiryReadModel>,
    /// Expiry associated with this market.
    pub expiry: Option<chrono::NaiveDate>,
    /// The proposed entry date. Established trades derive their date from execution.
    pub trade_date: chrono::NaiveDate,
    /// Last selectable expiry based on the original base contract's next cycle.
    expiry_through: Option<chrono::NaiveDate>,
    /// A close must retain the opening position's expiry.
    pub fixed_expiry: Option<chrono::NaiveDate>,
    /// Optional defaults; `None` represents an absent value.
    pub defaults: Option<Defaults>,
    /// Underlying associated with this market.
    pub underlying: FuturesContractV3ReadModel,
    /// Strategy associated with this market.
    pub strategy: String,
    /// Whether live is enabled or currently true.
    pub live: bool,
    /// Whether editable is enabled or currently true.
    pub editable: bool,
    /// Whether development is enabled or currently true.
    pub development: bool,
    /// Current status text or state exposed by this component.
    pub status: String,
}
impl Market {
    /// Constructs Market from the supplied inputs.
    pub fn new(
        underlying: FuturesContractV3ReadModel,
        strategy: String,
        context: &BusinessContext,
        editable: bool,
        development: bool,
    ) -> Self {
        Self {
            lease: Default::default(),
            draft: Default::default(),
            expirations: vec![],
            expiry: (strategy == "FuturesOutright").then_some(context.value_date),
            trade_date: context.value_date,
            expiry_through: None,
            fixed_expiry: None,
            defaults: None,
            underlying,
            strategy,
            live: false,
            editable,
            development,
            status: String::new(),
        }
    }

    /// Provides initialize for the market implementation.
    pub async fn initialize(
        &mut self,
        backend: &Backend,
        registry: &Registry,
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
            self.underlying
                .symbol
                .as_deref()
                .ok_or("Missing underlying symbol")?,
        )?;
        self.status = if self.defaults.is_none() && self.development {
            self.defaults = Some(Defaults::emulator()?);
            "Development emulator: editable delta and width inputs".into()
        } else if self.defaults.is_none() {
            "No applied startup defaults are available for this strategy".into()
        } else {
            String::new()
        };
        if let Some(expiry) = self.fixed_expiry {
            self.expirations = self.read_expirations(backend, registry, expiry, expiry, deadline).await?;
            let contract = self.underlying.contract_id.as_deref();
            self.expirations.retain(|row| row.contract_id.as_deref() == contract);
            if self.expirations.is_empty() {
                return Err("The opening trade's expiry is unavailable; a closing order cannot use another expiry".into());
            }
            self.expiry = Some(expiry);
            self.refresh(backend, registry, deadline).await?;
        } else if self.editable {
            let wire = tokio::time::timeout_at(deadline, super::actor_requests::query(
                backend, registry, "TomasAI.IFM.Domain.MarketData.Shared.Queries.GetFuturesContractsQuery",
                "none", BTreeMap::new(),
            )).await.map_err(|_| "Futures catalog deadline expired")??;
            let catalog = Vec::<FuturesContractV3ReadModel>::from_backend(&wire)?;
            self.expiry_through = Some(next_contract_expiry(&self.underlying, &catalog)?);
            self.expirations = self.read_expirations(backend, registry, self.trade_date,
                self.expiry_through.unwrap(), deadline).await?;
        }
        Ok(())
    }

    /// Refresh the calendar without loading a chain for a cleared selection.
    pub async fn change_trade_date(&mut self, backend: &Backend, registry: &Registry,
        date: chrono::NaiveDate, deadline: tokio::time::Instant) -> Result<(), String> {
        if !self.editable {
            return Err("Trade Date is read-only".into());
        }
        if let Some(through) = self.expiry_through {
            let choices = self.read_expirations(backend, registry, date, through, deadline).await?;
            if self.expiry.is_some_and(|expiry| !choices.iter().any(|row|
                row.expiry_date.as_ref().is_some_and(|value| value.as_str() == expiry.to_string())
                && row.contract_id == self.underlying.contract_id)) {
                self.lease.release(backend, registry, deadline).await?;
                self.expiry = None;
                self.draft = Default::default();
            }
            self.expirations = choices;
        } else if self.fixed_expiry.is_some_and(|expiry| date > expiry) {
            return Err("Trade Date cannot follow the opening position's expiry".into());
        }
        self.trade_date = date;
        Ok(())
    }

    async fn read_expirations(&self, backend: &Backend, registry: &Registry,
        from: chrono::NaiveDate, through: chrono::NaiveDate,
        deadline: tokio::time::Instant) -> Result<Vec<OptionContractExpiryReadModel>, String> {
        if from > through { return Ok(vec![]); }
        let symbol = self.underlying.symbol.as_deref().ok_or("Missing underlying symbol")?;
        option_chain::expirations(backend, registry, symbol, from, through, from, deadline).await
    }

    /// The picker disambiguates listed series that share a date across futures contracts.
    pub fn expiry_choices(&self) -> BTreeMap<String, (chrono::NaiveDate, String)> {
        expiry_choices(&self.expirations)
    }

    pub fn expiry_label(&self) -> String {
        let Some(expiry) = self.expiry else { return String::new(); };
        self.expiry_choices().into_iter().find(|(_, (date, contract))|
            *date == expiry && self.underlying.contract_id.as_ref() == Some(contract))
            .map(|(label, _)| label).unwrap_or_else(|| expiry.to_string())
    }

    /// Refreshes composer market for market.
    pub async fn refresh(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let Some(expiry) = self.expiry else { return Ok(()); };
        // Preserve the hybrid's initial 16/50 display inputs when configuration
        // is missing, but never enable automatic selection or order entry from
        // these placeholders. The raw market grid is still useful read-only.
        let placeholder = Defaults {
            call_delta: ExactDecimal::parse("16")?,
            put_delta: ExactDecimal::parse("16")?,
            call_width: ExactDecimal::parse("50")?,
            put_width: ExactDecimal::parse("50")?,
        };
        let defaults = self.defaults.as_ref().unwrap_or(&placeholder);
        let contract = self
            .underlying
            .contract_id
            .as_deref()
            .ok_or("Missing underlying identity")?;
        let symbol = self
            .underlying
            .symbol
            .as_deref()
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
            .filter(|r| {
                r.expiry_date.as_ref().map(|value| value.as_str())
                    == Some(expiry.to_string().as_str())
                    && r.contract_id.as_deref() == Some(contract)
            })
            .filter_map(|r| r.provider_root.clone())
            .collect();
        let spec = option_chain::Specification {
            contract: contract.into(),
            symbol: symbol.into(),
            expiry,
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
        self.draft.accept(
            incoming,
            &option_draft::Settings {
                strategy: &self.strategy,
                call_delta: &defaults.call_delta,
                put_delta: &defaults.put_delta,
                call_width: &defaults.call_width,
                put_width: &defaults.put_width,
                live: self.live,
                development: self.development,
                editable: self.editable && self.defaults.is_some(),
            },
        )
    }

    /// Selecting display liquidity does not require an editable order or applied
    /// strategy defaults. Missing defaults still prohibit order entry.
    pub async fn select_source(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        live: bool,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        if !self.development {
            return Err("Emulator liquidity is available only in Development".into());
        }
        if live != self.live {
            self.lease.release(backend, registry, deadline).await?;
            self.draft.raw = None;
            self.draft.clear_live_quotes();
            self.draft.entered_price = None;
        }
        self.live = live;
        self.refresh(backend, registry, deadline).await
    }

    /// Provides change for the market implementation.
    pub async fn change(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        context: &BusinessContext,
        selection: &str,
        live: bool,
        defaults: Defaults,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let (expiry, contract) = if selection.is_empty() {
            (None, self.underlying.contract_id.clone().ok_or("Missing underlying identity")?)
        } else {
            let (expiry, contract) = self.expiry_choices().remove(selection).ok_or("Select an available expiry")?;
            (Some(expiry), contract)
        };
        if self.fixed_expiry.is_some_and(|fixed| Some(fixed) != expiry) {
            return Err("A closing order must use the opening trade's expiry".into());
        }
        if !self.editable {
            return Err("Market selection is read-only".into());
        }
        if self.defaults.is_none() && self.fixed_expiry.is_none() {
            return Err(
                "Applied startup defaults are unavailable; configure them and reopen the Blotter"
                    .into(),
            );
        }
        let underlying = if self.underlying.contract_id.as_deref() == Some(contract.as_str()) {
            self.underlying.clone()
        } else {
            super::broker_initialization::basic::<FuturesContractV3ReadModel>(
                backend, registry, "TomasAI.IFM.Domain.MarketData.Shared.Queries.GetFuturesContractQuery",
                &contract, BTreeMap::from([("ContractId".into(), contract.as_str().into())]), deadline,
            ).await?.filter(|row| row.contract_id.as_deref() == Some(contract.as_str())
                && row.symbol == self.underlying.symbol).ok_or("Selected option series underlying is unavailable")?
        };
        option_draft::validate(&option_draft::Settings {
            strategy: &self.strategy,
            call_delta: &defaults.call_delta,
            put_delta: &defaults.put_delta,
            call_width: &defaults.call_width,
            put_width: &defaults.put_width,
            live,
            development: self.development,
            editable: self.editable,
        })?;
        if live
            && context.market_state != 2
            && !(self.development && context.allow_development_emulator)
        {
            return Err("Live Feed is unavailable outside the permitted trading session or Development emulator".into());
        }
        let series_changed = expiry != self.expiry || underlying.contract_id != self.underlying.contract_id;
        if series_changed || live != self.live {
            self.lease.release(backend, registry, deadline).await?;
            if self.fixed_expiry.is_none() && series_changed {
                self.draft = Default::default();
            } else {
                self.draft.raw = None;
                self.draft.clear_live_quotes();
            }
            self.draft.entered_price = None;
        }
        self.expiry = expiry;
        self.underlying = underlying;
        self.live = live;
        self.defaults = Some(defaults);
        self.refresh(backend, registry, deadline).await
    }

    /// Closes composer market for market.
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
