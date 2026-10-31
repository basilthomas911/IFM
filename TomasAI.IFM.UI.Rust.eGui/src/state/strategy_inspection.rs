//! Single-owner read-only inspection state. Failed queries retain the last
//! successful page, while request receipts distinguish failure from an empty page.
use crate::backend::contract_view::Field;
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    lookup_editor::EditorOutcome,
    services::{inspection_queries as api, strategy_display},
    strategy_inspection::{InspectionPublication, InspectionResult, InspectionRow},
};
use rmpv::Value;
use serde_json::Value as Json;
use std::collections::BTreeMap;
#[derive(Default)]
pub struct InspectionController {
    generation: u64,
    result: Option<InspectionResult>,
    receipts: BTreeMap<String, (Json, EditorOutcome)>,
}
fn field<'a>(input: &'a Json, key: &str) -> &'a str {
    input["fields"][key].as_str().unwrap_or_default().trim()
}
fn identity(input: &Json, key: &str) -> Result<uuid::Uuid, String> {
    uuid::Uuid::parse_str(field(input, key))
        .ok()
        .filter(|id| !id.is_nil())
        .ok_or(format!("Enter a valid {key}"))
}
fn number(input: &Json, key: &str) -> Result<i32, String> {
    field(input, key)
        .parse::<i32>()
        .ok()
        .filter(|v| *v > 0)
        .ok_or(format!("Enter a positive {key}"))
}
impl InspectionController {
    async fn read(
        &self,
        backend: &Backend,
        registry: &Registry,
        input: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<InspectionResult, String> {
        let view = input["view"].as_str().ok_or("Missing inspection view")?;
        let action = input["intent"]
            .as_str()
            .ok_or("Missing inspection action")?;
        if view == "observation" {
            if action == "history" {
                let horizon = field(input, "horizon");
                if !matches!(horizon, "Daily" | "Weekly" | "Monthly") {
                    return Err("Select Daily, Weekly or Monthly".into());
                }
                let profile = field(input, "profile");
                if profile.is_empty() {
                    return Err("Enter a market profile".into());
                }
                let wire = api::query(
                    backend,
                    registry,
                    "MarketCondition.Assessment",
                    "GetMarketConditionAssessmentHistoryQuery",
                    "assessment-history",
                    BTreeMap::from([
                        ("MarketProfileId".into(), profile.into()),
                        ("InstrumentRoot".into(), "ES".into()),
                        (
                            "TargetHorizon".into(),
                            registry
                                .enum_value(
                                    "TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType",
                                    horizon,
                                )?
                                .into(),
                        ),
                        (
                            "BeforeUtc".into(),
                            timestamp_wire(
                                chrono::DateTime::parse_from_rfc3339(
                                    "9999-12-31T23:59:59.9999999Z",
                                )
                                .unwrap()
                                .to_utc(),
                            ),
                        ),
                        ("PageSize".into(), 25.into()),
                    ]),
                    deadline,
                )
                .await?;
                let mut rows = vec![];
                for event in wire
                    .as_array()
                    .filter(|v| v.len() <= 25)
                    .ok_or("Invalid assessment history page")?
                {
                    let event_kind = format!(
                        "{}.Events.MarketConditionAssessmentCompletedEvent",
                        api::ROOT
                    );
                    let projected = Field::new(registry, &event_kind, event);
                    let envelope = super::super::services::strategy::member(
                        registry,
                        &event_kind,
                        event,
                        "Result",
                    )?;
                    let result = assessment(registry, &envelope)?;
                    let id = projected.path(&["WorkflowId", "Value"])?.text()?;
                    uuid::Uuid::parse_str(&id)
                        .map_err(|_| "Invalid assessment workflow identity")?;
                    if result.profile != profile
                        || result.instrument != "ES"
                        || result.horizon != horizon
                        || result.workflow != id
                    {
                        return Err("Assessment history crossed requested scope".into());
                    }
                    rows.push(InspectionRow {
                        id: id.replace('-', ""),
                        label: format!(
                            "{} {} {} {}",
                            result.evaluated.format("%Y-%m-%d %H:%M:%S"),
                            result.horizon,
                            result.availability,
                            result.condition
                        ),
                        fields: BTreeMap::from([("workflow".into(), id)]),
                    });
                }
                return Ok(InspectionResult {
                    view: view.into(),
                    rows,
                    ..Default::default()
                });
            }
            let id = identity(input, "workflow")?;
            let wire = api::workflow(backend, registry, id, deadline).await?;
            let row = Field::new(registry, strategy_display::VIEW, &wire);
            if !row.field("AssessmentBinding")?.is_nil() {
                // The existing screen reads the projection alongside the accepted complete
                // workflow. It never upgrades projection evidence into workflow authority.
                let _projection = api::query(
                    backend,
                    registry,
                    "MarketCondition.Assessment",
                    "GetMarketConditionAssessmentQuery",
                    &id.simple().to_string(),
                    BTreeMap::from([(
                        "WorkflowId".into(),
                        Value::Array(vec![id.to_string().into()]),
                    )]),
                    deadline,
                )
                .await?;
            }
            let mut detail = strategy_display::details(registry, &wire)?;
            detail.schema = 1;
            detail.epoch = uuid::Uuid::new_v4().to_string();
            detail.generation = self.generation + 1;
            detail.mode = "Live".into();
            let candidate = row.path(&[
                "OrderComposition",
                "Result",
                "CompositionResult",
                "Candidate",
            ])?;
            let context =
                if row.field("CurrentStage")?.text()? == "RiskManagement" && !candidate.is_nil() {
                    Some(BTreeMap::from([
                        ("portfolio".into(), candidate.field("PortfolioId")?.text()?),
                        ("fund".into(), candidate.field("FundId")?.text()?),
                        ("workflow".into(), id.to_string()),
                        (
                            "invocation".into(),
                            row.path(&["RiskExecution", "CommandId"])?
                                .string()
                                .map(str::to_owned)
                                .unwrap_or(row.field("CausationId")?.text()?),
                        ),
                    ]))
                } else {
                    None
                };
            return Ok(InspectionResult {
                view: view.into(),
                detail: Some(detail),
                context,
                ..Default::default()
            });
        }
        if matches!(action, "history" | "next") {
            let date = if field(input, "date").is_empty() {
                crate::services::business_context::BusinessContext::load(backend)
                    .await?
                    .value_date
            } else {
                chrono::NaiveDate::parse_from_str(field(input, "date"), "%Y-%m-%d")
                    .map_err(|_| "Enter date as yyyy-MM-dd (UTC)")?
            };
            let portfolio = number(input, "portfolio")?;
            let fund = number(input, "fund")?;
            let cursor = if action == "next" {
                let value = field(input, "cursor");
                if value.is_empty() {
                    return Err("No next risk page".into());
                }
                value.into()
            } else {
                Value::Nil
            };
            let wire = api::query(
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
            let page_kind = format!("{}.RiskManagement.RiskHistoryPage", api::ROOT);
            let page = Field::new(registry, &page_kind, &wire);
            let mut rows = vec![];
            for row in page.field("Items")?.items(25)? {
                if row.field("PortfolioId")?.integer() != Some(portfolio as i64)
                    || row.field("FundId")?.integer() != Some(fund as i64)
                    || row.field("ValueDate")?.text()? != date.to_string()
                {
                    return Err(
                        "Risk history crossed requested portfolio, fund or value date".into(),
                    );
                }
                rows.push(InspectionRow {
                    id: row.field("InvocationId")?.text()?,
                    label: format!(
                        "{} {} {} {} {} {}",
                        row.field("EvaluatedAtUtc")?
                            .utc()?
                            .ok_or("Missing inspection timestamp")?
                            .format("%Y-%m-%d %H:%M:%S"),
                        row.field("Horizon")?.text()?,
                        row.field("Variant")?.text()?,
                        row.field("Outcome")?.text()?,
                        row.field("Units")?.text()?,
                        row.field("Reason")?.text()?
                    ),
                    fields: BTreeMap::from([
                        ("workflow".into(), row.field("WorkflowId")?.text()?),
                        ("invocation".into(), row.field("InvocationId")?.text()?),
                    ]),
                });
            }
            if action == "next"
                && page.field("PagingState")?.string() == Some(field(input, "cursor"))
            {
                return Err("Risk history cursor did not advance".into());
            }
            return Ok(InspectionResult {
                view: view.into(),
                rows,
                paging_state: page.field("PagingState")?.string().map(str::to_owned),
                context: Some(BTreeMap::from([("date".into(), date.to_string())])),
                ..Default::default()
            });
        }
        let workflow = identity(input, "workflow")?;
        let invocation = identity(input, "invocation")?;
        let wire = api::query(
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
                ("InvocationId".into(), invocation.to_string().into()),
            ]),
            deadline,
        )
        .await?;
        let kind = format!("{}.RiskManagement.RiskObservation", api::ROOT);
        let row = Field::new(registry, &kind, &wire);
        if row
            .path(&["Snapshot", "WorkflowDefinition", "WorkflowId", "Value"])?
            .string()
            != Some(workflow.to_string().as_str())
        {
            return Err("Risk observation crossed workflow identity".into());
        }
        let decision = crate::services::inspection_display::risk(registry, &wire)?;
        let evidence = strategy_display::evidence(registry, &kind, &wire)?;
        Ok(InspectionResult {
            view: view.into(),
            decision,
            evidence,
            ..Default::default()
        })
    }
    pub async fn execute(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        input: &Json,
    ) -> Result<InspectionPublication, String> {
        if input["schema"] != 1
            || !matches!(input["view"].as_str(), Some("observation" | "risk"))
            || !matches!(input["intent"].as_str(), Some("history" | "open" | "next"))
        {
            return Err("Invalid inspection intent".into());
        }
        let request = identity_request(input)?;
        let key = request.to_string();
        let fields = input["fields"]
            .as_object()
            .filter(|v| v.len() <= 16)
            .ok_or("Invalid inspection fields")?;
        if fields
            .iter()
            .any(|(k, v)| k.len() > 64 || v.as_str().is_none_or(|s| s.len() > 2048))
        {
            return Err("Inspection field limit exceeded".into());
        }
        let outcome = if let Some((original, outcome)) = self.receipts.get(&key) {
            if original != input {
                return Err("RequestId belongs to another inspection query".into());
            }
            outcome.clone()
        } else {
            let deadline = tokio::time::Instant::now() + backend.default_timeout();
            let result =
                tokio::time::timeout_at(deadline, self.read(backend, registry, input, deadline))
                    .await
                    .map_err(|_| "Inspection query deadline expired".to_string())
                    .and_then(|v| v);
            let outcome = match result {
                Ok(mut loaded) => {
                    if input["intent"] == "open" {
                        if let Some(previous) = self
                            .result
                            .as_ref()
                            .filter(|previous| previous.view == loaded.view)
                        {
                            loaded.rows = previous.rows.clone();
                            loaded.paging_state = previous.paging_state.clone();
                        }
                    }
                    self.result = Some(loaded);
                    EditorOutcome {
                        request_id: key.clone(),
                        state: "Completed".into(),
                        message: "Loaded".into(),
                    }
                }
                Err(error) => EditorOutcome {
                    request_id: key.clone(),
                    state: "Failed".into(),
                    message: error,
                },
            };
            if self.receipts.len() >= 64 {
                if let Some(old) = self.receipts.keys().next().cloned() {
                    self.receipts.remove(&old);
                }
            }
            self.receipts.insert(key, (input.clone(), outcome.clone()));
            outcome
        };
        self.generation += 1;
        let result = self.result.clone().unwrap_or_else(|| InspectionResult {
            view: input["view"].as_str().unwrap_or_default().into(),
            ..Default::default()
        });
        Ok(InspectionPublication {
            schema: 1,
            generation: self.generation,
            status: outcome.message.clone(),
            busy: false,
            result,
            outcomes: vec![outcome],
        })
    }
}
fn identity_request(input: &Json) -> Result<uuid::Uuid, String> {
    uuid::Uuid::parse_str(input["requestId"].as_str().unwrap_or_default())
        .ok()
        .filter(|v| !v.is_nil())
        .ok_or("Invalid inspection request identity".into())
}
struct AssessmentHeader {
    profile: String,
    instrument: String,
    horizon: String,
    workflow: String,
    evaluated: chrono::DateTime<chrono::Utc>,
    availability: String,
    condition: String,
}
fn assessment(registry: &Registry, envelope: &Value) -> Result<AssessmentHeader, String> {
    let kind = "TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyStageResultEnvelope";
    let envelope = Field::new(registry, kind, envelope);
    if envelope.field("ResultType")?.text()? != "MarketConditionAssessmentResult"
        || envelope.field("SchemaVersion")?.integer() != Some(1)
    {
        return Err("Invalid assessment result envelope".into());
    }
    let typed = envelope.field("AssessmentResult")?;
    let payload;
    let result_kind = format!(
        "{}.MarketCondition.Assessment.MarketConditionAssessmentResult",
        api::ROOT
    );
    let result = if typed.is_nil() {
        payload = crate::backend::codec::decode(
            envelope
                .field("Payload")?
                .value
                .as_slice()
                .ok_or("Missing assessment payload")?,
        )?;
        Field::new(registry, &result_kind, &payload)
    } else {
        typed
    };
    if result.field("ResultId")?.string() != envelope.field("ResultId")?.string()
        || result.field("EvaluatedAtUtc")?.utc()? != envelope.field("ProducedAtUtc")?.utc()?
    {
        return Err("Assessment result identity or timestamp mismatch".into());
    }
    Ok(AssessmentHeader {
        profile: result.field("MarketProfileId")?.text()?,
        instrument: result.field("InstrumentRoot")?.text()?,
        horizon: result.field("TargetHorizon")?.text()?,
        workflow: result.path(&["WorkflowId", "Value"])?.text()?,
        evaluated: result
            .field("EvaluatedAtUtc")?
            .utc()?
            .ok_or("Missing inspection timestamp")?,
        availability: result.path(&["Assessment", "Availability"])?.text()?,
        condition: result.path(&["Assessment", "ConditionType"])?.text()?,
    })
}
