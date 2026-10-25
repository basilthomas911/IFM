use crate::backend::{codec, contracts::*, schema::Registry, transport::Backend};
use futures_util::StreamExt;
use rmpv::Value;
use serde_json::Value as Json;
use std::collections::BTreeMap;

pub async fn query(
    backend: &Backend,
    registry: &Registry,
    kind: &str,
    entity: &str,
    mut fields: BTreeMap<String, Value>,
) -> Result<Value, String> {
    let (actor, verb) = registry.route(kind)?;
    let subject = ActorSubject::query(actor, verb, entity);
    fields.insert("Subject".into(), subject.wire());
    let body = registry.encode(kind, &fields)?;
    let result = backend.request(&subject, &body, None, None).await?;
    Ok(service_result(&result)?.clone())
}
pub async fn command(
    backend: &Backend,
    registry: &Registry,
    subject: &ActorSubject,
    body: &Value,
    success_type: &str,
    failure_type: &str,
    id: &str,
    deadline: tokio::time::Instant,
) -> Result<Json, String> {
    let (actor, success) = registry.route(success_type)?;
    let (failure_actor, failure) = registry.route(failure_type)?;
    if actor != failure_actor {
        return Err("Terminal event actors differ".into());
    }
    tokio::time::timeout_at(deadline, async {
        let mut events = backend.subscribe("Event", actor).await?;
        backend.flush_until(deadline).await?;
        let accepted = backend
            .request_until(subject, body, deadline, None)
            .await
            .map_err(|e| format!("OutcomeUnknown: {e}"))?;
        if let Err(error) = service_result(&accepted) {
            if accepted
                .as_array()
                .and_then(|p| p.first())
                .and_then(Value::as_bool)
                == Some(false)
            {
                return Err(error);
            }
            return Err(format!(
                "OutcomeUnknown: invalid command acknowledgement: {error}"
            ));
        }
        while let Some(event) = events.next().await {
            let verb = event.subject.splitn(4, '.').nth(2).unwrap_or_default();
            if verb != success && verb != failure {
                continue;
            }
            let value = codec::decode(&event.payload)
                .map_err(|e| format!("OutcomeUnknown: invalid completion: {e}"))?;
            let kind = if verb == success {
                success_type
            } else {
                failure_type
            };
            let correlation = registry
                .field(kind, "CommandId", &value)
                .map_err(|e| format!("OutcomeUnknown: invalid completion: {e}"))?;
            if correlation.as_str() != Some(id) {
                continue;
            }
            if verb == failure {
                let code = registry
                    .field(kind, "ErrorCode", &value)
                    .map_err(|e| format!("OutcomeUnknown: invalid failure notification: {e}"))?;
                let message = registry
                    .field(kind, "ErrorMessage", &value)
                    .map_err(|e| format!("OutcomeUnknown: invalid failure notification: {e}"))?;
                if code.as_i64().is_none_or(|v| v <= 0) || message.as_str().is_none() {
                    return Err("OutcomeUnknown: incomplete failure notification".into());
                }
                return Err(format!(
                    "Backend error {}: {}",
                    code,
                    message.as_str().unwrap()
                ));
            }
            return Ok(serde_json::json!({"CommandId":correlation,"Verb":verb}));
        }
        Err("OutcomeUnknown: completion subscription ended".into())
    })
    .await
    .map_err(|_| {
        "OutcomeUnknown: command completion deadline expired; reconcile before retrying".to_string()
    })?
}
