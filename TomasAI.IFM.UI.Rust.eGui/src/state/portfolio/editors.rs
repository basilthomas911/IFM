//! Detached Portfolio administration drafts. Editing never mutates the selected
//! projection; only an acknowledged backend command can replace authority.
use super::*;
use crate::services::{
    business_context::BusinessContext, parameters::overlay_type, portfolio_commands as commands,
};

pub(super) fn input(
    key: &str,
    caption: &str,
    value: impl Into<Json>,
    kind: &str,
    enabled: bool,
    choices: Option<Vec<Json>>,
) -> Json {
    let value = value.into();
    json!({"key":key,"caption":caption,"value":text(&value),"kind":kind,"enabled":enabled,"choices":choices,"minimum":0,"maximum":1000000000000f64,"decimals":2})
}
pub(super) fn editor(
    key: &str,
    title: &str,
    width: i32,
    height: i32,
    caption: i32,
    fields: Vec<Json>,
    save: &str,
) -> Json {
    json!({"key":key,"title":title,"width":width,"height":height,"captionWidth":caption,"fields":fields,"buttons":[save,"Cancel"],"status":"","grids":null,"buttonEnabled":null})
}
pub(super) fn enum_choices(registry: &Registry, kind: &str) -> Result<Vec<Json>, String> {
    Ok(registry
        .enum_names(kind)?
        .into_iter()
        .filter(|name| name != "Unknown")
        .map(|name| json!({"value":name,"label":name,"enabled":true}))
        .collect())
}
pub(super) fn defaults(name: &str) -> Result<Value, String> {
    let models: BTreeMap<String, Vec<u8>> = serde_json::from_str(include_str!(
        "../../../tests/fixtures/backend/portfolio-model-defaults.json"
    ))
    .map_err(|e| e.to_string())?;
    crate::backend::codec::decode(
        models
            .get(&kind(name))
            .ok_or("Missing compiled editor defaults")?,
    )
}
impl PortfolioController {
    pub(super) async fn open_editor(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        view: &str,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        if self.editing_policy.is_some() {
            return Err("Save or cancel the policy working copy first".into());
        }
        if !self.dialogs.is_empty() {
            return Err("Close the current editor before opening another".into());
        }
        if view == "risk-policy" {
            self.load_policies(backend, registry, deadline).await?;
            self.dialogs.push(json!({"key":"risk-policy","status":""}));
            return Ok("Risk policies loaded".into());
        }
        if view == "risk-history" {
            return self
                .open_risk_history(backend, registry, false, deadline)
                .await;
        }
        let context = BusinessContext::load(backend).await?;
        self.data.context = Some(context.clone());
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
        let portfolio_state = format!("{}.Contracts.PortfolioOperatingState", api::ROOT);
        let fund_state = format!("{}.Contracts.FundOperatingState", api::ROOT);
        let dialog = match view {
            "ledger" | "setup" | "authority" => {
                return self
                    .open_ledger_editor(backend, registry, view, deadline)
                    .await;
            }
            "financials" => {
                return self.open_financials(backend, registry, deadline).await;
            }
            "new-fund" | "version-fund" | "allocation" | "envelope" | "assignment" => {
                self.open_fund_editor(backend, registry, view, &context, deadline)
                    .await?
            }
            "new-portfolio" | "version-portfolio" => {
                let original = if view == "new-portfolio" {
                    let allocated = api::query(
                        backend,
                        registry,
                        &context,
                        "AllocatePortfolioBusinessIdQuery",
                        "Portfolio",
                        BTreeMap::from([(
                            "Kind".into(),
                            registry
                                .enum_value(
                                    &format!(
                                        "{}.ServiceApi.PortfolioBusinessIdentityKind",
                                        api::ROOT
                                    ),
                                    "Portfolio",
                                )?
                                .into(),
                        )]),
                        deadline,
                    )
                    .await?;
                    let row = registry.project(
                        &format!("{}.ServiceApi.PortfolioBusinessIdAllocation", api::ROOT),
                        &allocated,
                    )?;
                    if row["Kind"] != "Portfolio" {
                        return Err("Wrong allocated identity kind".into());
                    }
                    let id = row["Value"]
                        .as_i64()
                        .filter(|id| *id > 0 && *id <= i32::MAX as i64)
                        .ok_or("Invalid allocated Portfolio identity")?;
                    overlay_type(
                        registry,
                        &kind("PortfolioReadModel"),
                        &defaults("PortfolioReadModel")?,
                        &BTreeMap::from([
                            ("PortfolioId".into(), id.into()),
                            ("PortfolioVersion".into(), 1.into()),
                            (
                                "OperatingState".into(),
                                registry.enum_value(&portfolio_state, "Draft")?.into(),
                            ),
                            ("EffectiveFromUtc".into(), timestamp_wire(context.as_of_utc)),
                        ]),
                    )?
                } else {
                    let version = parent["PortfolioVersion"]
                        .as_i64()
                        .filter(|v| *v > 0)
                        .and_then(|v| v.checked_add(1))
                        .ok_or("Select a current Portfolio version")?;
                    overlay_type(
                        registry,
                        &kind("PortfolioReadModel"),
                        self.data.portfolio.as_ref().ok_or("Select a Portfolio")?,
                        &BTreeMap::from([
                            ("PortfolioVersion".into(), version.into()),
                            ("EffectiveUntilUtc".into(), Value::Nil),
                        ]),
                    )?
                };
                let value = overlay_type(
                    registry,
                    &kind("PortfolioReadModel"),
                    &original,
                    &BTreeMap::from([
                        ("CreatedOnUtc".into(), timestamp_wire(context.as_of_utc)),
                        (
                            "CreatedBy".into(),
                            std::env::var("USERNAME").unwrap_or_default().into(),
                        ),
                    ]),
                )?;
                let row = registry.project(&kind("PortfolioReadModel"), &value)?;
                let currency = enum_choices(
                    registry,
                    "TomasAI.IFM.Domain.MarketData.Shared.CurrencyType",
                )?;
                self.editing = Some(("PortfolioReadModel".into(), value));
                self.edit_key = Some(uuid::Uuid::new_v4());
                let effective = chrono::DateTime::parse_from_rfc3339(
                    row["EffectiveFromUtc"]
                        .as_str()
                        .ok_or("Missing effective date")?,
                )
                .map_err(|e| e.to_string())?
                .with_timezone(&chrono_tz::America::New_York)
                .format("%Y-%m-%d %H:%M")
                .to_string();
                editor(
                    view,
                    if view == "new-portfolio" {
                        "Create Portfolio"
                    } else {
                        "Create Portfolio Version"
                    },
                    640,
                    260,
                    180,
                    vec![
                        input(
                            "PortfolioId",
                            "Portfolio ID",
                            row["PortfolioId"].clone(),
                            "text",
                            false,
                            None,
                        ),
                        input("Name", "Name", row["Name"].clone(), "text", true, None),
                        input(
                            "BaseCurrency",
                            "Base Currency",
                            row["BaseCurrency"].clone(),
                            "choice",
                            true,
                            Some(currency),
                        ),
                        input(
                            "OperatingState",
                            "Operating State",
                            row["OperatingState"].clone(),
                            "choice",
                            true,
                            Some(enum_choices(registry, &portfolio_state)?),
                        ),
                        input(
                            "BrokerAccountRefs",
                            "Broker Accounts (CSV)",
                            row["BrokerAccountRefs"]
                                .as_array()
                                .ok_or("Invalid broker accounts")?
                                .iter()
                                .map(text)
                                .collect::<Vec<_>>()
                                .join(", "),
                            "text",
                            true,
                            None,
                        ),
                        input(
                            "EffectiveFromUtc",
                            "Effective From",
                            effective,
                            "datetime",
                            true,
                            None,
                        ),
                    ],
                    "Save",
                )
            }
            "portfolio-state" | "fund-state" => {
                let is_fund = view == "fund-state";
                if is_fund && !self.data.is_current_fund(registry)? {
                    return Err("Select the latest Fund version before changing its state".into());
                }
                let row = if is_fund { &fund } else { &parent };
                if row.is_null() {
                    return Err("Select a current row".into());
                }
                editor(
                    view,
                    if is_fund {
                        "Change Fund State"
                    } else {
                        "Change Portfolio State"
                    },
                    520,
                    150,
                    140,
                    vec![
                        input(
                            "state",
                            "Target State",
                            row["OperatingState"].clone(),
                            "choice",
                            true,
                            Some(enum_choices(
                                registry,
                                if is_fund {
                                    &fund_state
                                } else {
                                    &portfolio_state
                                },
                            )?),
                        ),
                        input("reason", "Reason", "", "text", true, None),
                    ],
                    "Apply",
                )
            }
            "delete-portfolio" => {
                if parent["OperatingState"] != "Draft" {
                    return Err("Only a Draft Portfolio can be deleted".into());
                }
                editor(
                    view,
                    "Delete Draft Portfolio",
                    660,
                    320,
                    190,
                    vec![
                        input(
                            "warning",
                            "",
                            format!(
                                "Delete Draft Portfolio {}: {}. The sequence-generated ID will never be reused. Type '{}' to confirm.",
                                text(&parent["PortfolioId"]),
                                text(&parent["Name"]),
                                text(&parent["PortfolioId"])
                            ),
                            "label",
                            false,
                            None,
                        ),
                        input("confirmation", "Portfolio ID", "", "text", true, None),
                        input("reason", "Reason", "", "text", true, None),
                    ],
                    "Delete Draft",
                )
            }
            _ => return Err(format!("Portfolio editor {view} is not yet wired")),
        };
        self.dialogs.push(dialog);
        self.message.clear();
        Ok("Editor opened".into())
    }
    pub(super) async fn save_editor(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        if self.pending_attachment.is_some() {
            return self
                .retry_fund_attachment(backend, registry, intent, deadline)
                .await;
        }
        if self.dialogs.last().is_some_and(|d| d["key"] == "posting") {
            return self
                .post_or_reconcile(backend, registry, intent, deadline)
                .await;
        }
        if self.dialogs.last().is_some_and(|d| {
            matches!(
                d["key"].as_str(),
                Some("setup" | "authority" | "account-version" | "rule-version")
            )
        }) {
            let mut input = intent.clone();
            input["fields"]["action"] = "Save".into();
            return self
                .ledger_action(backend, registry, &input, deadline)
                .await;
        }
        let view = self
            .dialogs
            .last()
            .and_then(|d| d["key"].as_str())
            .ok_or("No editor is open")?
            .to_string();
        let context = BusinessContext::load(backend).await?;
        self.data.context = Some(context.clone());
        if matches!(
            view.as_str(),
            "new-fund" | "version-fund" | "allocation" | "envelope" | "assignment"
        ) {
            return self
                .save_fund_editor(backend, registry, &view, intent, &context, deadline)
                .await;
        }
        let parent = self
            .data
            .portfolio
            .as_ref()
            .map(|v| registry.project(&kind("PortfolioReadModel"), v))
            .transpose()?
            .unwrap_or(Json::Null);
        let fields = intent["fields"]
            .as_object()
            .ok_or("Missing editor fields")?;
        let request = uuid::Uuid::parse_str(intent["requestId"].as_str().unwrap_or_default())
            .map_err(|_| "Invalid editor request identity")?;
        let reason = field(intent, "reason").trim();
        let mut target = parent["PortfolioId"]
            .as_i64()
            .and_then(|id| i32::try_from(id).ok())
            .unwrap_or_default();
        let mut child = None;
        let mut command_id = request;
        let mut payload = BTreeMap::new();
        let name = match view.as_str() {
            "new-portfolio" | "version-portfolio" => {
                let (model, wire) = self.editing.as_ref().ok_or("Missing editor working copy")?;
                if model != "PortfolioReadModel" {
                    return Err("Editor model changed".into());
                }
                let mut changes = BTreeMap::new();
                for key in [
                    "Name",
                    "BaseCurrency",
                    "OperatingState",
                    "BrokerAccountRefs",
                    "EffectiveFromUtc",
                ] {
                    let Some(value) = fields.get(key).and_then(Json::as_str) else {
                        continue;
                    };
                    let value = value.trim();
                    let next = match key {
                        "BaseCurrency" => Json::String(value.to_uppercase()),
                        "OperatingState" => {
                            registry.enum_value(
                                &format!("{}.Contracts.PortfolioOperatingState", api::ROOT),
                                value,
                            )?;
                            value.into()
                        }
                        "BrokerAccountRefs" => json!(
                            value
                                .split(',')
                                .map(str::trim)
                                .filter(|v| !v.is_empty())
                                .collect::<Vec<_>>()
                        ),
                        "EffectiveFromUtc" => {
                            use chrono::TimeZone;
                            let local =
                                chrono::NaiveDateTime::parse_from_str(value, "%Y-%m-%d %H:%M")
                                    .map_err(
                                        |_| "Enter Effective From as yyyy-MM-dd HH:mm Eastern",
                                    )?;
                            let instant=chrono_tz::America::New_York.from_local_datetime(&local).single().ok_or("Effective From is ambiguous or invalid at the daylight-saving transition")?;
                            crate::backend::contracts::utc_roundtrip(
                                instant.with_timezone(&chrono::Utc),
                            )
                            .into()
                        }
                        _ => value.into(),
                    };
                    changes.insert(key.to_string(), next);
                }
                let value = registry.patch(&kind(model), wire, &changes)?;
                let row = registry.project(&kind(model), &value)?;
                target = row["PortfolioId"]
                    .as_i64()
                    .and_then(|id| i32::try_from(id).ok())
                    .filter(|id| *id > 0)
                    .ok_or("Invalid draft identity")?;
                self.editing = Some((model.clone(), value.clone()));
                payload.insert("Portfolio".into(), value.clone());
                if view == "new-portfolio" {
                    let key = self.edit_key.ok_or("Missing prepared create identity")?;
                    command_id = api::create_command_id(key, &value)?;
                    payload.insert("IdempotencyKey".into(), key.to_string().into());
                    "CreatePortfolioCommand"
                } else {
                    payload.insert(
                        "ExpectedVersion".into(),
                        self.data.portfolio_revision.into(),
                    );
                    "AddPortfolioVersionCommand"
                }
            }
            "portfolio-state" | "fund-state" => {
                if reason.is_empty() {
                    return Err("Enter a reason".into());
                }
                let is_fund = view == "fund-state";
                if is_fund {
                    if !self.data.is_current_fund(registry)? {
                        return Err("Select the latest Fund version".into());
                    }
                    let row = registry.project(
                        &kind("FundMandateReadModel"),
                        self.data.fund.as_ref().ok_or("Select a Fund")?,
                    )?;
                    child = Some(
                        row["FundId"]
                            .as_i64()
                            .and_then(|id| i32::try_from(id).ok())
                            .filter(|id| *id > 0)
                            .ok_or("Invalid Fund identity")?,
                    );
                }
                let state = registry.enum_value(
                    &format!(
                        "{}.Contracts.{}OperatingState",
                        api::ROOT,
                        if is_fund { "Fund" } else { "Portfolio" }
                    ),
                    field(intent, "state"),
                )?;
                if state == 0 {
                    return Err("Select a valid operating state".into());
                }
                payload.insert("State".into(), state.into());
                payload.insert("Reason".into(), reason.into());
                payload.insert(
                    "ExpectedVersion".into(),
                    if is_fund {
                        self.data.fund_revision
                    } else {
                        self.data.portfolio_revision
                    }
                    .into(),
                );
                if is_fund {
                    "ChangeFundOperatingStateCommand"
                } else {
                    "ChangePortfolioOperatingStateCommand"
                }
            }
            "delete-portfolio" => {
                if parent["OperatingState"] != "Draft"
                    || field(intent, "confirmation") != target.to_string()
                    || reason.is_empty()
                {
                    return Err("Type the Draft Portfolio ID and a deletion reason".into());
                }
                payload.insert(
                    "ExpectedVersion".into(),
                    self.data.portfolio_revision.into(),
                );
                payload.insert("Reason".into(), reason.into());
                "DeleteDraftPortfolioCommand"
            }
            _ => return Err(format!("Portfolio save {view} is not yet wired")),
        };
        let prepared = commands::prepare(
            registry, &context, name, target, child, command_id, request, payload,
        )?;
        self.write_in_flight = true;
        let result = commands::submit(backend, &prepared, deadline).await;
        self.write_in_flight = false;
        match result {
            Err(error) => {
                if error.starts_with("OutcomeUnknown:") {
                    self.requires_reload = true;
                }
                Err(error)
            }
            Ok(_) => {
                self.accepted_write = true;
                self.dialogs.pop();
                self.editing = None;
                self.edit_key = None;
                let filter = if matches!(view.as_str(), "new-portfolio" | "version-portfolio") {
                    let row = registry.project(
                        &kind("PortfolioReadModel"),
                        &prepared_dto(registry, &prepared, "Portfolio")?,
                    )?;
                    text(&row["OperatingState"])
                } else if view == "portfolio-state" {
                    field(intent, "state").into()
                } else {
                    self.data.filter.clone()
                };
                self.data
                    .refresh(backend, registry, &filter, deadline)
                    .await?;
                self.load_policies(backend, registry, deadline).await?;
                self.load_metrics(backend, registry, deadline).await?;
                self.message = self.data.status.clone();
                Ok("Portfolio command committed".into())
            }
        }
    }
}
fn prepared_dto(
    registry: &Registry,
    prepared: &commands::Prepared,
    name: &str,
) -> Result<Value, String> {
    let kind = if prepared.subject.verb == "CreatePortfolio" {
        "CreatePortfolioCommand"
    } else {
        "AddPortfolioVersionCommand"
    };
    crate::services::strategy::member(
        registry,
        &format!("{}.Commands.{kind}", api::ROOT),
        &prepared.body,
        name,
    )
}
