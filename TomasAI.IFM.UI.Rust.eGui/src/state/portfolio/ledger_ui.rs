//! Ledger administration dialogs. Configured identities and versions remain in
//! backend DTOs; editable selectors only choose among those exact definitions.
use super::editors::{editor, input};
use super::financial_ui::{amount, typed_grid};
use super::*;
use crate::services::{business_context::BusinessContext, financial, financial_controls};
fn fk(name: &str) -> String {
    format!("{}.{name}", financial::ROOT)
}
fn choice(value: impl crate::models::portfolio::FieldText, label: impl Into<String>) -> Choice {
    Choice {
        value: value.field_text(),
        label: label.into(),
        enabled: true,
        ..Default::default()
    }
}
fn member(registry: &Registry, kind: &str, value: &Value, name: &str) -> Result<Value, String> {
    crate::services::strategy::member(registry, &fk(kind), value, name)
}
fn rows(
    registry: &Registry,
    name: &str,
    value: Option<&Value>,
    field: &str,
) -> Result<Vec<Value>, String> {
    let Some(value) = value else {
        return Ok(vec![]);
    };
    member(registry, name, value, field)?
        .as_array()
        .filter(|v| v.len() <= 100_000)
        .cloned()
        .ok_or("Invalid ledger configuration rows".into())
}
fn day(text: &Json) -> Result<chrono::NaiveDate, String> {
    crate::backend::date_input::parse(text.as_str().ok_or("Missing ledger date")?)
}
impl PortfolioController {
    fn open_ledger_version(
        &mut self,
        registry: &Registry,
        intent: &Json,
        account: bool,
    ) -> Result<String, String> {
        let read = self
            .ledger_configuration
            .as_ref()
            .ok_or("Load ledger configuration")?;
        let config = registry.project(&fk("FinancialLedgerConfiguration"), read.found()?)?;
        self.pending_configuration = None;
        let accounts = config["Accounts"]
            .as_array()
            .ok_or("Invalid configured accounts")?;
        let choices = accounts
            .iter()
            .filter(|a| a["State"] == "Active")
            .map(|a| {
                choice(
                    format!(
                        "{}@{}",
                        text(&a["Definition"]["AccountId"]),
                        text(&a["Definition"]["Version"])
                    ),
                    format!(
                        "{} / {} v{}",
                        text(&a["Definition"]["Category"]),
                        text(&a["Definition"]["AccountId"]),
                        text(&a["Definition"]["Version"])
                    ),
                )
            })
            .collect::<Vec<_>>();
        let dialog = if account {
            let selected = accounts
                .iter()
                .find(|a| text(&a["Definition"]["AccountId"]) == field(intent, "account"))
                .ok_or("Select a configured account")?;
            let definition = &selected["Definition"];
            let version = definition["Version"]
                .as_i64()
                .ok_or("Missing account version")?;
            self.editing_account = Some(
                i32::try_from(
                    definition["AccountId"]
                        .as_i64()
                        .ok_or("Missing account identity")?,
                )
                .map_err(|_| "Account identity overflow")?,
            );
            editor(
                "account-version",
                "Edit ledger account",
                670,
                340,
                150,
                vec![
                    input(
                        "id",
                        "Version",
                        format!(
                            "{}: {} ? {}",
                            text(&definition["Category"]),
                            version,
                            version.checked_add(1).ok_or("Account version overflow")?
                        ),
                        "label",
                        false,
                        None,
                    ),
                    input(
                        "normal",
                        "Normal side",
                        definition["NormalSide"].clone(),
                        "choice",
                        true,
                        Some(vec![choice("Debit", "Debit"), choice("Credit", "Credit")]),
                    ),
                    input(
                        "dimension",
                        "Require Fund dimension",
                        if definition["FundDimensionRequired"] == true {
                            "True"
                        } else {
                            "False"
                        },
                        "check",
                        true,
                        None,
                    ),
                    input("reason", "Audit reason", "", "multiline", true, None),
                ],
                "Review and save",
            )
        } else {
            let selected = config["Rules"]
                .as_array()
                .ok_or("Invalid configured rules")?
                .iter()
                .find(|r| r["Definition"]["RuleId"] == field(intent, "rule"))
                .ok_or("Select a configured rule")?;
            let definition = &selected["Definition"];
            let version = definition["Version"]
                .as_i64()
                .ok_or("Missing rule version")?;
            self.editing_rule = Some(
                uuid::Uuid::parse_str(
                    definition["RuleId"]
                        .as_str()
                        .ok_or("Missing rule identity")?,
                )
                .map_err(|_| "Invalid rule identity")?,
            );
            let binding = |key: &str| {
                if definition[key].is_null() {
                    String::new()
                } else {
                    format!(
                        "{}@{}",
                        text(&definition[key]["AccountId"]),
                        text(&definition[key]["Version"])
                    )
                }
            };
            let mut optional = vec![choice("", "None")];
            optional.extend(choices.clone());
            let context = self.data.context.as_ref().ok_or("Load API time")?;
            let today =
                crate::backend::time_zone::local_date("America/New_York", context.as_of_utc)?;
            editor(
                "rule-version",
                "Edit posting rule",
                670,
                480,
                150,
                vec![
                    input(
                        "id",
                        "Version",
                        format!(
                            "{}: {} ? {}",
                            text(&definition["Kind"]),
                            version,
                            version.checked_add(1).ok_or("Rule version overflow")?
                        ),
                        "label",
                        false,
                        None,
                    ),
                    input(
                        "debit",
                        "Debit",
                        binding("Debit"),
                        "choice",
                        true,
                        Some(choices.clone()),
                    ),
                    input(
                        "credit",
                        "Credit",
                        binding("Credit"),
                        "choice",
                        true,
                        Some(choices),
                    ),
                    input(
                        "asset",
                        "Valuation asset",
                        binding("ValuationAsset"),
                        "choice",
                        true,
                        Some(optional.clone()),
                    ),
                    input(
                        "pnl",
                        "Unrealized P&L",
                        binding("UnrealizedPnl"),
                        "choice",
                        true,
                        Some(optional),
                    ),
                    input(
                        "confirmed",
                        "Require confirmed movement",
                        if definition["RequiresConfirmedMovement"] == true {
                            "True"
                        } else {
                            "False"
                        },
                        "check",
                        true,
                        None,
                    ),
                    input(
                        "effective",
                        "Effective from",
                        today.to_string(),
                        "date",
                        true,
                        None,
                    ),
                    input("reason", "Audit reason", "", "multiline", true, None),
                ],
                "Review and save",
            )
        };
        self.put_ledger_dialog(dialog);
        Ok("Exact version editor opened".into())
    }
    fn configuration_outcome_dialog(&mut self) -> Result<(), String> {
        let pending = self
            .pending_configuration
            .as_ref()
            .ok_or("No retained configuration")?;
        let phase = pending["Phase"]
            .as_i64()
            .ok_or("Invalid configuration phase")?;
        let title = match phase {
            2 => "Saved",
            3 => "Expired",
            _ => "Check outcome",
        };
        let dialog = self.dialogs.last_mut().ok_or("Ledger editor closed")?;
        for field in dialog.fields.iter_mut() {
            field.enabled = false;
        }
        for button in dialog.buttons.iter_mut() {
            if matches!(
                button.as_str(),
                "Post" | "Save" | "Review and save" | "Check outcome" | "Saved" | "Expired"
            ) {
                *button = title.into();
            }
        }
        dialog.button_enabled = Some(BTreeMap::from([
            ((title).to_string(), matches!(phase, 0 | 1)),
            ("Close".into(), true),
        ]));
        dialog.status = text(&pending["Message"]);
        Ok(())
    }
    pub(super) async fn submit_configuration(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<(), String> {
        let pending = self
            .pending_configuration
            .as_ref()
            .ok_or("Prepare a configuration request first")?
            .clone();
        let store = self.operation_store(true)?;
        let result = if pending["Phase"] == 0 {
            store.submit(backend, registry, pending, deadline).await
        } else {
            let id = uuid::Uuid::parse_str(
                pending["Request"]["OperationId"]
                    .as_str()
                    .unwrap_or_default(),
            )
            .map_err(|_| "Invalid retained configuration identity")?;
            store.recover(backend, registry, id, deadline).await
        };
        match result {
            Ok(pending) => {
                self.pending_configuration = Some(pending);
                self.configuration_outcome_dialog()
            }
            Err(error) => {
                self.configuration_outcome_dialog()?;
                Err(error)
            }
        }
    }
    pub(super) async fn ledger_action(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let key = self
            .dialogs
            .last()
            .map(|d| d.key.clone())
            .ok_or("Open ledger administration")?;
        let action = field(intent, "action");
        // Keep form values for error review; no field can replace the retained
        // operation's bytes once preparation has created that operation identity.
        if let Some(fields) = intent["fields"].as_object() {
            if let Some(dialog) = self.dialogs.last_mut() {
                for input in dialog.fields.iter_mut() {
                    if let Some(value) = fields.get(input.key.as_str()).and_then(Json::as_str) {
                        input.value = value.into();
                    }
                }
            }
        }
        if action == "Close" {
            self.dialogs.pop();
            return Ok("Ledger dialog closed".into());
        }
        if key == "ledger" {
            if action == "Refresh" {
                return self.open_ledger(backend, registry, deadline).await;
            }
            if action == "Set up book" {
                return self
                    .open_ledger_editor(backend, registry, "setup", deadline)
                    .await;
            }
            if matches!(action, "Apply" | "Check outcome") {
                if !self
                    .pending_configuration
                    .as_ref()
                    .is_some_and(|p| matches!(p["Phase"].as_i64(), Some(0 | 1)))
                {
                    if action == "Check outcome" {
                        return Err("No unresolved ledger operation is selected".into());
                    }
                    let control = field(intent, "control");
                    if control == "RefreshAuthority" {
                        return self
                            .open_ledger_editor(backend, registry, "authority", deadline)
                            .await;
                    }
                    if matches!(control, "AddAccountVersion" | "AddPostingRuleVersion") {
                        return self.open_ledger_version(
                            registry,
                            intent,
                            control == "AddAccountVersion",
                        );
                    }
                    let context = BusinessContext::load(backend).await?;
                    let portfolio = self.ledger_portfolio(registry)?;
                    self.pending_configuration = Some(financial_controls::control(
                        registry,
                        &context,
                        portfolio,
                        self.ledger_configuration
                            .as_ref()
                            .ok_or("Load ledger configuration")?,
                        control,
                        intent["fields"]
                            .as_object()
                            .ok_or("Invalid ledger inputs")?,
                    )?);
                    self.data.context = Some(context);
                }
                self.submit_configuration(backend, registry, deadline)
                    .await?;
                self.open_ledger(backend, registry, deadline).await?;
                return Ok("Ledger control outcome updated".into());
            }
        }
        if action == "Prepare" {
            return self
                .prepare_ledger_draft(backend, registry, intent, deadline)
                .await;
        }
        if matches!(action, "Save" | "Review and save" | "Check outcome") {
            if self.pending_configuration.is_none() {
                let context = BusinessContext::load(backend).await?;
                let portfolio = self.ledger_portfolio(registry)?;
                let fields = intent["fields"]
                    .as_object()
                    .ok_or("Invalid ledger fields")?;
                let pending = match key.as_str() {
                    "setup" => financial_controls::backend_draft(
                        registry,
                        &context,
                        portfolio,
                        0,
                        self.book_draft.as_ref().ok_or("Prepare the book first")?,
                        field(intent, "reason"),
                        false,
                    )?,
                    "authority" => {
                        let read = self
                            .authority_draft
                            .as_ref()
                            .ok_or("Prepare the authority first")?;
                        let draft =
                            member(registry, "FinancialAuthorityDraft", read.found()?, "Draft")?;
                        financial_controls::backend_draft(
                            registry,
                            &context,
                            portfolio,
                            read.financial_revision,
                            &draft,
                            field(intent, "reason"),
                            true,
                        )?
                    }
                    "account-version" => financial_controls::edit_account(
                        registry,
                        &context,
                        portfolio,
                        self.ledger_configuration
                            .as_ref()
                            .ok_or("Load ledger configuration")?,
                        self.editing_account
                            .ok_or("No account version is selected")?,
                        fields,
                    )?,
                    "rule-version" => financial_controls::edit_rule(
                        registry,
                        &context,
                        portfolio,
                        self.ledger_configuration
                            .as_ref()
                            .ok_or("Load ledger configuration")?,
                        self.editing_rule.ok_or("No rule version is selected")?,
                        fields,
                    )?,
                    _ => return Err("No financial configuration draft is open".into()),
                };
                self.pending_configuration = Some(pending);
                self.data.context = Some(context);
            }
            self.submit_configuration(backend, registry, deadline)
                .await?;
            if matches!(key.as_str(), "account-version" | "rule-version") {
                self.dialogs.pop();
                self.open_ledger(backend, registry, deadline).await?;
                if let Some(dialog) = self.dialogs.last_mut() {
                    dialog.status = text(
                        &self
                            .pending_configuration
                            .as_ref()
                            .ok_or("Missing configuration receipt")?["Message"],
                    );
                }
            }
            return Ok("Configuration outcome updated".into());
        }
        Err("Unsupported ledger administration action".into())
    }
    pub(super) async fn open_ledger_editor(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        view: &str,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        match view {
            "ledger" => {
                self.pending_configuration = None;
                self.open_ledger(backend, registry, deadline).await
            }
            "setup" => {
                self.book_draft = None;
                self.pending_configuration = None;
                let context = BusinessContext::load(backend).await?;
                let portfolio = self.ledger_portfolio(registry)?;
                let parameters =
                    registry.encode(&fk("PrepareFinancialBookRequest"), &BTreeMap::new())?;
                let response = financial::query(
                    backend,
                    registry,
                    &context,
                    "PrepareFinancialBookQuery",
                    portfolio,
                    None,
                    parameters,
                    financial::AccessScope::LedgerEditor,
                    deadline,
                )
                .await?;
                let setup = registry.project(&fk("FinancialBookSetup"), response.found()?)?;
                let today =
                    crate::backend::time_zone::local_date("America/New_York", context.as_of_utc)?;
                use chrono::Datelike;
                let accounts = setup["ExecutionAccounts"]
                    .as_array()
                    .filter(|v| v.len() <= 4096)
                    .ok_or("Invalid execution account choices")?;
                let fields = vec![
                    input(
                        "account",
                        "Execution account",
                        accounts.first().map(text).unwrap_or_default(),
                        "choice",
                        true,
                        Some(accounts.iter().map(|a| choice(text(a), text(a))).collect()),
                    ),
                    input(
                        "start",
                        "Period start",
                        format!("{}-01-01", today.year()),
                        "date",
                        true,
                        None,
                    ),
                    input(
                        "end",
                        "Period end",
                        format!("{}-12-31", today.year()),
                        "date",
                        true,
                        None,
                    ),
                    input("reason", "Reason", "", "multiline", true, None),
                    input(
                        "review",
                        "",
                        format!(
                            "Funds: {}\nPrepare to review generated accounts and posting rules. No capital is added and spending stays disabled.",
                            setup["FundNames"]
                                .as_array()
                                .ok_or("Invalid configured Fund names")?
                                .iter()
                                .map(text)
                                .collect::<Vec<_>>()
                                .join(", ")
                        ),
                        "review",
                        false,
                        None,
                    ),
                ];
                self.put_ledger_dialog(Dialog {
                    key: "setup".into(),
                    title: "Development ledger setup".into(),
                    width: (850) as _,
                    height: (660) as _,
                    caption_width: (145) as _,
                    fields: fields,
                    buttons: vec!["Prepare".into(), "Save".into(), "Close".into()],
                    status: "".into(),
                    grids: None,
                    button_enabled: Some(BTreeMap::from([
                        ("Save".into(), false),
                        ("Prepare".into(), !accounts.is_empty()),
                        ("Close".into(), true),
                    ])),
                    ..Default::default()
                });
                self.data.context = Some(context);
                Ok("Book preparation opened".into())
            }
            "authority" => {
                self.pending_configuration = None;
                self.authority_draft = None;
                let fields = vec![
                    input(
                        "permit",
                        "Permit new spending when all authority is ready",
                        "False",
                        "check",
                        true,
                        None,
                    ),
                    input("reason", "Reason", "", "multiline", true, None),
                    input("review", "", "", "review", false, None),
                ];
                self.put_ledger_dialog(Dialog {
                    key: "authority".into(),
                    title: "Portfolio financial authority".into(),
                    width: (900) as _,
                    height: (660) as _,
                    caption_width: (145) as _,
                    fields: fields,
                    buttons: vec!["Prepare".into(), "Save".into(), "Close".into()],
                    status:
                        "Prepare to review current policy, mandate, envelope and deployment limits."
                            .into(),
                    grids: None,
                    button_enabled: Some(BTreeMap::from([
                        ("Save".into(), false),
                        ("Prepare".into(), true),
                        ("Close".into(), true),
                    ])),
                    ..Default::default()
                });
                Ok("Authority preparation opened".into())
            }
            _ => Err("Unsupported ledger editor".into()),
        }
    }
    pub(super) async fn choose_ledger(&mut self, intent: &Json) -> Result<String, String> {
        let fields = intent["fields"]
            .as_object()
            .ok_or("Invalid ledger fields")?;
        let key = self
            .dialogs
            .last()
            .map(|d| d.key.clone())
            .ok_or("Open a ledger editor")?;
        if let Some(dialog) = self.dialogs.last_mut() {
            for field in dialog.fields.iter_mut() {
                if let Some(value) = fields.get(field.key.as_str()).and_then(Json::as_str) {
                    field.value = value.into();
                }
            }
        }
        if key == "ledger" {
            self.update_ledger_fields()?;
        } else if matches!(key.as_str(), "setup" | "authority") {
            if key == "setup" {
                self.book_draft = None;
            } else {
                self.authority_draft = None;
            }
            let dialog = self.dialogs.last_mut().ok_or("Ledger editor closed")?;
            if key == "authority" {
                for field in dialog.fields.iter_mut() {
                    if field.key == "review" {
                        field.value = "".into();
                    }
                }
            }
            dialog.button_enabled = Some(BTreeMap::from([
                ("Save".into(), false),
                ("Prepare".into(), true),
                ("Close".into(), true),
            ]));
        }
        Ok("Ledger selection updated".into())
    }
    fn ledger_review(&mut self, review: String) -> Result<(), String> {
        let dialog = self.dialogs.last_mut().ok_or("Ledger editor closed")?;
        for field in dialog.fields.iter_mut() {
            if field.key == "review" {
                field.value = review.clone().into();
            }
        }
        dialog.button_enabled = Some(BTreeMap::from([
            ("Save".into(), true),
            ("Prepare".into(), true),
            ("Close".into(), true),
        ]));
        dialog.status =
            "Review the prepared configuration and enter an audit reason before saving.".into();
        Ok(())
    }
    async fn prepare_ledger_draft(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let context = BusinessContext::load(backend).await?;
        let portfolio = self.ledger_portfolio(registry)?;
        let key = self
            .dialogs
            .last()
            .map(|d| d.key.clone())
            .ok_or("Open a ledger preparation editor")?;
        if key == "setup" {
            let start = chrono::NaiveDate::parse_from_str(field(intent, "start"), "%Y-%m-%d")
                .map_err(|_| "Enter period Start as yyyy-MM-dd")?;
            let end = chrono::NaiveDate::parse_from_str(field(intent, "end"), "%Y-%m-%d")
                .map_err(|_| "Enter period End as yyyy-MM-dd")?;
            let request = registry.encode(
                &fk("PrepareFinancialBookRequest"),
                &BTreeMap::from([
                    (
                        "ExecutionAccountReference".into(),
                        field(intent, "account").into(),
                    ),
                    ("PeriodStart".into(), date_only_wire(start)),
                    ("PeriodEnd".into(), date_only_wire(end)),
                ]),
            )?;
            let read = financial::query(
                backend,
                registry,
                &context,
                "PrepareFinancialBookQuery",
                portfolio,
                None,
                request,
                financial::AccessScope::LedgerEditor,
                deadline,
            )
            .await?;
            let draft = member(registry, "FinancialBookSetup", read.found()?, "Draft")?;
            if draft.is_nil() {
                return Err("Book preparation did not return a draft".into());
            }
            let row = registry.project(&fk("LedgerConfigurationRequest"), &draft)?;
            let accounts = row["Accounts"]
                .as_array()
                .ok_or("Invalid prepared accounts")?
                .iter()
                .map(|a| {
                    format!(
                        "Account {}: {}, v{}",
                        text(&a["AccountId"]),
                        text(&a["Category"]),
                        text(&a["Version"])
                    )
                })
                .collect::<Vec<_>>()
                .join("\n");
            let rules = row["Rules"]
                .as_array()
                .ok_or("Invalid prepared rules")?
                .iter()
                .map(|r| text(&r["Kind"]))
                .collect::<Vec<_>>()
                .join(", ");
            let review = format!(
                "Generated book {} | {} | Development\n{accounts}\n\nRules: {rules}\n\nThis creates configuration only. Opening capital and qualification are separate operations.",
                text(&row["BookId"]),
                text(&row["Book"]["Currency"])
            );
            self.book_draft = Some(draft);
            self.ledger_review(review)?;
        } else if key == "authority" {
            let permit = if field(intent, "permit").eq_ignore_ascii_case("true") {
                true
            } else if field(intent, "permit").eq_ignore_ascii_case("false") {
                false
            } else {
                return Err("Choose whether new spending should be permitted".into());
            };
            let parameters = registry.encode(
                &fk("PrepareFinancialAuthorityRequest"),
                &BTreeMap::from([("PermitNewSpending".into(), permit.into())]),
            )?;
            let read = financial::query(
                backend,
                registry,
                &context,
                "PrepareFinancialAuthorityQuery",
                portfolio,
                None,
                parameters,
                financial::AccessScope::LedgerEditor,
                deadline,
            )
            .await?;
            let row = registry.project(&fk("FinancialAuthorityDraft"), read.found()?)?;
            let mut lines = vec![
                format!("Financial revision {}", read.financial_revision),
                row["Notes"]
                    .as_array()
                    .ok_or("Invalid authority notes")?
                    .iter()
                    .map(text)
                    .collect::<Vec<_>>()
                    .join("\r\n"),
                "".into(),
            ];
            for fund in row["Draft"]["Book"]["Funds"]
                .as_array()
                .ok_or("Invalid authority Funds")?
            {
                lines.push(format!(
                    "Fund {}: {}",
                    text(&fund["FundId"]),
                    if fund["CanSpend"] == true {
                        "New spending permitted"
                    } else {
                        "New spending disabled"
                    }
                ));
                for deployment in fund["Deployments"]
                    .as_array()
                    .ok_or("Invalid authority deployments")?
                {
                    let key = &deployment["Reference"]["DeploymentKey"];
                    let key = if key.is_null() {
                        String::new()
                    } else {
                        format!(
                            "CatalogKey {{ Kind = {}, Id = {}, Version = {} }}",
                            text(&key["Kind"]),
                            text(&key["Id"]),
                            text(&key["Version"])
                        )
                    };
                    lines.push(format!(
                        "Deployment {key}: per-trade loss {} USD; valid until {}",
                        amount(&deployment["MaximumRiskPerTrade"])?,
                        utc_roundtrip(
                            chrono::DateTime::parse_from_rfc3339(
                                deployment["Reference"]["ValidUntilUtc"]
                                    .as_str()
                                    .ok_or("Missing authority expiry")?
                            )
                            .map_err(|_| "Invalid authority expiry")?
                            .to_utc()
                        )
                    ));
                    for limit in deployment["Limits"]
                        .as_array()
                        .ok_or("Invalid deployment limits")?
                    {
                        lines.push(format!(
                            "  {}: {} {}",
                            text(&limit["Measure"]),
                            amount(&limit["Maximum"])?,
                            text(&limit["Unit"])
                        ));
                    }
                }
                for limit in fund["Limits"].as_array().ok_or("Invalid Fund limits")? {
                    lines.push(format!(
                        "{} {} / {}: {} {}",
                        text(&limit["ScopeKind"]),
                        text(&limit["ScopeKey"]),
                        text(&limit["Measure"]),
                        if limit["Enabled"] == true {
                            amount(&limit["Maximum"])?
                        } else {
                            "Disabled".into()
                        },
                        text(&limit["Unit"])
                    ));
                }
            }
            self.authority_draft = Some(read);
            self.ledger_review(lines.join("\r\n"))?;
        } else {
            return Err("Open a book or authority preparation editor".into());
        }
        self.data.context = Some(context);
        Ok("Configuration draft prepared for review".into())
    }
    fn ledger_portfolio(&self, registry: &Registry) -> Result<i32, String> {
        let row = registry.project(
            &kind("PortfolioReadModel"),
            self.data.portfolio.as_ref().ok_or("Select a Portfolio")?,
        )?;
        i32::try_from(
            row["PortfolioId"]
                .as_i64()
                .filter(|v| *v > 0)
                .ok_or("Invalid Portfolio identity")?,
        )
        .map_err(|_| "Portfolio identity overflow".into())
    }
    fn put_ledger_dialog(&mut self, dialog: Dialog) {
        if let Some(index) = self.dialogs.iter().position(|d| d.key == dialog.key) {
            self.dialogs[index] = dialog;
        } else {
            self.dialogs.push(dialog);
        }
    }
    pub(super) async fn open_ledger(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let context = BusinessContext::load(backend).await?;
        let portfolio = self.ledger_portfolio(registry)?;
        let parameters = registry.encode(
            &fk("GetFinancialLedgerConfigurationRequest"),
            &BTreeMap::new(),
        )?;
        let read = financial::query(
            backend,
            registry,
            &context,
            "GetFinancialLedgerConfigurationQuery",
            portfolio,
            None,
            parameters,
            financial::AccessScope::LedgerEditor,
            deadline,
        )
        .await?;
        if read.status == 3 {
            return Err("Ledger configuration is unavailable".into());
        }
        let config = read.value.as_ref();
        let row = config
            .map(|v| registry.project(&fk("FinancialLedgerConfiguration"), v))
            .transpose()?
            .unwrap_or(Json::Null);
        let today = crate::backend::time_zone::local_date("America/New_York", context.as_of_utc)?;
        let periods = row["Periods"].as_array().cloned().unwrap_or_default();
        let accounts = row["Accounts"].as_array().cloned().unwrap_or_default();
        let rules = row["Rules"].as_array().cloned().unwrap_or_default();
        let period_choices = periods
            .iter()
            .map(|p| {
                Ok(choice(
                    p["PeriodId"].clone(),
                    format!(
                        "{} - {} ({})",
                        crate::backend::date_display::short(day(&p["StartDate"])?)?,
                        crate::backend::date_display::short(day(&p["EndDate"])?)?,
                        text(&p["State"])
                    ),
                ))
            })
            .collect::<Result<Vec<_>, String>>()?;
        let account_choices = accounts
            .iter()
            .map(|a| {
                choice(
                    text(&a["Definition"]["AccountId"]),
                    format!(
                        "{}: {} ({})",
                        text(&a["Definition"]["AccountId"]),
                        text(&a["Definition"]["Category"]),
                        text(&a["State"])
                    ),
                )
            })
            .collect::<Vec<_>>();
        let rule_choices = rules
            .iter()
            .map(|r| {
                choice(
                    r["Definition"]["RuleId"].clone(),
                    format!(
                        "{} v{} ({})",
                        text(&r["Definition"]["Kind"]),
                        text(&r["Definition"]["Version"]),
                        text(&r["State"])
                    ),
                )
            })
            .collect::<Vec<_>>();
        let controls = [
            ("Reconcile", "Reconcile"),
            ("OpenPeriod", "Open period"),
            ("ClosePeriod", "Close period"),
            ("ReopenPeriod", "Reopen period"),
            ("RetireAccount", "Retire account"),
            ("RetirePostingRule", "Retire rule"),
            ("QualifyDevelopmentBook", "Qualify book"),
            ("RefreshAuthority", "Refresh authority"),
            ("AddAccountVersion", "Edit account"),
            ("AddPostingRuleVersion", "Edit rule"),
        ]
        .into_iter()
        .map(|(v, l)| choice(v, l))
        .collect();
        let fields = vec![
            input(
                "control",
                "Action",
                "Reconcile",
                "choice",
                true,
                Some(controls),
            ),
            input(
                "period",
                "Period",
                periods
                    .first()
                    .map(|p| text(&p["PeriodId"]))
                    .unwrap_or_default(),
                "choice",
                true,
                Some(period_choices),
            ),
            input("start", "Start", today.to_string(), "date", true, None),
            input("end", "End", today.to_string(), "date", true, None),
            input(
                "account",
                "Account",
                accounts
                    .first()
                    .map(|a| text(&a["Definition"]["AccountId"]))
                    .unwrap_or_default(),
                "choice",
                true,
                Some(account_choices),
            ),
            input(
                "rule",
                "Rule",
                rules
                    .first()
                    .map(|r| text(&r["Definition"]["RuleId"]))
                    .unwrap_or_default(),
                "choice",
                true,
                Some(rule_choices),
            ),
            input("reason", "Reason", "", "multiline", true, None),
        ];
        let account_rows = accounts
            .iter()
            .enumerate()
            .map(|(i, a)| {
                cell_row(
                    i.to_string(),
                    ["AccountId", "Version", "Category", "NormalSide"]
                        .into_iter()
                        .map(|key| (key.into(), text(&a["Definition"][key])))
                        .chain(std::iter::once(("State".into(), text(&a["State"]))))
                        .collect(),
                )
            })
            .collect();
        let rule_rows = rules
            .iter()
            .enumerate()
            .map(|(i, r)| {
                cell_row(
                    i.to_string(),
                    BTreeMap::from([
                        ("Kind".into(), text(&r["Definition"]["Kind"])),
                        ("RuleId".into(), text(&r["Definition"]["RuleId"])),
                        ("Version".into(), text(&r["Definition"]["Version"])),
                        (
                            "DebitAccount".into(),
                            text(&r["Definition"]["Debit"]["AccountId"]),
                        ),
                        (
                            "CreditAccount".into(),
                            text(&r["Definition"]["Credit"]["AccountId"]),
                        ),
                        ("State".into(), text(&r["State"])),
                        ("EffectiveFrom".into(), text(&r["EffectiveFrom"])),
                        ("EffectiveTo".into(), text(&r["EffectiveTo"])),
                    ]),
                )
            })
            .collect();
        let pending_store = self.operation_store(true)?;
        let directory = pending_store.directory;
        let pending = tokio::task::spawn_blocking(move || {
            let registry = Registry::compiled()?;
            let mut rows = crate::persistence::journal::Journal::new(directory).list(&|value| {
                crate::persistence::financial_journal::validate(
                    &registry,
                    "ConfigureLedgerCommand",
                    value,
                )
            })?;
            rows.retain(|v| v["Request"]["PortfolioId"] == portfolio);
            Ok::<_, String>(rows)
        })
        .await
        .map_err(|e| e.to_string())??;
        let pending_rows = pending
            .iter()
            .map(|p| {
                let action = p["Request"]["Body"]["Action"]
                    .as_i64()
                    .ok_or("Invalid retained configuration action")?;
                let action = registry
                    .enum_names(&fk("LedgerConfigurationAction"))?
                    .into_iter()
                    .find(|name| {
                        registry.enum_value(&fk("LedgerConfigurationAction"), name) == Ok(action)
                    })
                    .ok_or("Invalid retained configuration action")?;
                let phase = match p["Phase"].as_i64() {
                    Some(0) => "Prepared",
                    Some(1) => "OutcomeUnknown",
                    Some(2) => "Committed",
                    Some(3) => "ExpiredWithoutPosting",
                    _ => return Err("Invalid retained configuration phase".into()),
                };
                Ok(cell_row(
                    text(&p["Request"]["OperationId"]),
                    BTreeMap::from([
                        ("OperationId".into(), text(&p["Request"]["OperationId"])),
                        ("Action".into(), action),
                        ("Phase".into(), phase.into()),
                        ("Message".into(), text(&p["Message"])),
                    ]),
                ))
            })
            .collect::<Result<Vec<_>, String>>()?;
        let reconciliation = config
            .map(|config| {
                member(
                    registry,
                    "FinancialLedgerConfiguration",
                    config,
                    "LatestReconciliation",
                )
            })
            .transpose()?
            .filter(|v| !v.is_nil());
        let differences = if let Some(value) = &reconciliation {
            member(registry, "LedgerReconciliationResult", value, "Differences")?
                .as_array()
                .ok_or("Invalid reconciliation differences")?
                .clone()
        } else {
            vec![]
        };
        let grids = vec![
            typed_grid(
                registry,
                "periods",
                "FinancialLedgerPeriod",
                &rows(registry, "FinancialLedgerConfiguration", config, "Periods")?,
            )?,
            grid(
                "accounts",
                ["AccountId", "Version", "Category", "NormalSide", "State"]
                    .map(str::to_string)
                    .to_vec(),
                account_rows,
                None,
            ),
            grid(
                "rules",
                [
                    "Kind",
                    "RuleId",
                    "Version",
                    "DebitAccount",
                    "CreditAccount",
                    "State",
                    "EffectiveFrom",
                    "EffectiveTo",
                ]
                .map(str::to_string)
                .to_vec(),
                rule_rows,
                None,
            ),
            typed_grid(
                registry,
                "reconciliation",
                "LedgerBalanceDifference",
                &differences,
            )?,
            grid(
                "pending-controls",
                ["OperationId", "Action", "Phase", "Message"]
                    .map(str::to_string)
                    .to_vec(),
                pending_rows,
                None,
            ),
        ];
        let status = if config.is_none() {
            "No ledger book is configured for this Portfolio.".into()
        } else {
            let evidence = &row["LatestReconciliation"];
            format!(
                "Book {} | {} | {} | Revision {}\n{}",
                text(&row["BookId"]),
                text(&row["Currency"]),
                text(&row["OperatingState"]),
                read.financial_revision,
                if evidence.is_null() {
                    "No ledger reconciliation has been recorded.".into()
                } else {
                    format!(
                        "Ledger reconciliation: debits {}, credits {}, differences {}; source cut {}",
                        amount(&evidence["Debits"])?,
                        amount(&evidence["Credits"])?,
                        differences.len(),
                        text(&evidence["SourceCut"])
                    )
                }
            )
        };
        let mut dialog = Dialog {
            key: "ledger".into(),
            title: "Portfolio ledger administration".into(),
            width: (1120) as _,
            height: (710) as _,
            caption_width: (95) as _,
            fields: fields,
            buttons: (if config.is_none() {
                vec!["Set up book", "Refresh", "Close"]
            } else {
                vec!["Apply", "Refresh", "Close"]
            })
            .into_iter()
            .map(|v| v.to_string())
            .collect(),
            status: (status).to_string(),
            grids: Some(grids),
            button_enabled: None,
            ..Default::default()
        };
        if config.is_none() {
            dialog.button_enabled = Some(BTreeMap::from([
                (
                    "Set up book".into(),
                    !pending
                        .iter()
                        .any(|p| matches!(p["Phase"].as_i64(), Some(0 | 1))),
                ),
                ("Refresh".into(), true),
                ("Close".into(), true),
            ]));
        }
        self.pending_configurations = pending;
        self.ledger_configuration = Some(read);
        self.data.context = Some(context);
        self.put_ledger_dialog(dialog);
        self.update_ledger_fields()?;
        if self
            .pending_configuration
            .as_ref()
            .is_some_and(|p| matches!(p["Phase"].as_i64(), Some(0 | 1)))
        {
            if let Some(dialog) = self.dialogs.iter_mut().find(|d| d.key == "ledger") {
                for field in dialog.fields.iter_mut() {
                    field.enabled = false;
                }
                dialog.buttons = ["Check outcome", "Refresh", "Close"]
                    .into_iter()
                    .map(str::to_string)
                    .collect();
                dialog.status = text(&self.pending_configuration.as_ref().unwrap()["Message"]);
            }
        }
        Ok("Ledger configuration loaded".into())
    }
    pub(super) fn update_ledger_fields(&mut self) -> Result<(), String> {
        let configured = self
            .ledger_configuration
            .as_ref()
            .is_some_and(|v| v.value.is_some());
        let dialog = self
            .dialogs
            .iter_mut()
            .find(|d| d.key == "ledger")
            .ok_or("Open ledger administration")?;
        let fields = &mut dialog.fields;
        let control = fields
            .iter()
            .find(|f| f.key == "control")
            .map(|f| f.value.clone())
            .unwrap_or_default();
        for field in fields {
            let enabled = match field.key.as_str() {
                "period" => matches!(control.as_str(), "ClosePeriod" | "ReopenPeriod"),
                "start" | "end" => control == "OpenPeriod",
                "account" => matches!(control.as_str(), "RetireAccount" | "AddAccountVersion"),
                "rule" => matches!(
                    control.as_str(),
                    "RetirePostingRule" | "AddPostingRuleVersion"
                ),
                _ => true,
            };
            field.enabled = (configured && enabled).into();
        }
        Ok(())
    }
}
