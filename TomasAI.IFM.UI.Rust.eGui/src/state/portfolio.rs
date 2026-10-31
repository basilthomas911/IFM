//! Native Portfolio workspace owner. Display documents use copied screen
//! contracts; immutable backend DTOs and revisions remain separate from cells.
use crate::models::portfolio::{Choice, Dialog, Document, Field, Grid};
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    contract_editor::{ContractPublication, ContractResult},
    lookup_editor::EditorOutcome,
    services::{financial, portfolio as api, strategy_display as display},
    state::portfolio_data::{PortfolioData, fund_key, kind},
};
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
mod editors;
mod financial_data;
mod financial_grid;
mod financial_ui;
mod fund_editors;
mod ledger_ui;
mod policy;
mod posting_ui;
mod risk_history;
#[derive(Default)]
pub struct PortfolioController {
    pub data: PortfolioData,
    from: Option<chrono::NaiveDate>,
    through: Option<chrono::NaiveDate>,
    metrics: Option<Value>,
    policies: Vec<Value>,
    deployments: Vec<Json>,
    selected_policy: Option<String>,
    editing_policy: Option<Value>,
    policy_version: bool,
    policy_dirty: bool,
    policy_key: Option<uuid::Uuid>,
    requires_reload: bool,
    write_in_flight: bool,
    accepted_write: bool,
    dialogs: Vec<Dialog>,
    editing: Option<(String, Value)>,
    edit_key: Option<uuid::Uuid>,
    selections: Option<crate::services::fund_selections::Selections>,
    pending_attachment: Option<crate::services::portfolio_commands::Prepared>,
    attachment_rejected: bool,
    ledger_data: Option<financial_data::LedgerData>,
    pending_postings: Vec<Json>,
    journal_root: Option<std::path::PathBuf>,
    posting_configuration: Option<financial::Read>,
    related_transaction: Option<Value>,
    related_receipt: Option<Value>,
    pending_posting: Option<Json>,
    ledger_configuration: Option<financial::Read>,
    pending_configuration: Option<Json>,
    pending_configurations: Vec<Json>,
    book_draft: Option<Value>,
    authority_draft: Option<financial::Read>,
    editing_account: Option<i32>,
    editing_rule: Option<uuid::Uuid>,
    inline_fund_view: Option<String>,
    risk_rows: Vec<Value>,
    risk_cursor: Option<String>,
    risk_date: Option<chrono::NaiveDate>,
    generation: u64,
    receipts: BTreeMap<String, (Json, EditorOutcome)>,
    message: String,
}
fn field<'a>(intent: &'a Json, name: &str) -> &'a str {
    intent["fields"][name].as_str().unwrap_or_default()
}
fn text(value: &Json) -> String {
    value.as_str().map(str::to_string).unwrap_or_else(|| {
        if value.is_null() {
            String::new()
        } else {
            value.to_string()
        }
    })
}
fn cell_row(
    id: impl Into<String>,
    fields: BTreeMap<String, String>,
) -> crate::contract_editor::ContractRow {
    crate::contract_editor::ContractRow {
        id: id.into(),
        fields: fields,
        data: None,
        ..Default::default()
    }
}
fn grid(
    key: &str,
    columns: Vec<String>,
    rows: Vec<crate::contract_editor::ContractRow>,
    selected: Option<String>,
) -> Grid {
    Grid {
        key: (key).to_string(),
        columns: (columns).into_iter().map(|v| v.to_string()).collect(),
        rows: rows,
        selected: selected,
        editable: false,
        ..Default::default()
    }
}
impl PortfolioController {
    /// Isolated qualification can supply a journal directory without touching
    /// the operator's real receipts. Production uses the existing per-user store.
    pub fn with_journal_root(root: std::path::PathBuf) -> Self {
        Self {
            journal_root: Some(root),
            ..Default::default()
        }
    }
    async fn load_metrics(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        self.metrics = None;
        let Some(fund) = &self.data.fund else {
            return Ok(());
        };
        let row = registry.project(&kind("FundMandateReadModel"), fund)?;
        let portfolio = i32::try_from(
            row["PortfolioId"]
                .as_i64()
                .ok_or("Invalid Portfolio identity")?,
        )
        .map_err(|_| "Portfolio identity overflow")?;
        let fund = i32::try_from(row["FundId"].as_i64().ok_or("Invalid Fund identity")?)
            .map_err(|_| "Fund identity overflow")?;
        let from = self.from.ok_or("Load API date first")?;
        let through = self.through.ok_or("Load API date first")?;
        if from > through {
            return Err("Metrics: choose a valid date range".into());
        }
        let read = financial::query(
            backend,
            registry,
            self.data.context.as_ref().ok_or("Load API context")?,
            "GetFundPerformanceMetricsQuery",
            portfolio,
            Some(fund),
            Value::Array(vec![date_only_wire(from), date_only_wire(through)]),
            financial::AccessScope::MetricsReader,
            deadline,
        )
        .await?;
        let wire = read.found()?;
        let report =
            registry.project(&format!("{}.FundPerformanceMetrics", financial::ROOT), wire)?;
        if report["From"] != from.to_string() || report["Through"] != through.to_string() {
            return Err("Financial metrics crossed the requested accounting range".into());
        }
        self.metrics = Some(wire.clone());
        Ok(())
    }
    async fn load_policies(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        self.policies.clear();
        let Some(portfolio) = &self.data.portfolio else {
            return Ok(());
        };
        let row = registry.project(&kind("PortfolioReadModel"), portfolio)?;
        self.deployments =
            crate::services::portfolio_reference::deployments(backend, registry, deadline).await?;
        let id = row["PortfolioId"]
            .as_i64()
            .ok_or("Invalid Portfolio identity")?;
        let wire = api::query(
            backend,
            registry,
            self.data.context.as_ref().ok_or("Load API context")?,
            "GetPortfolioFinancialPoliciesQuery",
            &id.to_string(),
            BTreeMap::from([
                ("PortfolioId".into(), id.into()),
                ("PageSize".into(), 200.into()),
            ]),
            deadline,
        )
        .await?;
        let rows = wire
            .as_array()
            .and_then(|v| v.first())
            .and_then(Value::as_array)
            .filter(|v| v.len() <= 200)
            .ok_or("Invalid Portfolio policy page")?;
        for row in rows {
            let model = registry.project(&kind("PortfolioFinancialPolicyReadModel"), row)?;
            if model["PortfolioId"] != id {
                return Err("Policy page crossed selected Portfolio".into());
            }
        }
        self.policies = rows.clone();
        // Match the managed session: the assigned policy wins on a refresh;
        // otherwise retain a still-present selection before taking the first row.
        self.selected_policy = self
            .policies
            .iter()
            .find_map(|wire| {
                let policy = registry
                    .project(&kind("PortfolioFinancialPolicyReadModel"), wire)
                    .ok()?;
                (policy["PolicyId"] == row["ActivePolicyId"]
                    && policy["PolicyVersion"] == row["ActivePolicyVersion"])
                    .then(|| {
                        format!(
                            "{}@{}",
                            text(&policy["PolicyId"]),
                            text(&policy["PolicyVersion"])
                        )
                    })
            })
            .or_else(|| {
                self.selected_policy.clone().filter(|key| {
                    self.policies.iter().any(|wire| {
                        registry
                            .project(&kind("PortfolioFinancialPolicyReadModel"), wire)
                            .is_ok_and(|policy| {
                                *key == format!(
                                    "{}@{}",
                                    text(&policy["PolicyId"]),
                                    text(&policy["PolicyVersion"])
                                )
                            })
                    })
                })
            })
            .or_else(|| {
                self.policies
                    .first()
                    .and_then(|wire| {
                        registry
                            .project(&kind("PortfolioFinancialPolicyReadModel"), wire)
                            .ok()
                    })
                    .map(|policy| {
                        format!(
                            "{}@{}",
                            text(&policy["PolicyId"]),
                            text(&policy["PolicyVersion"])
                        )
                    })
            });
        Ok(())
    }
    async fn apply(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let action = intent["intent"]
            .as_str()
            .ok_or("Missing Portfolio action")?;
        if self.requires_reload
            && !matches!(action, "load" | "portfolio-refresh")
            && !(action == "portfolio-save" && self.pending_attachment.is_some())
            && !(action == "portfolio-policy" && field(intent, "action") == "Cancel")
        {
            return Err("A Portfolio write outcome is unknown. Reload authoritative data before another action.".into());
        }
        if !matches!(action, "load" | "portfolio-refresh")
            && intent["expectedGeneration"].as_u64() != Some(self.generation)
        {
            return Err("Portfolio selection changed; review the current view".into());
        }
        if !self.requires_reload
            && matches!(
                action,
                "load"
                    | "portfolio-refresh"
                    | "portfolio-select"
                    | "portfolio-fund"
                    | "portfolio-fund-filter"
            )
            && self
                .dialogs
                .iter()
                .any(|d| !matches!(d.key.as_str(), "financials" | "risk-history"))
        {
            return Err("Close the current editor before changing the Portfolio or Fund".into());
        }
        match action {
            "portfolio-financial" => {
                return self
                    .financial_page_action(backend, registry, intent, deadline)
                    .await;
            }
            "portfolio-choice" => {
                if self.dialogs.last().is_some_and(|d| d.key == "risk-history") {
                    return self.choose_risk_date(intent);
                }
                if self.dialogs.last().is_some_and(|d| d.key == "posting") {
                    return self
                        .choose_posting(backend, registry, intent, deadline)
                        .await;
                }
                if self.dialogs.last().is_some_and(|d| {
                    matches!(
                        d.key.as_str(),
                        "ledger" | "setup" | "authority" | "account-version" | "rule-version"
                    )
                }) {
                    return self.choose_ledger(intent).await;
                }
                return self.choose_assignment(registry, intent);
            }
            "portfolio-open" => {
                return self
                    .open_editor(backend, registry, field(intent, "view"), deadline)
                    .await;
            }
            "portfolio-fund-view" => {
                return self
                    .show_inline_fund_view(backend, registry, field(intent, "view"), deadline)
                    .await;
            }
            "portfolio-save" => {
                return self.save_editor(backend, registry, intent, deadline).await;
            }
            "portfolio-cancel" => {
                if self.dialogs.last().is_some_and(|d| d.key == "risk-policy")
                    && self.editing_policy.is_some()
                    && field(intent, "dirty").eq_ignore_ascii_case("true")
                {
                    self.policy_dirty = true;
                    self.dialogs.push(Dialog {
                        key: "discard-policy".into(),
                        title: "Unsaved Risk Policy".into(),
                        width: (440) as _,
                        height: (180) as _,
                        caption_width: (0) as _,
                        fields: vec![editors::input(
                            "warning",
                            "",
                            "Discard unsaved Risk Policy changes?",
                            "label",
                            false,
                            None,
                        )],
                        buttons: vec!["Yes".into(), "No".into()],
                        status: "".into(),
                        grids: None,
                        button_enabled: None,
                        ..Default::default()
                    });
                    return Ok("Confirm discarding the policy working copy".into());
                }
                if self.dialogs.last().is_some_and(|d| d.key == "risk-policy") {
                    self.editing_policy = None;
                    self.policy_key = None;
                    self.policy_dirty = false;
                }
                self.dialogs.pop();
                self.editing = None;
                self.edit_key = None;
                if self.dialogs.last().is_some_and(|d| d.key == "financials") {
                    self.refresh_financial_journals(registry).await?;
                }
                return Ok("Editor closed".into());
            }
            "portfolio-policy" => {
                return self
                    .policy_action(backend, registry, intent, deadline)
                    .await;
            }
            "portfolio-grid" if field(intent, "grid") == "policies" => {
                if self.editing_policy.is_some() {
                    return Err("Save or cancel the edited policy before selecting another".into());
                }
                let key = intent["originalId"].as_str().ok_or("Select a policy")?;
                if !self.policies.iter().any(|v| {
                    registry
                        .project(&kind("PortfolioFinancialPolicyReadModel"), v)
                        .is_ok_and(|row| {
                            key == format!(
                                "{}@{}",
                                text(&row["PolicyId"]),
                                text(&row["PolicyVersion"])
                            )
                        })
                }) {
                    return Err("Select a current policy row".into());
                }
                self.selected_policy = Some(key.into());
                self.message.clear();
                return Ok("Policy selected".into());
            }
            "portfolio-grid" => {
                if field(intent, "grid") == "risk-history" {
                    return self
                        .risk_grid_action(backend, registry, intent, deadline)
                        .await;
                }
                return self
                    .financial_grid_action(backend, registry, intent, deadline)
                    .await;
            }
            "load" | "portfolio-refresh" => {
                if !self.dialogs.is_empty() && !self.requires_reload {
                    return Err("Close the editor before refreshing Portfolios".into());
                }
                if self.editing_policy.is_some() && !self.requires_reload {
                    return Err(
                        "Save or cancel the edited policy before changing Portfolios".into(),
                    );
                }
                let filter = if field(intent, "filter").is_empty() {
                    if self.data.filter.is_empty() {
                        "Active"
                    } else {
                        &self.data.filter
                    }
                } else {
                    field(intent, "filter")
                }
                .to_string();
                self.data
                    .refresh(backend, registry, &filter, deadline)
                    .await?;
                if self.through.is_none() {
                    let date = self
                        .data
                        .context
                        .as_ref()
                        .ok_or("Missing API date")?
                        .value_date;
                    self.through = Some(date);
                    use chrono::Datelike;
                    self.from = chrono::NaiveDate::from_ymd_opt(date.year(), 1, 1);
                }
            }
            "portfolio-select" => {
                if self.editing_policy.is_some() {
                    return Err(
                        "Save or cancel the edited policy before changing Portfolios".into(),
                    );
                }
                let id = intent["originalId"]
                    .as_str()
                    .unwrap_or_default()
                    .parse::<i32>()
                    .map_err(|_| "Select a Portfolio")?;
                self.data
                    .select_portfolio(backend, registry, id, deadline)
                    .await?;
            }
            "portfolio-fund" => {
                self.data
                    .select_fund(
                        backend,
                        registry,
                        intent["originalId"].as_str().unwrap_or_default(),
                        deadline,
                    )
                    .await?
            }
            "portfolio-fund-filter" => {
                let filter = field(intent, "fundFilter");
                if filter != "All" {
                    let value = registry.enum_value(
                        &format!("{}.Contracts.FundOperatingState", api::ROOT),
                        filter,
                    )?;
                    if value == 0 {
                        return Err("Select a valid Fund state".into());
                    }
                }
                self.data.fund_filter = if filter == "All" {
                    None
                } else {
                    Some(filter.into())
                };
                if let Some(first) = self.data.visible_funds(registry)?.first() {
                    let key = fund_key(&registry.project(&kind("FundMandateReadModel"), first)?)?;
                    self.data
                        .select_fund(backend, registry, &key, deadline)
                        .await?;
                } else {
                    self.data.fund = None;
                    self.data.allocation = None;
                    self.data.envelope = None;
                    self.data.assignments.clear();
                    self.data.fund_revision = 0;
                    self.data.status = "No Funds match the selected state.".into();
                }
            }
            "portfolio-metrics" => {
                let from = chrono::NaiveDate::parse_from_str(field(intent, "from"), "%Y-%m-%d")
                    .map_err(|_| "Enter accounting From as yyyy-MM-dd")?;
                let through =
                    chrono::NaiveDate::parse_from_str(field(intent, "through"), "%Y-%m-%d")
                        .map_err(|_| "Enter accounting Through as yyyy-MM-dd")?;
                if from > through {
                    return Err("Metrics: choose a valid date range".into());
                }
                self.from = Some(from);
                self.through = Some(through);
            }
            _ => return Err(format!("Portfolio action {action} is not yet wired")),
        }
        let metric_error = self.load_metrics(backend, registry, deadline).await.err();
        if matches!(action, "load" | "portfolio-refresh" | "portfolio-select") {
            self.load_policies(backend, registry, deadline).await?;
            self.requires_reload = self.pending_attachment.is_some();
            self.write_in_flight = false;
            self.editing_policy = None;
            if self.pending_attachment.is_none() {
                self.dialogs.clear();
                self.editing = None;
                self.edit_key = None;
            }
            self.policy_dirty = false;
        }
        if matches!(
            action,
            "portfolio-fund"
                | "portfolio-select"
                | "portfolio-fund-filter"
                | "load"
                | "portfolio-refresh"
        ) {
            if let Some(view) = self.inline_fund_view.clone() {
                self.dialogs
                    .retain(|d| !matches!(d.key.as_str(), "financials" | "risk-history"));
                if self.data.fund.is_some() {
                    self.show_inline_fund_view(backend, registry, &view, deadline)
                        .await?;
                }
            }
        }
        self.message = self.data.status.clone();
        if let Some(error) = metric_error {
            return Err(format!("Fund metrics unavailable: {error}"));
        }
        Ok("Portfolio action completed".into())
    }
    fn row_grid(
        &self,
        registry: &Registry,
        key: &str,
        name: &str,
        values: &[impl std::borrow::Borrow<Value>],
        columns: Option<Vec<String>>,
        selected: Option<String>,
        ids: impl Fn(&Value, usize) -> Result<String, String>,
    ) -> Result<Grid, String> {
        let kind = kind(name);
        use crate::{backend::contract_view::Field, models::wire_types::FromBackend};
        let parent_name = if key == "funds" {
            self.data
                .portfolio
                .as_ref()
                .map(|wire| {
                    let kind = crate::state::portfolio_data::kind("PortfolioReadModel");
                    let name = Field::new(registry, &kind, wire).field("Name")?;
                    Option::<String>::from_backend(name.value).map(Option::unwrap_or_default)
                })
                .transpose()?
                .unwrap_or_default()
        } else {
            String::new()
        };
        let mut rows = vec![];
        for (index, value) in values.iter().enumerate() {
            let value = value.borrow();
            let mut fields: BTreeMap<_, _> = display::fields(registry, &kind, value)?
                .into_iter()
                .collect();
            if key == "funds" {
                fields.insert("Portfolio".into(), parent_name.clone());
            }
            rows.push(cell_row(ids(value, index)?, fields));
        }
        Ok(grid(
            key,
            columns.unwrap_or(display::field_names(&kind)?),
            rows,
            selected,
        ))
    }
    fn details_grid(
        &self,
        registry: &Registry,
        key: &str,
        name: &str,
        value: Option<&Value>,
    ) -> Result<Grid, String> {
        let rows = if let Some(value) = value {
            display::fields(registry, &kind(name), value)?
                .into_iter()
                .enumerate()
                .map(|(i, (name, value))| {
                    cell_row(
                        i.to_string(),
                        BTreeMap::from([("Property".into(), name), ("Value".into(), value)]),
                    )
                })
                .collect()
        } else {
            vec![]
        };
        Ok(grid(
            key,
            vec!["Property".into(), "Value".into()],
            rows,
            None,
        ))
    }
    fn fund_summary(&self, registry: &Registry, parent: &Json) -> Result<Grid, String> {
        let Some(fund) = &self.data.fund else {
            return Ok(grid(
                "fund",
                vec!["Property".into(), "Value".into()],
                vec![],
                None,
            ));
        };
        let fund = registry.project(&kind("FundMandateReadModel"), fund)?;
        let csv = |key: &str| -> Result<String, String> {
            Ok(fund[key]
                .as_array()
                .ok_or(format!("Invalid Fund {key}"))?
                .iter()
                .map(text)
                .collect::<Vec<_>>()
                .join(", "))
        };
        let rows = vec![
            ("Fund ID", text(&fund["FundId"])),
            ("Portfolio", text(&parent["Name"])),
            ("Name", text(&fund["Name"])),
            ("Version", text(&fund["FundMandateVersion"])),
            ("Trading year", text(&fund["TradingYear"])),
            ("State", text(&fund["OperatingState"])),
            (
                "Effective from",
                text(&fund["EffectiveFromUtc"]).chars().take(10).collect(),
            ),
            (
                "Effective until",
                if fund["EffectiveUntilUtc"].is_null() {
                    "Open ended".into()
                } else {
                    text(&fund["EffectiveUntilUtc"]).chars().take(10).collect()
                },
            ),
            ("Decision horizon", text(&fund["DecisionHorizon"])),
            ("Objective", text(&fund["Objective"])),
            ("Underlyings", csv("UnderlyingUniverse")?),
            ("Asset types", csv("EligibleAssetTypes")?),
            ("Directions", csv("PermittedDirections")?),
            ("Conditions", csv("PermittedConditions")?),
            (
                "Strategy permissions",
                format!(
                    "{} configured; see Assignments",
                    fund["PermittedTradeStrategyFamilies"]
                        .as_array()
                        .ok_or("Invalid Fund permissions")?
                        .len()
                ),
            ),
        ];
        Ok(grid(
            "fund",
            vec!["Property".into(), "Value".into()],
            rows.into_iter()
                .enumerate()
                .map(|(i, (name, value))| {
                    cell_row(
                        i.to_string(),
                        BTreeMap::from([("Property".into(), name.into()), ("Value".into(), value)]),
                    )
                })
                .collect(),
            None,
        ))
    }
    pub fn result(&self, registry: &Registry) -> Result<ContractResult, String> {
        let parent = self
            .data
            .portfolio
            .as_ref()
            .map(|v| registry.project(&kind("PortfolioReadModel"), v))
            .transpose()?
            .unwrap_or(Json::Null);
        let fund = self
            .data
            .fund
            .as_ref()
            .map(|v| registry.project(&kind("FundMandateReadModel"), v))
            .transpose()?
            .unwrap_or(Json::Null);
        let mut context = BTreeMap::from([
            ("status".into(), self.message.clone()),
            ("portfolioName".into(), text(&parent["Name"])),
            ("fundName".into(), text(&fund["Name"])),
            ("filter".into(), self.data.filter.clone()),
            (
                "fundFilter".into(),
                self.data.fund_filter.clone().unwrap_or("All".into()),
            ),
            (
                "from".into(),
                self.from.map(|v| v.to_string()).unwrap_or_default(),
            ),
            (
                "through".into(),
                self.through.map(|v| v.to_string()).unwrap_or_default(),
            ),
            ("refreshParent".into(), String::new()),
            (
                "policyDirty".into(),
                if self.policy_dirty { "True" } else { "False" }.into(),
            ),
        ]);
        if let Some(report) = &self.metrics {
            context.extend(financial::metrics_context(registry, report)?);
        }
        let current = self.data.is_current_fund(registry)?;
        let p = !parent.is_null();
        let f = !fund.is_null();
        let enabled = BTreeMap::from([
            ("New Portfolio...", true),
            ("Risk Policy...", p),
            ("Portfolio Actions", p),
            ("New Version...", p),
            ("Change State...", p),
            ("Delete Draft...", parent["OperatingState"] == "Draft"),
            ("Create Fund...", p),
            ("Change Fund...", f && current),
            ("Change Fund State...", f && current),
            ("Financials...", f),
            ("Risk History...", f),
            ("Allocation...", f && current),
            ("Risk Envelope...", f && current),
            ("Trade Assignment...", f && current),
        ]);
        let visible = self.data.visible_fund_refs(registry)?;
        let grids = vec![
            self.row_grid(
                registry,
                "portfolios",
                "PortfolioReadModel",
                &self.data.portfolios,
                None,
                if p {
                    Some(text(&parent["PortfolioId"]))
                } else {
                    None
                },
                |row, _| {
                    let kind = kind("PortfolioReadModel");
                    crate::backend::contract_view::Field::new(registry, &kind, row)
                        .field("PortfolioId")?
                        .text()
                },
            )?,
            self.row_grid(
                registry,
                "funds",
                "FundMandateReadModel",
                &visible,
                Some(
                    [
                        "FundId",
                        "Name",
                        "Portfolio",
                        "OperatingState",
                        "FundMandateVersion",
                    ]
                    .into_iter()
                    .map(str::to_string)
                    .collect(),
                ),
                if f { Some(fund_key(&fund)?) } else { None },
                |row, _| crate::state::portfolio_data::fund_key_wire(registry, row),
            )?,
            self.fund_summary(registry, &parent)?,
            self.details_grid(
                registry,
                "allocation",
                "FundAllocationReadModel",
                self.data.allocation.as_ref(),
            )?,
            self.details_grid(
                registry,
                "envelope",
                "FundRiskEnvelopeReadModel",
                self.data.envelope.as_ref(),
            )?,
            self.row_grid(
                registry,
                "assignments",
                "FundTradeTemplateAssignmentReadModel",
                &self.data.assignments,
                None,
                None,
                |_, index| Ok(index.to_string()),
            )?,
        ];
        let policy = if p {
            Some(self.policy_document(registry, &parent)?)
        } else {
            None
        };
        let mut dialogs = self.dialogs.clone();
        if let Some(index) = dialogs.iter().position(|d| d.key == "risk-policy") {
            let mut modal = self.policy_document(registry, &parent)?;
            modal.buttons.retain(|v| v != "Change Policy...");
            modal.status = format!(
                "Portfolio {} — {}; Active policy: {} v{}",
                text(&parent["PortfolioId"]),
                text(&parent["Name"]),
                text(&parent["ActivePolicyId"]),
                text(&parent["ActivePolicyVersion"])
            )
            .into();
            if !self.dialogs[index].status.is_empty() {
                modal.status = self.dialogs[index].status.clone();
            }
            dialogs[index] = modal;
        }
        let fund_view = self
            .inline_fund_view
            .as_ref()
            .and_then(|key| dialogs.iter().find(|d| d.key == *key))
            .cloned();
        if let Some(key) = &self.inline_fund_view {
            dialogs.retain(|d| d.key != *key);
        }
        let document = Document {
            grids: grids,
            enabled: enabled
                .into_iter()
                .map(|(k, v)| (k.to_string(), v))
                .collect(),
            context: context.clone(),
            dialogs: dialogs,
            policy: policy,
            fund_view: fund_view,
            ..Default::default()
        };
        let bytes = crate::wire::encode(&document).map_err(|e| e.to_string())?;
        Ok(ContractResult {
            view: "portfolio".into(),
            context,
            documents: BTreeMap::from([("portfolio".into(), serde_bytes::ByteBuf::from(bytes))]),
            ..Default::default()
        })
    }
    fn policy_document(&self, registry: &Registry, parent: &Json) -> Result<Dialog, String> {
        let selected = self
            .policies
            .iter()
            .find(|value| {
                registry
                    .project(&kind("PortfolioFinancialPolicyReadModel"), value)
                    .is_ok_and(|row| {
                        self.selected_policy.as_ref().is_some_and(|key| {
                            *key == format!(
                                "{}@{}",
                                text(&row["PolicyId"]),
                                text(&row["PolicyVersion"])
                            )
                        })
                    })
            })
            .or_else(|| {
                self.policies.iter().find(|value| {
                    registry
                        .project(&kind("PortfolioFinancialPolicyReadModel"), value)
                        .is_ok_and(|row| {
                            row["PolicyId"] == parent["ActivePolicyId"]
                                && row["PolicyVersion"] == parent["ActivePolicyVersion"]
                        })
                })
            })
            .or(self.policies.first());
        let source = self.editing_policy.as_ref().or(selected);
        let editing = self.editing_policy.is_some();
        let values = source
            .map(|v| display::fields(registry, &kind("PortfolioFinancialPolicyReadModel"), v))
            .transpose()?
            .unwrap_or_default()
            .into_iter()
            .collect::<BTreeMap<_, _>>();
        let fields = [
            ("Name", "Name"),
            ("BaseCurrency", "Base Currency"),
            ("CapitalBase", "Capital Base"),
            ("ProtectedReserve", "Protected Reserve"),
            ("MaximumDeployableCapital", "Deployable Capital"),
            ("MaximumRiskPerTrade", "Risk Per Trade"),
            ("MaximumAggregateRisk", "Aggregate Risk"),
            ("MaximumMargin", "Maximum Margin"),
            ("MaximumGrossNotional", "Gross Notional"),
            ("MaximumOpenPositions", "Open Positions"),
            ("MaximumDrawdownAmount", "Drawdown"),
        ]
        .into_iter()
        .map(|(key, caption)| Field {
            key: (key).to_string(),
            caption: (caption).to_string(),
            value: (values.get(key).cloned().unwrap_or_default()).to_string(),
            kind: (if matches!(key, "Name" | "BaseCurrency") {
                "text"
            } else {
                "number"
            })
            .to_string(),
            enabled: editing && key != "BaseCurrency",
            choices: None,
            minimum: (0) as _,
            maximum: (1000000000000f64) as _,
            decimals: (2) as usize,
            ..Default::default()
        })
        .collect::<Vec<_>>();
        let selected_json = selected
            .map(|v| registry.project(&kind("PortfolioFinancialPolicyReadModel"), v))
            .transpose()?
            .unwrap_or(Json::Null);
        let policy_grid = self.row_grid(
            registry,
            "policies",
            "PortfolioFinancialPolicyReadModel",
            &self.policies,
            None,
            if selected.is_some() {
                Some(format!(
                    "{}@{}",
                    text(&selected_json["PolicyId"]),
                    text(&selected_json["PolicyVersion"])
                ))
            } else {
                None
            },
            |row, _| {
                Ok(format!(
                    "{}@{}",
                    crate::backend::contract_view::Field::new(
                        registry,
                        &kind("PortfolioFinancialPolicyReadModel"),
                        row
                    )
                    .field("PolicyId")?
                    .text()?,
                    crate::backend::contract_view::Field::new(
                        registry,
                        &kind("PortfolioFinancialPolicyReadModel"),
                        row
                    )
                    .field("PolicyVersion")?
                    .text()?
                ))
            },
        )?;
        let limit_columns = display::field_names(&kind("TradeFamilyRiskLimitReadModel"))?
            .into_iter()
            .filter(|v| !matches!(v.as_str(), "DefinitionVersion" | "CatalogDeployment"))
            .collect();
        let mut limit_rows = vec![];
        if let Some(source) = source {
            let values = crate::services::strategy::member(
                registry,
                &kind("PortfolioFinancialPolicyReadModel"),
                source,
                "TradeFamilyLimits",
            )?;
            for (index, wire) in values
                .as_array()
                .filter(|v| v.len() <= 4096)
                .ok_or("Invalid policy family limits")?
                .iter()
                .enumerate()
            {
                let row = registry.project(&kind("TradeFamilyRiskLimitReadModel"), wire)?;
                let mut fields: BTreeMap<_, _> =
                    display::fields(registry, &kind("TradeFamilyRiskLimitReadModel"), wire)?
                        .into_iter()
                        .collect();
                let label = if row["CatalogDeployment"].is_null() {
                    format!(
                        "Legacy family {} v{}",
                        text(&row["TradeStrategyFamilyId"]),
                        text(&row["DefinitionVersion"])
                    )
                } else {
                    self.deployments
                        .iter()
                        .find(|d| policy::same_deployment(&d["Key"], &row["CatalogDeployment"]))
                        .map(|d| text(&d["Description"]))
                        .unwrap_or_else(|| {
                            format!(
                                "Unavailable deployment {} v{}",
                                text(&row["CatalogDeployment"]["Id"]),
                                text(&row["CatalogDeployment"]["Version"])
                            )
                        })
                };
                fields.insert("TradeStrategyFamilyId".into(), label);
                limit_rows.push(cell_row(index.to_string(), fields));
            }
        }
        let mut limits = grid("family-limits", limit_columns, limit_rows, None);
        limits.editable = editing;
        let draft = selected_json["OperatingState"] == "Draft";
        Ok(Dialog {
            key: "risk-policy".into(),
            title: "Portfolio Risk Policy".into(),
            width: (1260) as _,
            height: (820) as _,
            caption_width: (170) as _,
            fields: fields,
            buttons: vec![
                "New Policy".into(),
                "Change Policy...".into(),
                "New Version".into(),
                "Save".into(),
                "Cancel".into(),
                "Activate && Assign".into(),
                "Retire".into(),
                "Delete Draft".into(),
            ],
            status: "".into(),
            grids: Some(vec![policy_grid, limits]),
            button_enabled: Some(BTreeMap::from([
                ("New Policy".into(), !editing),
                ("Change Policy...".into(), !editing && selected.is_some()),
                ("New Version".into(), !editing && selected.is_some()),
                ("Save".into(), editing),
                ("Cancel".into(), editing),
                ("Activate && Assign".into(), !editing && draft),
                ("Retire".into(), !editing && selected.is_some()),
                ("Delete Draft".into(), !editing && draft),
            ])),
            ..Default::default()
        })
    }
    pub async fn execute(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
    ) -> Result<ContractPublication, String> {
        if intent["schema"] != 1 || intent["view"] != "portfolio" {
            return Err("Invalid Portfolio intent".into());
        }
        let request = uuid::Uuid::parse_str(intent["requestId"].as_str().unwrap_or_default())
            .ok()
            .filter(|v| !v.is_nil())
            .ok_or("Invalid Portfolio request identity")?;
        let key = request.to_string();
        let fields = intent["fields"]
            .as_object()
            .filter(|v| v.len() <= 4096)
            .ok_or("Invalid Portfolio fields")?;
        if fields.iter().any(|(name, value)| {
            name.len() > 128 || value.as_str().is_none_or(|v| v.len() > 131072)
        }) || fields
            .iter()
            .map(|(k, v)| k.len() + v.as_str().unwrap_or_default().len())
            .sum::<usize>()
            > 4 * 1024 * 1024
        {
            return Err("Portfolio input exceeds its bound".into());
        }
        if intent["originalId"]
            .as_str()
            .is_some_and(|id| id.len() > 512)
        {
            return Err("Portfolio selection identity exceeds its bound".into());
        }
        let outcome = if let Some((original, outcome)) = self.receipts.get(&key) {
            if original != intent {
                return Err("RequestId belongs to another Portfolio action".into());
            }
            outcome.clone()
        } else {
            // Reserve receipt storage before a possible write. Never evict an
            // uncertain write merely to make room for an unrelated user action.
            if self.receipts.len() >= 64 {
                let old = self
                    .receipts
                    .iter()
                    .find(|(_, (_, outcome))| outcome.state != "Unknown")
                    .map(|(id, _)| id.clone());
                if let Some(old) = old {
                    self.receipts.remove(&old);
                } else {
                    return Err(
                        "Reconcile uncertain Portfolio outcomes before submitting more actions"
                            .into(),
                    );
                }
            }
            self.accepted_write = false;
            let deadline = tokio::time::Instant::now() + backend.default_timeout();
            let result = tokio::time::timeout_at(
                deadline,
                self.apply(backend, registry, intent, deadline),
            )
            .await
            .map_err(|_| {
                if self.write_in_flight {
                    self.requires_reload = true;
                    "OutcomeUnknown: Portfolio write deadline expired; reload before another write"
                        .to_string()
                } else {
                    "Portfolio action deadline expired".to_string()
                }
            })
            .and_then(|v| v);
            let result = if result.is_err() && self.accepted_write {
                self.message = "Saved; reload authoritative data to refresh the view".into();
                Ok("Saved; reload authoritative data to refresh the view".to_string())
            } else {
                result
            };
            let outcome = match result {
                Ok(message) => EditorOutcome {
                    request_id: key.clone(),
                    state: "Completed".into(),
                    message,
                },
                Err(message) => {
                    self.message = message.clone();
                    if let Some(dialog) = self.dialogs.last_mut() {
                        dialog.status = message.clone().into();
                    }
                    EditorOutcome {
                        request_id: key.clone(),
                        state: if message.starts_with("OutcomeUnknown:") {
                            self.requires_reload = true;
                            "Unknown"
                        } else {
                            "Failed"
                        }
                        .into(),
                        message,
                    }
                }
            };
            self.receipts.insert(key, (intent.clone(), outcome.clone()));
            outcome
        };
        self.generation += 1;
        Ok(ContractPublication {
            schema: 1,
            generation: self.generation,
            status: outcome.message.clone(),
            result: self.result(registry)?,
            outcomes: vec![outcome],
            ..Default::default()
        })
    }
}
