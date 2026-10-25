//! Single-owner read-only inspection state. Failed queries retain the last
//! successful page, while request receipts distinguish failure from an empty page.
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    services::{inspection_queries as api, strategy_display},
    strategy_inspection::InspectionPublication,
};
use rmpv::Value;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
#[derive(Default)]
pub struct InspectionController {
    generation: u64,
    result: Json,
    receipts: BTreeMap<String, (Json, Json)>,
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
fn stamp(value: &Json) -> Result<String, String> {
    chrono::DateTime::parse_from_rfc3339(value.as_str().ok_or("Missing inspection timestamp")?)
        .map(|v| v.format("%Y-%m-%d %H:%M:%S").to_string())
        .map_err(|_| "Invalid inspection timestamp".into())
}
fn t(value: &Json) -> String {
    value.as_str().map(str::to_string).unwrap_or_else(|| {
        if value.is_null() {
            String::new()
        } else {
            value.to_string()
        }
    })
}
impl InspectionController {
    async fn read(
        &self,
        backend: &Backend,
        registry: &Registry,
        input: &Json,
        deadline: tokio::time::Instant,
    ) -> Result<Json, String> {
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
                    let projected = registry.project(&event_kind, event)?;
                    let envelope = super::super::services::strategy::member(
                        registry,
                        &event_kind,
                        event,
                        "Result",
                    )?;
                    let result = assessment(registry, &envelope)?;
                    let id = t(&projected["WorkflowId"]["Value"]);
                    uuid::Uuid::parse_str(&id)
                        .map_err(|_| "Invalid assessment workflow identity")?;
                    if result["MarketProfileId"] != profile
                        || result["InstrumentRoot"] != "ES"
                        || result["TargetHorizon"] != horizon
                        || result["WorkflowId"] != projected["WorkflowId"]
                    {
                        return Err("Assessment history crossed requested scope".into());
                    }
                    rows.push(json!({"id":id.replace('-',""),"label":format!("{} {} {} {}",stamp(&result["EvaluatedAtUtc"])?,t(&result["TargetHorizon"]),t(&result["Assessment"]["Availability"]),t(&result["Assessment"]["ConditionType"])),"fields":{"workflow":id}}));
                }
                return Ok(
                    json!({"view":view,"rows":rows,"detail":null,"decision":"","evidence":"","pagingState":null,"context":null}),
                );
            }
            let id = identity(input, "workflow")?;
            let wire = api::workflow(backend, registry, id, deadline).await?;
            let row = registry.project(strategy_display::VIEW, &wire)?;
            if !row["AssessmentBinding"].is_null() {
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
            for (key, value) in [
                ("schema", json!(1)),
                ("epoch", json!(uuid::Uuid::new_v4().to_string())),
                ("generation", json!(self.generation + 1)),
                ("mode", json!("Live")),
            ] {
                detail[key] = value;
            }
            let candidate = &row["OrderComposition"]["Result"]["CompositionResult"]["Candidate"];
            let context = if row["CurrentStage"] == "RiskManagement" && !candidate.is_null() {
                json!({"portfolio":t(&candidate["PortfolioId"]),"fund":t(&candidate["FundId"]),"workflow":id.to_string(),"invocation":row["RiskExecution"]["CommandId"].as_str().map(str::to_string).unwrap_or_else(||t(&row["CausationId"]))})
            } else {
                Json::Null
            };
            return Ok(
                json!({"view":view,"rows":[],"detail":detail,"decision":"","evidence":"","pagingState":null,"context":context}),
            );
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
            let page = registry.project(
                &format!("{}.RiskManagement.RiskHistoryPage", api::ROOT),
                &wire,
            )?;
            let mut rows = vec![];
            for row in page["Items"]
                .as_array()
                .filter(|v| v.len() <= 25)
                .ok_or("Invalid risk history page")?
            {
                if row["PortfolioId"] != portfolio
                    || row["FundId"] != fund
                    || row["ValueDate"] != date.to_string()
                {
                    return Err(
                        "Risk history crossed requested portfolio, fund or value date".into(),
                    );
                }
                rows.push(json!({"id":row["InvocationId"],"label":format!("{} {} {} {} {} {}",stamp(&row["EvaluatedAtUtc"])?,t(&row["Horizon"]),t(&row["Variant"]),t(&row["Outcome"]),t(&row["Units"]),t(&row["Reason"])),"fields":{"workflow":row["WorkflowId"],"invocation":row["InvocationId"]}}));
            }
            if action == "next" && page["PagingState"].as_str() == Some(field(input, "cursor")) {
                return Err("Risk history cursor did not advance".into());
            }
            return Ok(
                json!({"view":view,"rows":rows,"detail":null,"decision":"","evidence":"","pagingState":page["PagingState"],"context":{"date":date.to_string()}}),
            );
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
        let row = registry.project(&kind, &wire)?;
        if row["Snapshot"]["WorkflowDefinition"]["WorkflowId"]["Value"] != workflow.to_string() {
            return Err("Risk observation crossed workflow identity".into());
        }
        let decision = crate::services::inspection_display::risk(registry, &wire)?;
        let evidence = strategy_display::evidence(registry, &kind, &wire)?;
        Ok(
            json!({"view":view,"rows":[],"detail":null,"decision":decision,"evidence":evidence,"pagingState":null,"context":null}),
        )
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
                    if input["intent"] == "open" && loaded["view"] == self.result["view"] {
                        loaded["rows"] = self.result["rows"].clone();
                        loaded["pagingState"] = self.result["pagingState"].clone();
                    }
                    self.result = loaded;
                    json!({"requestId":key,"state":"Completed","message":"Loaded"})
                }
                Err(error) => json!({"requestId":key,"state":"Failed","message":error}),
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
        let result = if self.result.is_null() {
            json!({"view":input["view"],"rows":[],"detail":null,"decision":"","evidence":"","pagingState":null,"context":null})
        } else {
            self.result.clone()
        };
        serde_json::from_value(json!({"schema":1,"generation":self.generation,"status":outcome["message"],"busy":false,"result":result,"outcomes":[outcome]})).map_err(|e|e.to_string())
    }
}
fn identity_request(input: &Json) -> Result<uuid::Uuid, String> {
    uuid::Uuid::parse_str(input["requestId"].as_str().unwrap_or_default())
        .ok()
        .filter(|v| !v.is_nil())
        .ok_or("Invalid inspection request identity".into())
}
fn assessment(registry: &Registry, envelope: &Value) -> Result<Json, String> {
    let kind = "TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyStageResultEnvelope";
    let row = registry.project(kind, envelope)?;
    if row["ResultType"] != "MarketConditionAssessmentResult" || row["SchemaVersion"] != 1 {
        return Err("Invalid assessment result envelope".into());
    }
    let typed = crate::services::strategy::member(registry, kind, envelope, "AssessmentResult")?;
    let typed = if typed.is_nil() {
        let payload = crate::services::strategy::member(registry, kind, envelope, "Payload")?;
        crate::backend::codec::decode(payload.as_slice().ok_or("Missing assessment payload")?)?
    } else {
        typed
    };
    let result = registry.project(
        &format!(
            "{}.MarketCondition.Assessment.MarketConditionAssessmentResult",
            api::ROOT
        ),
        &typed,
    )?;
    if result["ResultId"] != row["ResultId"] || result["EvaluatedAtUtc"] != row["ProducedAtUtc"] {
        return Err("Assessment result identity or timestamp mismatch".into());
    }
    Ok(result)
}
