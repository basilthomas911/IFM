//! Read-only inspection queries. One caller deadline covers the complete lookup;
//! authority and financial decisions remain backend results, never UI estimates.
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use rmpv::Value;
use std::collections::BTreeMap;
pub const ROOT: &str = "TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline";

pub async fn query(
    backend: &Backend,
    registry: &Registry,
    section: &str,
    name: &str,
    key: &str,
    mut fields: BTreeMap<String, Value>,
    deadline: tokio::time::Instant,
) -> Result<Value, String> {
    // This is a closed read surface, not a general actor routing escape hatch.
    if !matches!(
        (section, name),
        (
            "RiskManagement",
            "GetRiskHistoryPageQuery" | "GetRiskInvocationQuery"
        ) | (
            "MarketCondition.Assessment",
            "GetMarketConditionAssessmentQuery" | "GetMarketConditionAssessmentHistoryQuery"
        )
    ) {
        return Err("Unsupported inspection query".into());
    }
    let kind = format!("{ROOT}.{section}.{name}");
    let (actor, verb) = registry.route(&kind)?;
    let subject = ActorSubject::query(actor, verb, key);
    fields.insert("Subject".into(), subject.wire());
    let entity_key = if section == "RiskManagement" {
        "none"
    } else {
        key
    };
    let entity_kind = "TomasAI.IFM.Shared.EventModelActor.ActorEntityId";
    let union = registry.describe("TomasAI.IFM.Shared.EventModelActor.Contracts.IActorEntityId")?;
    let tag = union
        .unions
        .iter()
        .find(|(_, kind)| kind == entity_kind)
        .ok_or("Missing compiled actor entity contract")?
        .0;
    fields.insert(
        "EntityId".into(),
        Value::Array(vec![tag.into(), Value::Array(vec![entity_key.into()])]),
    );
    if section == "RiskManagement" {
        // Match the existing interactive reader scope. Roles are requests only;
        // the backend checks the authenticated principal and actual authority.
        let principal = format!(
            "interactive:{}",
            std::env::var("USERNAME").unwrap_or_default()
        );
        fields.insert(
            "Access".into(),
            Value::Array(vec![
                principal.into(),
                Value::Array(vec!["PortfolioReader".into()]),
            ]),
        );
    }
    let wire = registry.encode(&kind, &fields)?;
    let reply = backend
        .request_until(&subject, &wire, deadline, None)
        .await?;
    Ok(service_result(&reply)?.clone())
}

pub async fn workflow(
    backend: &Backend,
    registry: &Registry,
    id: uuid::Uuid,
    deadline: tokio::time::Instant,
) -> Result<Value, String> {
    if id.is_nil() {
        return Err("Enter a valid workflow identity".into());
    }
    let kind =
        crate::services::strategy::kind("Queries", "GetIntrinsicTimeStrategyWorkflowByIdQuery");
    let (actor, verb) = registry.route(&kind)?;
    let subject = ActorSubject::query(actor, verb, &id.simple().to_string());
    let wire = registry.encode(
        &kind,
        &BTreeMap::from([
            ("Subject".into(), subject.wire()),
            (
                "EntityId".into(),
                Value::Array(vec![
                    0.into(),
                    Value::Array(vec![id.simple().to_string().into()]),
                ]),
            ),
            (
                "WorkflowId".into(),
                Value::Array(vec![id.to_string().into()]),
            ),
            ("MinimumWorkflowRevision".into(), 0.into()),
        ]),
    )?;
    let reply = backend
        .request_until(&subject, &wire, deadline, None)
        .await?;
    let value = service_result(&reply)?;
    if value.is_nil() {
        return Err("Workflow not found".into());
    }
    let model =
        crate::services::strategy::kind("ViewModels", "IntrinsicTimeStrategyWorkflowReadModel");
    let row = registry.project(&model, value)?;
    if row["WorkflowId"]["Value"].as_str() != Some(&id.to_string()) {
        return Err("Workflow query identity mismatch".into());
    }
    let payload = crate::services::strategy::member(registry, &model, value, "StatePayload")?;
    let bytes = match &payload {
        Value::Binary(bytes) if bytes.len() <= 16 * 1024 * 1024 => bytes,
        _ => return Err("Invalid workflow state payload".into()),
    };
    let view = crate::backend::codec::decode(bytes)?;
    let projected = registry.project(crate::services::strategy_display::VIEW, &view)?;
    if projected["WorkflowId"] != row["WorkflowId"]
        || projected["WorkflowRevision"] != row["WorkflowRevision"]
    {
        return Err("Workflow state identity or revision mismatch".into());
    }
    Ok(view)
}
