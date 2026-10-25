//! Portfolio API boundary. Identity allocation and commands are never retried;
//! every request uses the API clock and the caller's absolute deadline.
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use crate::services::business_context::BusinessContext;
use rmpv::Value;
use std::collections::BTreeMap;
pub const ROOT: &str = "TomasAI.IFM.Domain.Portfolio.Shared";
pub fn principal() -> String {
    format!(
        "interactive:{}",
        std::env::var("USERNAME").unwrap_or_default()
    )
}
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
            | "AllocatePortfolioBusinessIdQuery"
    )
}
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
    fields.insert(
        "Access".into(),
        access(name == "AllocatePortfolioBusinessIdQuery"),
    );
    let body = registry.encode(&kind, &fields)?;
    let response = backend
        .request_until(&subject, &body, deadline, None)
        .await
        .map_err(|error| {
            if name == "AllocatePortfolioBusinessIdQuery" {
                format!("OutcomeUnknown: identity allocation may have completed: {error}")
            } else {
                error
            }
        })?;
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
