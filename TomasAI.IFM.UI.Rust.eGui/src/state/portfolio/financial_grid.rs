//! Selecting historical rows changes presentation only. Retained operations
//! are reloaded with integrity checks and keep their original backend scope.
use super::editors::input;
use super::*;
use crate::services::{business_context::BusinessContext, financial};
fn fk(name: &str) -> String {
    format!("{}.{name}", financial::ROOT)
}
impl PortfolioController {
    pub(super) async fn financial_grid_action(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let id = intent["originalId"]
            .as_str()
            .ok_or("Select a financial row")?;
        let operation = uuid::Uuid::parse_str(id).map_err(|_| "Invalid financial row identity")?;
        let parent = registry.project(
            &kind("PortfolioReadModel"),
            self.data.portfolio.as_ref().ok_or("Select a Portfolio")?,
        )?;
        let portfolio = parent["PortfolioId"]
            .as_i64()
            .ok_or("Missing Portfolio identity")?;
        let fund = registry.project(
            &kind("FundMandateReadModel"),
            self.data.fund.as_ref().ok_or("Select a Fund")?,
        )?["FundId"]
            .as_i64()
            .ok_or("Missing Fund identity")?;
        match field(intent, "grid") {
            "transactions" => {
                let state = self
                    .ledger_data
                    .as_ref()
                    .ok_or("Load financial data first")?;
                let mut selected = None;
                for row in state.transaction_rows()? {
                    let projected = registry.project(&fk("FinancialTransactionRow"), &row)?;
                    if projected["OperationId"] == operation.to_string() {
                        selected = Some((row, projected));
                        break;
                    }
                }
                let (row, projected) = selected.ok_or("Select a current transaction row")?;
                if let Some(journal) = projected["JournalId"].as_i64().filter(|v| *v > 0) {
                    let context = BusinessContext::load(backend).await?;
                    self.ledger_data
                        .as_mut()
                        .ok_or("Load financial data first")?
                        .load_journal(backend, registry, &context, journal, deadline)
                        .await?;
                    self.data.context = Some(context);
                }
                self.related_transaction = Some(row);
                self.related_receipt = None;
                self.update_financial_dialog(registry)?;
                Ok("Transaction selected".into())
            }
            "pending-postings" => {
                let pending = self.operation_store(false)?.load(operation).await?;
                let request = &pending["Request"];
                if request["PortfolioId"] != portfolio || request["Body"]["FundId"] != fund {
                    return Err("Retained posting belongs to another Portfolio or Fund".into());
                }
                let body = &request["Body"];
                let number = body["TransactionKind"]
                    .as_i64()
                    .ok_or("Invalid retained transaction kind")?;
                let name = registry
                    .enum_names(&fk("LedgerTransactionKind"))?
                    .into_iter()
                    .find(|name| {
                        registry.enum_value(&fk("LedgerTransactionKind"), name) == Ok(number)
                    })
                    .ok_or("Invalid retained transaction kind")?;
                let original = body["RelatedJournalId"]
                    .as_i64()
                    .map(|v| v.to_string())
                    .or_else(|| body["RelatedObligationId"].as_str().map(str::to_string))
                    .unwrap_or("None selected".into());
                let fields = vec![
                    input(
                        "rule",
                        "Transaction",
                        format!("{name} (version {})", text(&body["PostingRule"]["Version"])),
                        "text",
                        false,
                        None,
                    ),
                    input(
                        "amount",
                        "Amount (USD)",
                        text(&body["Amount"]),
                        "number",
                        false,
                        None,
                    ),
                    input(
                        "accountingDate",
                        "Accounting date",
                        text(&body["AccountingDate"]),
                        "date",
                        false,
                        None,
                    ),
                    input(
                        "valueDate",
                        "Value date",
                        text(&body["ValueDate"]),
                        "date",
                        false,
                        None,
                    ),
                    input(
                        "description",
                        "Description",
                        text(&body["Description"]),
                        "multiline",
                        false,
                        None,
                    ),
                    input(
                        "reference",
                        "Movement reference",
                        text(&body["MovementEvidence"]["SourceReference"]),
                        "text",
                        false,
                        None,
                    ),
                    input(
                        "confirmed",
                        "I confirm this cash movement",
                        if body["MovementEvidence"]["Status"]
                            == registry.enum_value(&fk("MovementStatus"), "Confirmed")?
                        {
                            "True"
                        } else {
                            "False"
                        },
                        "check",
                        false,
                        None,
                    ),
                    input(
                        "original",
                        "Original transaction",
                        original,
                        "text",
                        false,
                        None,
                    ),
                ];
                self.dialogs.push(json!({"key":"posting","title":"Fund Transaction","width":660,"height":360,"captionWidth":280,"fields":fields,"buttons":["Check outcome","Close"],"status":pending["Message"],"grids":null,"buttonEnabled":null}));
                self.pending_posting = Some(pending);
                self.pending_posting_dialog(registry)?;
                Ok("Original financial operation restored".into())
            }
            "pending-controls" => {
                let pending = self.operation_store(true)?.load(operation).await?;
                if pending["Request"]["PortfolioId"] != portfolio {
                    return Err("Retained control belongs to another Portfolio".into());
                }
                self.pending_configuration = Some(pending);
                self.submit_configuration(backend, registry, deadline)
                    .await?;
                self.open_ledger(backend, registry, deadline).await
            }
            _ => Err("Unsupported financial grid".into()),
        }
    }
}
