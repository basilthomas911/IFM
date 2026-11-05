//! Portfolio API boundary. Identity allocation and commands are never retried;
//! every request uses the API clock and the caller's absolute deadline.
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use crate::services::business_context::BusinessContext;
use rmpv::Value;
use std::collections::BTreeMap;
pub const ROOT: &str = "TomasAI.IFM.Domain.Portfolio.Shared";


/// Provides principal for the portfolio implementation.
pub fn principal() -> String {
    format!(
        "interactive:{}",
        std::env::var("USERNAME").unwrap_or_default()
    )
}


/// Provides access for the portfolio implementation.
pub fn access(administrator: bool) -> Value {
    Value::Array(vec![
        principal().into(),
        Value::Array(vec![
            if administrator {
                "PortfolioAdministrator"
            } else {
                "PortfolioReader"
            }
            .into(),
        ]),
    ])
}


/// Checks whether the requested query is permitted by the declared route policy.
fn allowed_query(name: &str) -> bool {
    matches!(
        name,
        "GetPortfoliosQuery"
            | "GetPortfolioQuery"
            | "GetPortfolioRevisionQuery"
            | "GetFundsQuery"
            | "GetFundQuery"
            | "GetFundRevisionQuery"
            | "GetFundAllocationQuery"
            | "GetFundRiskEnvelopeQuery"
            | "GetFundTemplateAssignmentsQuery"
            | "GetPortfolioFinancialPoliciesQuery"
            | "GetPortfolioFinancialPolicyQuery"
            | "GetActivePortfolioFinancialPolicyQuery"
            | "GetFundOrdersPageQuery"
            | "GetFundOrderTradesPageQuery"
            | "GetFundOrderByOrderIdQuery"
            | "GetFundOrderTradeByTradeIdQuery"
            | "GetFundCompositionByWorkflowQuery"
            | "GetPortfolioFundStrategyReferenceCombinationsQuery"
    )
}


/// Provides query for the portfolio implementation.
pub async fn query(
    backend: &Backend,
    registry: &Registry,
    context: &BusinessContext,
    name: &str,
    key: &str,
    fields: BTreeMap<String, Value>,
    deadline: tokio::time::Instant,
) -> Result<Value, String> {
    let response = query_reply(backend, registry, context, name, key, fields, deadline).await?;
    Ok(service_result(&response)?.clone())
}


/// Allocates one Portfolio identity through the Function actor and its durable receipt.
pub async fn allocate_identity(
    backend: &Backend,
    registry: &Registry,
    identity: &str,
    deadline: tokio::time::Instant,
) -> Result<Value, String> {
    let kind = registry.enum_value(
        &format!("{ROOT}.ServiceApi.PortfolioBusinessIdentityKind"),
        identity,
    )?;
    let completed = request_preparation(
        backend,
        registry,
        "AllocatePortfolioBusinessIdCommand",
        "PortfolioBusinessIdAllocation",
        uuid::Uuid::new_v4(),
        BTreeMap::from([("Kind".into(), kind.into()), ("Access".into(), access(true))]),
        deadline,
    )
    .await?;
    Ok(registry
        .wire_field(&format!("{ROOT}.Preparation.PortfolioBusinessIdAllocationCompletedEvent"), "Value", &completed)?
        .clone())
}


