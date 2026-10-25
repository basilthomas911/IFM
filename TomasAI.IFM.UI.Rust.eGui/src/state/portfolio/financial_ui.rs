//! Financial panel publications use committed backend reads and retained local
//! operation receipts. Display history never grants financial write authority.
use super::*;
use super::{editors::input, financial_data::LedgerData};
use crate::services::{business_context::BusinessContext, financial};
fn fk(name: &str) -> String {
    format!("{}.{name}", financial::ROOT)
}
pub(super) fn amount(value: &Json) -> Result<String, String> {
    let raw = value.as_str().ok_or("Missing exact financial amount")?;
    let fixed = crate::services::outlook::decimal_fixed(raw, 2, false)?;
    let (whole, fraction) = fixed.split_once('.').unwrap_or((&fixed, ""));
    let negative = whole.starts_with('-');
    let whole = whole.trim_start_matches('-');
    let mut output = if negative { "-".into() } else { String::new() };
    for (index, ch) in whole.chars().enumerate() {
        if index > 0 && (whole.len() - index) % 3 == 0 {
            output.push(',');
        }
        output.push(ch);
    }
    if !fraction.is_empty() {
        output.push('.');
        output.push_str(fraction);
    }
    Ok(output)
}
pub(super) fn typed_grid(
    registry: &Registry,
    key: &str,
    name: &str,
    values: &[Value],
) -> Result<Json, String> {
    let kind = fk(name);
    let columns = display::field_names(&kind)?;
    let mut rows = vec![];
    for (index, value) in values.iter().enumerate() {
        let mut fields = display::fields(registry, &kind, value)?
            .into_iter()
            .collect::<BTreeMap<_, _>>();
        if matches!(key, "balances" | "journal") {
            let row = registry.project(&kind, value)?;
            for name in ["Debits", "Credits", "Balance", "Amount", "Debit", "Credit"] {
                if fields.contains_key(name) {
                    fields.insert(name.into(), amount(&row[name])?);
                }
            }
        }
        rows.push(cell_row(index.to_string(), fields));
    }
    Ok(grid(key, columns, rows, None))
}
fn rows_member(
    registry: &Registry,
    name: &str,
    value: Option<&Value>,
    member: &str,
) -> Result<Vec<Value>, String> {
    let Some(value) = value else {
        return Ok(vec![]);
    };
    crate::services::strategy::member(registry, &fk(name), value, member)?
        .as_array()
        .filter(|v| v.len() <= 100_000)
        .cloned()
        .ok_or("Invalid financial row collection".into())
}
impl PortfolioController {
    pub(super) async fn open_financials(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let fund = registry.project(
            &kind("FundMandateReadModel"),
            self.data.fund.as_ref().ok_or("Select a Fund")?,
        )?;
        let portfolio = fund["PortfolioId"]
            .as_i64()
            .and_then(|id| i32::try_from(id).ok())
            .filter(|id| *id > 0)
            .ok_or("Invalid Portfolio identity")?;
        let id = fund["FundId"]
            .as_i64()
            .and_then(|id| i32::try_from(id).ok())
            .filter(|id| *id > 0)
            .ok_or("Invalid Fund identity")?;
        let context = BusinessContext::load(backend).await?;
        self.ledger_data =
            Some(LedgerData::load(backend, registry, &context, portfolio, id, deadline).await);
        self.related_transaction = self
            .ledger_data
            .as_ref()
            .ok_or("Financial panels did not load")?
            .transaction_rows()?
            .first()
            .cloned();
        self.related_receipt = None;
        self.data.context = Some(context);
        self.refresh_financial_journals(registry).await?;
        Ok("Financial panels loaded".into())
    }
    pub(super) async fn refresh_financial_journals(
        &mut self,
        registry: &Registry,
    ) -> Result<(), String> {
        let state = self
            .ledger_data
            .as_ref()
            .ok_or("Load financial data first")?;
        let portfolio = state.portfolio;
        let id = state.fund;
        let journal_root = self.journal_root.clone();
        self.pending_postings = tokio::task::spawn_blocking(move || {
            let registry = Registry::compiled()?;
            let directory = if let Some(root) = journal_root {
                root.join("PendingFinancialOperations")
            } else {
                crate::persistence::financial_journal::current_user_directory(false)?
            };
            let mut rows = crate::persistence::journal::Journal::new(directory).list(&|row| {
                crate::persistence::financial_journal::validate(
                    &registry,
                    "PostFundTransactionCommand",
                    row,
                )
            })?;
            rows.retain(|row| {
                row["Request"]["PortfolioId"] == portfolio && row["Request"]["Body"]["FundId"] == id
            });
            rows.sort_by_key(|row| {
                std::cmp::Reverse(
                    chrono::DateTime::parse_from_rfc3339(
                        row["Request"]["RequestedAtUtc"]
                            .as_str()
                            .unwrap_or_default(),
                    )
                    .ok()
                    .map(|v| v.to_utc()),
                )
            });
            Ok::<_, String>(rows)
        })
        .await
        .map_err(|e| e.to_string())??;
        self.update_financial_dialog(registry)?;
        Ok(())
    }
    pub(super) fn update_financial_dialog(&mut self, registry: &Registry) -> Result<(), String> {
        let state = self.ledger_data.as_ref().ok_or("Load Financials first")?;
        let fund = registry.project(
            &kind("FundMandateReadModel"),
            self.data.fund.as_ref().ok_or("Select a Fund")?,
        )?;
        if fund["PortfolioId"] != state.portfolio || fund["FundId"] != state.fund {
            return Err("Financial panels belong to a different Fund".into());
        }
        let balance = state.balances.as_ref().and_then(|v| v.value.as_ref());
        let row = balance
            .map(|v| registry.project(&fk("FinancialBalanceSnapshot"), v))
            .transpose()?
            .unwrap_or(Json::Null);
        let mut transactions = vec![];
        for value in state.transaction_rows()? {
            let item = registry.project(&fk("FinancialTransactionRow"), &value)?;
            let tx = &item["Transaction"];
            transactions.push(cell_row(
                text(&item["OperationId"]),
                BTreeMap::from([
                    ("TransactionId".into(), text(&item["TransactionId"])),
                    ("AccountingDate".into(), text(&tx["AccountingDate"])),
                    ("Type".into(), text(&tx["TransactionKind"])),
                    ("Amount".into(), amount(&tx["Amount"])?),
                    ("Currency".into(), text(&tx["Currency"])),
                    ("Description".into(), text(&tx["Description"])),
                    ("JournalId".into(), text(&item["JournalId"])),
                    ("OperationId".into(), text(&item["OperationId"])),
                ]),
            ));
        }
        let mut reservations = vec![];
        for (index, value) in state.reservation_rows()?.iter().enumerate() {
            let item = registry.project(&fk("FinancialReservationView"), value)?;
            let receipt = &item["OriginalReceipt"];
            let current = &item["Current"];
            reservations.push(cell_row(
                index.to_string(),
                BTreeMap::from([
                    ("ReservationId".into(), text(&receipt["ReservationId"])),
                    ("OrderId".into(), text(&receipt["OrderId"])),
                    ("Status".into(), text(&current["Status"])),
                    ("Units".into(), text(&current["StrategyUnits"])),
                    ("Filled".into(), text(&current["FilledUnits"])),
                    ("Closed".into(), text(&current["ClosedUnits"])),
                    ("Cancelled".into(), text(&current["CancelledUnits"])),
                    ("Remaining".into(), text(&current["RemainingUnits"])),
                    ("Version".into(), text(&current["Version"])),
                ]),
            ));
        }
        let pending = self
            .pending_postings
            .iter()
            .map(|value| {
                let request = &value["Request"];
                let tx = &request["Body"];
                let phase = value["Phase"]
                    .as_i64()
                    .ok_or("Invalid pending posting phase")?;
                let phase = match phase {
                    0 => "Prepared",
                    1 => "OutcomeUnknown",
                    2 => "Committed",
                    3 => "ExpiredWithoutPosting",
                    _ => return Err("Invalid pending posting phase"),
                };
                let transaction = tx["TransactionKind"]
                    .as_i64()
                    .ok_or("Invalid pending transaction kind")?;
                let kind = registry
                    .enum_names(&fk("LedgerTransactionKind"))
                    .map_err(|_| "Missing transaction kind metadata")?
                    .into_iter()
                    .find(|name| {
                        registry.enum_value(&fk("LedgerTransactionKind"), name) == Ok(transaction)
                    })
                    .ok_or("Invalid pending transaction kind")?;
                Ok(cell_row(
                    text(&request["OperationId"]),
                    BTreeMap::from([
                        ("OperationId".into(), text(&request["OperationId"])),
                        ("Type".into(), kind),
                        (
                            "Amount".into(),
                            if tx["Amount"].is_number() {
                                tx["Amount"].to_string()
                            } else {
                                text(&tx["Amount"])
                            },
                        ),
                        ("Phase".into(), phase.into()),
                        ("Message".into(), text(&value["Message"])),
                    ]),
                ))
            })
            .collect::<Result<Vec<_>, &str>>()
            .map_err(str::to_string)?;
        let next = |read: Option<&financial::Read>| {
            read.and_then(|v| v.value.as_ref())
                .and_then(Value::as_array)
                .and_then(|v| v.get(1))
                .is_some_and(|v| !v.is_nil())
        };
        let grids = vec![
            typed_grid(
                registry,
                "balances",
                "FinancialAccountBalance",
                &rows_member(registry, "FinancialBalanceSnapshot", balance, "Accounts")?,
            )?,
            grid(
                "transactions",
                [
                    "TransactionId",
                    "AccountingDate",
                    "Type",
                    "Amount",
                    "Currency",
                    "Description",
                    "JournalId",
                    "OperationId",
                ]
                .map(str::to_string)
                .to_vec(),
                transactions,
                None,
            ),
            grid(
                "reservations",
                [
                    "ReservationId",
                    "OrderId",
                    "Status",
                    "Units",
                    "Filled",
                    "Closed",
                    "Cancelled",
                    "Remaining",
                    "Version",
                ]
                .map(str::to_string)
                .to_vec(),
                reservations,
                None,
            ),
            typed_grid(
                registry,
                "journal",
                "FinancialJournalEntry",
                &rows_member(
                    registry,
                    "FinancialJournal",
                    state.journal.as_ref().and_then(|v| v.value.as_ref()),
                    "Entries",
                )?,
            )?,
            grid(
                "pending-postings",
                ["OperationId", "Type", "Amount", "Phase", "Message"]
                    .map(str::to_string)
                    .to_vec(),
                pending,
                None,
            ),
        ];
        let dialog = json!({"key":"financials","title":format!("{} — Financials",text(&fund["Name"])),"width":1150,"height":700,"captionWidth":170,"fields":[input("cash","Available Cash:",if balance.is_some(){format!("{} USD",amount(&row["AvailableCash"]) ?)}else{"unavailable".into()},"label",false,None),input("pendingWithdrawals","Pending Withdrawals:",if balance.is_some(){format!("{} USD",amount(&row["PendingWithdrawals"]) ?)}else{"unavailable".into()},"label",false,None),input("financialState","",if balance.is_some(){text(&row["OperatingState"])}else{"Unavailable".into()},"label",false,None)],"buttons":["Next transactions","Next reservations","Transaction","Ledger","Close"],"status":state.message,"grids":grids,"buttonEnabled":{"Next transactions":next(state.transactions.as_ref()),"Next reservations":next(state.reservations.as_ref()),"Transaction":balance.is_some(),"Ledger":true}});
        if let Some(index) = self.dialogs.iter().position(|d| d["key"] == "financials") {
            self.dialogs[index] = dialog;
        } else {
            self.dialogs.push(dialog);
        }
        Ok(())
    }
    pub(super) async fn financial_page_action(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        if self
            .dialogs
            .last()
            .is_some_and(|d| d["key"] == "risk-history")
        {
            self.risk_date = Some(crate::backend::date_input::parse(field(intent, "date"))?);
            return self
                .open_risk_history(
                    backend,
                    registry,
                    field(intent, "action") == "Next page",
                    deadline,
                )
                .await;
        }
        if self.dialogs.last().is_some_and(|d| {
            matches!(
                d["key"].as_str(),
                Some("ledger" | "setup" | "authority" | "account-version" | "rule-version")
            )
        }) {
            return self
                .ledger_action(backend, registry, intent, deadline)
                .await;
        }
        if self.dialogs.last().is_some_and(|d| d["key"] == "posting")
            && matches!(field(intent, "action"), "Post" | "Check outcome")
        {
            return self
                .post_or_reconcile(backend, registry, intent, deadline)
                .await;
        }
        match field(intent, "action") {
            "Ledger" => {
                self.pending_configuration = None;
                self.open_ledger(backend, registry, deadline).await
            }
            "Transaction" => {
                self.pending_posting = None;
                self.open_posting(backend, registry, None, None, deadline)
                    .await
            }
            "Refresh" => self.open_financials(backend, registry, deadline).await,
            "Next transactions" | "Next reservations" => {
                let context = BusinessContext::load(backend).await?;
                self.ledger_data
                    .as_mut()
                    .ok_or("Load financial data first")?
                    .next(
                        backend,
                        registry,
                        &context,
                        field(intent, "action") == "Next transactions",
                        deadline,
                    )
                    .await?;
                self.data.context = Some(context);
                self.update_financial_dialog(registry)?;
                Ok("Financial page loaded".into())
            }
            "Close" => {
                self.dialogs.pop();
                if self
                    .dialogs
                    .last()
                    .is_some_and(|d| d["key"] == "financials")
                {
                    self.refresh_financial_journals(registry).await?;
                }
                Ok("Financial dialog closed".into())
            }
            _ => Err("Financial editor action is not yet wired".into()),
        }
    }
}
