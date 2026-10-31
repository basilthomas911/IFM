//! Cash movement editor. The form references a backend rule/configuration and
//! preserves one durable operation after submission, including unknown outcomes.
use super::editors::input;
use super::*;
use crate::services::{
    business_context::BusinessContext, financial, financial_operations::OperationStore,
    financial_preparation,
};
fn fk(name: &str) -> String {
    format!("{}.{name}", financial::ROOT)
}
fn label(kind: &str) -> &str {
    match kind {
        "DepositConfirmed" => "Confirmed deposit",
        "OpeningBalance" => "Development opening capital",
        "WithdrawalRequested" => "Request withdrawal",
        "WithdrawalSettled" => "Confirm selected withdrawal",
        "WithdrawalCancelled" => "Cancel selected withdrawal",
        "Reversal" => "Reverse selected journal",
        _ => kind,
    }
}
impl PortfolioController {
    pub(super) fn operation_store(&self, configuration: bool) -> Result<OperationStore, String> {
        let directory = if let Some(root) = &self.journal_root {
            root.join(if configuration {
                "PendingFinancialConfigurations"
            } else {
                "PendingFinancialOperations"
            })
        } else {
            crate::persistence::financial_journal::current_user_directory(configuration)?
        };
        Ok(OperationStore {
            directory,
            configuration,
        })
    }
    pub(super) async fn open_posting(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        day: Option<chrono::NaiveDate>,
        keep: Option<&serde_json::Map<String, Json>>,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let context = BusinessContext::load(backend).await?;
        let day = if let Some(day) = day {
            day
        } else {
            crate::backend::time_zone::local_date("America/New_York", context.as_of_utc)?
        };
        let fund = registry.project(
            &kind("FundMandateReadModel"),
            self.data.fund.as_ref().ok_or("Select a Fund")?,
        )?;
        let portfolio = i32::try_from(
            fund["PortfolioId"]
                .as_i64()
                .ok_or("Invalid Portfolio identity")?,
        )
        .map_err(|_| "Invalid Portfolio identity")?;
        let id = i32::try_from(fund["FundId"].as_i64().ok_or("Invalid Fund identity")?)
            .map_err(|_| "Invalid Fund identity")?;
        let parameters = registry.encode(
            &fk("GetFinancialPostingConfigurationRequest"),
            &BTreeMap::from([("AccountingDate".into(), date_only_wire(day))]),
        )?;
        let read = financial::query(
            backend,
            registry,
            &context,
            "GetFinancialPostingConfigurationQuery",
            portfolio,
            Some(id),
            parameters,
            financial::AccessScope::LedgerEditor,
            deadline,
        )
        .await?;
        let config = read.found()?;
        let row = registry.project(&fk("FinancialPostingConfiguration"), config)?;
        if row["FundId"] != id || row["AccountingDate"] != day.to_string() {
            return Err("Posting configuration crossed the selected Fund/date".into());
        }
        let related = self
            .related_transaction
            .as_ref()
            .map(|v| registry.project(&fk("FinancialTransactionRow"), v))
            .transpose()?
            .unwrap_or(Json::Null);
        self.related_receipt = None;
        if !related.is_null() {
            let parameters = registry.encode(
                &fk("GetPostingReceiptRequest"),
                &BTreeMap::from([(
                    "OperationId".into(),
                    related["OperationId"]
                        .as_str()
                        .ok_or("Invalid related operation")?
                        .into(),
                )]),
            )?;
            let receipt = financial::query(
                backend,
                registry,
                &context,
                "GetPostingReceiptQuery",
                portfolio,
                Some(id),
                parameters,
                financial::AccessScope::LedgerEditor,
                deadline,
            )
            .await?;
            if let Some(outcome) = receipt.value {
                let posting = crate::services::strategy::member(
                    registry,
                    &fk("FinancialOperationOutcome"),
                    &outcome,
                    "Posting",
                )?;
                if !posting.is_nil() {
                    self.related_receipt = Some(crate::services::strategy::member(
                        registry,
                        &fk("LedgerPostingCompletedEvent"),
                        &posting,
                        "Receipt",
                    )?);
                }
            }
        }
        let mut choices = vec![];
        for rule in row["Rules"]
            .as_array()
            .filter(|v| v.len() <= 4096)
            .ok_or("Invalid posting rules")?
        {
            let kind = rule["Kind"].as_str().ok_or("Invalid posting kind")?;
            let allowed = matches!(kind, "DepositConfirmed" | "WithdrawalRequested")
                || kind == "OpeningBalance" && row["AllowDevelopmentOpeningCapital"] == true
                || kind == "Reversal" && related["JournalId"].as_i64().is_some_and(|v| v > 0)
                || matches!(kind, "WithdrawalSettled" | "WithdrawalCancelled")
                    && related["Transaction"]["TransactionKind"] == "WithdrawalRequested";
            if allowed {
                choices.push(Choice {
                    value: text(&(rule["RuleId"])),
                    label: format!("{} (version {})", label(kind), text(&rule["Version"])),
                    enabled: true,
                    ..Default::default()
                });
            }
        }
        let mut amount = input(
            "amount",
            "Amount (USD)",
            related["Transaction"]["Amount"]
                .as_str()
                .map(|v| v.trim_start_matches('-'))
                .unwrap_or("0.01"),
            "number",
            true,
            None,
        );
        amount.minimum = 0.01;
        amount.maximum = 1000000000.0;
        let mut fields = vec![
            input(
                "rule",
                "Transaction",
                choices.first().map(|v| v.value.clone()).unwrap_or_default(),
                "choice",
                true,
                Some(choices.clone()),
            ),
            amount,
            input(
                "accountingDate",
                "Accounting date",
                day.to_string(),
                "date",
                true,
                None,
            ),
            input(
                "valueDate",
                "Value date",
                day.to_string(),
                "date",
                true,
                None,
            ),
            input("description", "Description", "", "multiline", true, None),
            input("reference", "Movement reference", "", "text", true, None),
            input(
                "confirmed",
                "I confirm this cash movement",
                "False",
                "check",
                true,
                None,
            ),
            input(
                "original",
                "Original transaction",
                if related.is_null() {
                    "None selected".into()
                } else {
                    format!(
                        "Transaction {}; journal {}",
                        text(&related["TransactionId"]),
                        text(&related["JournalId"])
                    )
                },
                "text",
                false,
                None,
            ),
        ];
        if let Some(keep) = keep {
            for field in &mut fields {
                if let Some(value) = keep.get(field.key.as_str()).and_then(Json::as_str) {
                    field.value = value.into();
                }
            }
        }
        let dialog = Dialog {
            key: "posting".into(),
            title: "Fund Transaction".into(),
            width: (660) as _,
            height: (360) as _,
            caption_width: (280) as _,
            fields: fields,
            buttons: vec!["Post".into(), "Close".into()],
            status: (if row["PeriodOpen"] == true {
                "Choose a configured transaction. Generated identities are assigned when you post."
            } else {
                "This accounting period is closed."
            })
            .to_string(),
            grids: None,
            button_enabled: Some(BTreeMap::from([
                (
                    "Post".into(),
                    row["PeriodOpen"] == true && !choices.is_empty(),
                ),
                ("Close".into(), true),
            ])),
            ..Default::default()
        };
        self.posting_configuration = Some(read);
        self.data.context = Some(context);
        if let Some(index) = self.dialogs.iter().position(|d| d.key == "posting") {
            self.dialogs[index] = dialog;
        } else {
            self.dialogs.push(dialog);
        }
        self.update_posting_rule(registry)?;
        Ok("Transaction editor loaded".into())
    }
    fn update_posting_rule(&mut self, registry: &Registry) -> Result<(), String> {
        let Some(dialog) = self.dialogs.iter_mut().find(|d| d.key == "posting") else {
            return Ok(());
        };
        let fields = &mut dialog.fields;
        let key = fields
            .iter()
            .find(|f| f.key == "rule")
            .map(|f| f.value.clone())
            .unwrap_or_default();
        let config = self
            .posting_configuration
            .as_ref()
            .and_then(|v| v.value.as_ref())
            .map(|v| registry.project(&fk("FinancialPostingConfiguration"), v))
            .transpose()?
            .unwrap_or(Json::Null);
        let opening = config["Rules"]
            .as_array()
            .and_then(|rules| rules.iter().find(|rule| rule["RuleId"] == key))
            .is_some_and(|rule| rule["Kind"] == "OpeningBalance");
        for field in fields {
            match field.key.as_str() {
                "confirmed" => {
                    field.caption = if opening {
                        "Development capital; no external movement"
                    } else {
                        "I confirm this cash movement"
                    }
                    .into();
                    field.enabled = (!opening).into();
                    if opening {
                        field.value = "false".into();
                    }
                }
                "reference" => {
                    field.enabled = (!opening).into();
                    if opening {
                        field.value = "".into();
                    }
                }
                _ => {}
            }
        }
        Ok(())
    }
    pub(super) async fn choose_posting(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        if self.pending_posting.is_some() {
            return Ok("Original financial operation retained".into());
        }
        let fields = intent["fields"]
            .as_object()
            .ok_or("Invalid posting fields")?;
        let day = crate::backend::date_input::parse(field(intent, "accountingDate"))?;
        let config = self
            .posting_configuration
            .as_ref()
            .and_then(|v| v.value.as_ref())
            .map(|v| registry.project(&fk("FinancialPostingConfiguration"), v))
            .transpose()?
            .unwrap_or(Json::Null);
        if config["AccountingDate"] != day.to_string() {
            return self
                .open_posting(backend, registry, Some(day), Some(fields), deadline)
                .await;
        }
        if let Some(dialog) = self.dialogs.iter_mut().find(|d| d.key == "posting") {
            for input in dialog.fields.iter_mut() {
                if let Some(value) = fields.get(input.key.as_str()).and_then(Json::as_str) {
                    input.value = value.into();
                }
            }
        }
        self.update_posting_rule(registry)?;
        Ok("Posting selection updated".into())
    }
    pub(super) fn pending_posting_dialog(&mut self, registry: &Registry) -> Result<(), String> {
        let pending = self.pending_posting.as_ref().ok_or("No retained posting")?;
        let phase = pending["Phase"].as_i64().ok_or("Invalid pending phase")?;
        let dialog = self
            .dialogs
            .iter_mut()
            .find(|d| d.key == "posting")
            .ok_or("Posting editor closed")?;
        for field in dialog.fields.iter_mut() {
            field.enabled = false;
        }
        dialog.buttons = ["Check outcome", "Close"]
            .into_iter()
            .map(str::to_string)
            .collect();
        dialog.button_enabled = Some(BTreeMap::from([
            ("Check outcome".into(), matches!(phase, 0 | 1)),
            ("Close".into(), true),
        ]));
        dialog.status = text(&pending["Message"]);
        let _ = registry;
        Ok(())
    }
    pub(super) async fn post_or_reconcile(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let store = self.operation_store(false)?;
        let result = if let Some(pending) = &self.pending_posting {
            let id = uuid::Uuid::parse_str(
                pending["Request"]["OperationId"]
                    .as_str()
                    .unwrap_or_default(),
            )
            .map_err(|_| "Invalid retained posting identity")?;
            store.recover(backend, registry, id, deadline).await
        } else {
            let context = BusinessContext::load(backend).await?;
            let fund = registry.project(
                &kind("FundMandateReadModel"),
                self.data.fund.as_ref().ok_or("Select a Fund")?,
            )?;
            let portfolio = i32::try_from(
                fund["PortfolioId"]
                    .as_i64()
                    .ok_or("Invalid Portfolio identity")?,
            )
            .map_err(|_| "Invalid Portfolio identity")?;
            let id = i32::try_from(fund["FundId"].as_i64().ok_or("Invalid Fund identity")?)
                .map_err(|_| "Invalid Fund identity")?;
            let read = self
                .posting_configuration
                .as_ref()
                .ok_or("Load posting configuration")?;
            let pending = financial_preparation::posting(
                registry,
                &context,
                portfolio,
                id,
                read.financial_revision,
                read.found()?,
                intent["fields"]
                    .as_object()
                    .ok_or("Invalid posting fields")?,
                self.related_transaction.as_ref(),
                self.related_receipt.as_ref(),
            )?;
            self.pending_posting = Some(pending.clone());
            self.data.context = Some(context);
            store.submit(backend, registry, pending, deadline).await
        };
        match result {
            Ok(pending) => {
                self.pending_posting = Some(pending);
                self.pending_posting_dialog(registry)?;
                Ok("Financial outcome updated".into())
            }
            Err(error) => {
                self.pending_posting_dialog(registry)?;
                Err(error)
            }
        }
    }
}