/// Encodes the shared preparation envelope and validates the correlated Function result.
pub(super) async fn request_preparation(
    backend: &Backend,
    registry: &Registry,
    command: &str,
    outcome: &str,
    id: uuid::Uuid,
    mut fields: BTreeMap<String, Value>,
    deadline: tokio::time::Instant,
) -> Result<Value, String> {
    let kind = format!("{ROOT}.Preparation.{command}");
    let operation = id.to_string();
    let entity = id.simple().to_string();
    let (actor, verb) = registry.route(&kind)?;
    let subject = ActorSubject {
        kind: ActorKind::Function,
        name: actor.into(),
        verb: verb.into(),
        entity: entity.clone(),
    };
    let route_to = registry.enum_value(
        "TomasAI.IFM.Shared.EventSourcing.BoundedContextName",
        "PortfolioBoundedContext",
    )?;
    fields.extend([
        ("CommandId".into(), operation.as_str().into()),
        ("Subject".into(), subject.wire()),
        ("EntityId".into(), Value::Array(vec![entity.into()])),
        ("ErrorCode".into(), registry.error_id(&kind)?.into()),
        ("RouteTo".into(), route_to.into()),
    ]);
    let body = registry.encode(&kind, &fields)?;
    let reply = backend
        .request_until(&subject, &body, deadline, None)
        .await
        .map_err(|error| format!("OutcomeUnknown: {command} may have completed: {error}"))?;
    let [completed, failed] = service_result(&reply)?
        .as_array()
        .map(Vec::as_slice)
        .ok_or("Invalid Portfolio preparation result")?
    else {
        return Err("Invalid Portfolio preparation result".into());
    };
    if completed.is_nil() && !failed.is_nil() {
        let failed_kind = format!("{ROOT}.Preparation.{outcome}FailedEvent");
        let message = registry.wire_field(&failed_kind, "ErrorMessage", failed)?;
        return Err(message
            .as_str()
            .unwrap_or("Portfolio preparation failed")
            .into());
    }
    let completed_kind = format!("{ROOT}.Preparation.{outcome}CompletedEvent");
    if !failed.is_nil()
        || registry
            .wire_field(&completed_kind, "CommandId", completed)?
            .as_str()
            != Some(operation.as_str())
    {
        return Err("Portfolio preparation result does not match this command".into());
    }
    Ok(completed.clone())
}


/// An absent order is distinct from an unavailable or unauthorized read. This
/// distinction is required before a deletion can be considered committed.
pub async fn optional_order(
    backend: &Backend,
    registry: &Registry,
    context: &BusinessContext,
    order: i32,
    deadline: tokio::time::Instant,
) -> Result<Option<Value>, String> {
    if order <= 0 {
        return Err("Select a valid order identity".into());
    }
    let response = query_reply(
        backend,
        registry,
        context,
        "GetFundOrderByOrderIdQuery",
        &order.to_string(),
        BTreeMap::from([("OrderId".into(), order.into())]),
        deadline,
    )
    .await?;
    if response.as_array().is_some_and(|v| {
        v.len() >= 4 && v[0].as_bool() == Some(false) && v[1].as_i64() == Some(34004)
    }) {
        return Ok(None);
    }
    let value = service_result(&response)?;
    if value.is_nil() {
        return Err("Order read succeeded without a value or explicit NotFound outcome".into());
    }
    Ok(Some(value.clone()))
}


/// Builds the decoded reply for the requested backend query.
async fn query_reply(
    backend: &Backend,
    registry: &Registry,
    context: &BusinessContext,
    name: &str,
    key: &str,
    mut fields: BTreeMap<String, Value>,
    deadline: tokio::time::Instant,
) -> Result<Value, String> {
    if !allowed_query(name) {
        return Err("Unsupported portfolio query".into());
    }
    let kind = format!("{ROOT}.Queries.{name}");
    let (actor, verb) = registry.route(&kind)?;
    let subject = ActorSubject::query(actor, verb, key);
    fields.insert("Subject".into(), subject.wire());
    fields.insert("EntityId".into(), Value::Array(vec![key.into()]));
    fields.insert(
        "CorrelationId".into(),
        uuid::Uuid::new_v4().to_string().into(),
    );
    fields.insert("RequestedOnUtc".into(), timestamp_wire(context.as_of_utc));
    fields.insert("Access".into(), access(false));
    let body = registry.encode(&kind, &fields)?;
    let response = backend
        .request_until(&subject, &body, deadline, None)
        .await?;
    Ok(response)
}


/// Build the exact stable identifier used by the managed create APIs from the
/// immutable Portfolio/Fund DTO (not its normalized command envelope).
pub fn create_command_id(key: uuid::Uuid, value: &Value) -> Result<uuid::Uuid, String> {
    use sha2::{Digest, Sha256};
    if key.is_nil() {
        return Err("IdempotencyKey is required".into());
    }
    let mut input = key.to_bytes_le().to_vec();
    rmpv::encode::write_value(&mut input, value).map_err(|e| e.to_string())?;
    let hash = Sha256::digest(input);
    Ok(uuid::Uuid::from_bytes_le(hash[..16].try_into().unwrap()))
}
