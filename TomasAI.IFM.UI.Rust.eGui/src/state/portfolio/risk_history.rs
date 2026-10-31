//! Read-only risk history belongs to the selected Portfolio/Fund/date. A stale
//! row can never redirect an invocation lookup outside that selected scope.
use super::editors::input;
use super::*;
use crate::services::{
    business_context::BusinessContext, inspection_queries as queries, strategy_display,
};
fn rk(name: &str) -> String {
    format!("{}.RiskManagement.{name}", queries::ROOT)
}
impl PortfolioController {
    pub(super) async fn show_inline_fund_view(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        view: &str,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        if !matches!(view, "financials" | "risk-history") {
            return Err("Select Financials or Risk History".into());
        }
        if self
            .dialogs
            .iter()
            .any(|d| !matches!(d.key.as_str(), "financials" | "risk-history"))
        {
            return Err("Close the Fund editor before changing its view".into());
        }
        self.dialogs
            .retain(|d| !matches!(d.key.as_str(), "financials" | "risk-history"));
        self.inline_fund_view = Some(view.into());
        if self.data.fund.is_none() {
            return Ok("No Fund selected".into());
        }
        if view == "financials" {
            self.open_financials(backend, registry, deadline).await
        } else {
            self.open_risk_history(backend, registry, false, deadline)
                .await
        }
    }
    pub(super) fn choose_risk_date(&mut self, intent: &Json) -> Result<String, String> {
        let date = crate::backend::date_input::parse(field(intent, "date"))?;
        self.risk_date = Some(date);
        self.risk_cursor = None;
        self.risk_rows.clear();
        let dialog = self.dialogs.last_mut().ok_or("Open Risk History")?;
        for input in dialog.fields.iter_mut() {
            if input.key == "date" {
                input.value = date.to_string().into();
            }
            if matches!(input.key.as_str(), "decision" | "evidence") {
                input.value = "".into();
            }
        }
        dialog.grids.as_mut().ok_or("Missing risk history grid")?[0]
            .rows
            .clear();
        dialog.button_enabled = Some(BTreeMap::from([
            ("Refresh".into(), true),
            ("Next page".into(), false),
        ]));
        Ok("Risk history date changed; refresh to load".into())
    }
    pub(super) async fn risk_grid_action(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let id = intent["originalId"]
            .as_str()
            .ok_or("Select a risk history row")?;
        let row = self
            .risk_rows
            .iter()
            .map(|row| registry.project(&rk("RiskHistoryRow"), row))
            .collect::<Result<Vec<_>, _>>()?
            .into_iter()
            .find(|row| row["InvocationId"] == id)
            .ok_or("Select a current risk history row")?;
        let workflow =
            uuid::Uuid::parse_str(row["WorkflowId"].as_str().ok_or("Missing risk workflow")?)
                .map_err(|_| "Invalid risk workflow")?;
        let wire = queries::query(
            backend,
            registry,
            "RiskManagement",
            "GetRiskInvocationQuery",
            &workflow.simple().to_string(),
            BTreeMap::from([
                (
                    "WorkflowId".into(),
                    Value::Array(vec![workflow.to_string().into()]),
                ),
                ("InvocationId".into(), id.into()),
            ]),
            deadline,
        )
        .await?;
        let projected = registry.project(&rk("RiskObservation"), &wire)?;
        if projected["Snapshot"]["WorkflowDefinition"]["WorkflowId"]["Value"]
            != workflow.to_string()
        {
            return Err("Risk observation crossed workflow identity".into());
        }
        let decision = crate::services::inspection_display::risk(registry, &wire)?;
        let evidence =
            crate::services::inspection_json::render(registry, &rk("RiskObservation"), &wire)?;
        let dialog = self
            .dialogs
            .iter_mut()
            .find(|d| d.key == "risk-history")
            .ok_or("Open Risk History")?;
        for input in dialog.fields.iter_mut() {
            if input.key == "decision" {
                input.value = decision.clone().into();
            }
            if input.key == "evidence" {
                input.value = evidence.clone().into();
            }
        }
        Ok("Risk evidence loaded".into())
    }
    pub(super) async fn open_risk_history(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        next: bool,
        deadline: tokio::time::Instant,
    ) -> Result<String, String> {
        let context = BusinessContext::load(backend).await?;
        let date = self.risk_date.unwrap_or(context.value_date);
        let fund = registry.project(
            &kind("FundMandateReadModel"),
            self.data.fund.as_ref().ok_or("Select a Fund")?,
        )?;
        let portfolio = fund["PortfolioId"]
            .as_i64()
            .ok_or("Missing Portfolio identity")?;
        let fund = fund["FundId"].as_i64().ok_or("Missing Fund identity")?;
        let cursor = if next {
            self.risk_cursor
                .as_ref()
                .ok_or("No next risk page")?
                .clone()
                .into()
        } else {
            Value::Nil
        };
        let wire = queries::query(
            backend,
            registry,
            "RiskManagement",
            "GetRiskHistoryPageQuery",
            &format!("{portfolio}.{fund}"),
            BTreeMap::from([
                ("PortfolioId".into(), portfolio.into()),
                ("FundId".into(), fund.into()),
                ("ValueDate".into(), date_only_wire(date)),
                ("PageSize".into(), 25.into()),
                ("PagingState".into(), cursor),
            ]),
            deadline,
        )
        .await?;
        let projected = registry.project(&rk("RiskHistoryPage"), &wire)?;
        let rows =
            crate::services::strategy::member(registry, &rk("RiskHistoryPage"), &wire, "Items")?
                .as_array()
                .filter(|v| v.len() <= 25)
                .cloned()
                .ok_or("Invalid risk history page")?;
        let mut display_rows = vec![];
        for row in &rows {
            let value = registry.project(&rk("RiskHistoryRow"), row)?;
            if value["PortfolioId"] != portfolio
                || value["FundId"] != fund
                || value["ValueDate"] != date.to_string()
            {
                return Err("Risk history crossed the selected Portfolio, Fund or date".into());
            }
            display_rows.push(cell_row(
                text(&value["InvocationId"]),
                strategy_display::fields(registry, &rk("RiskHistoryRow"), row)?
                    .into_iter()
                    .collect(),
            ));
        }
        let cursor = projected["PagingState"]
            .as_str()
            .filter(|v| !v.is_empty())
            .map(str::to_string);
        if cursor.as_ref().is_some_and(|value| value.len() > 2048) {
            return Err("Risk history cursor exceeds its bound".into());
        }
        if next && cursor.is_some() && cursor == self.risk_cursor {
            return Err("Risk history cursor did not advance".into());
        }
        let dialog = Dialog {
            key: "risk-history".into(),
            title: (format!("Risk history - Portfolio {portfolio}, Fund {fund}")).to_string(),
            width: (1300) as _,
            height: (850) as _,
            caption_width: (160) as _,
            fields: vec![
                input(
                    "date",
                    "Evaluation date (UTC)",
                    date.to_string(),
                    "date",
                    true,
                    None,
                ),
                input(
                    "decision",
                    "Decision and sizing",
                    "",
                    "multiline",
                    false,
                    None,
                ),
                input("evidence", "Evidence", "", "multiline", false, None),
            ],
            buttons: vec!["Refresh".into(), "Next page".into()],
            status: (if rows.is_empty() {
                "No projected Risk history for this UTC date."
            } else {
                "Double-click an invocation to refresh its details and current authority."
            })
            .to_string(),
            grids: Some(vec![grid(
                "risk-history",
                [
                    "EvaluatedAtUtc",
                    "Horizon",
                    "Variant",
                    "Outcome",
                    "Units",
                    "Reason",
                    "OrderId",
                    "WorkflowId",
                    "InvocationId",
                ]
                .map(str::to_string)
                .to_vec(),
                display_rows,
                None,
            )]),
            button_enabled: Some(BTreeMap::from([
                ("Refresh".into(), true),
                ("Next page".into(), cursor.is_some()),
            ])),
            ..Default::default()
        };
        if let Some(index) = self.dialogs.iter().position(|d| d.key == "risk-history") {
            self.dialogs[index] = dialog;
        } else {
            self.dialogs.push(dialog);
        }
        self.risk_rows = rows;
        self.risk_cursor = cursor;
        self.risk_date = Some(date);
        self.data.context = Some(context);
        Ok("Risk history loaded".into())
    }
}
