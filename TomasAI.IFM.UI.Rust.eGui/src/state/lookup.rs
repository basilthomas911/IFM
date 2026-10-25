use crate::{
    backend::{codec, contracts::*, schema::Registry, transport::Backend},
    lookup_editor::{EditorOutcome, EditorPublication, LookupRow},
    services::reference,
};
use futures_util::StreamExt;
use rmpv::Value;
use serde_json::Value as Json;
use std::collections::BTreeMap;

#[derive(Default)]
pub struct LookupController {
    pub publication: EditorPublication,
    history: BTreeMap<String, (Json, EditorOutcome)>,
}
impl LookupController {
    pub async fn execute(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
    ) -> Result<EditorPublication, String> {
        let id = intent["requestId"]
            .as_str()
            .and_then(|s| uuid::Uuid::parse_str(s).ok())
            .filter(|id| !id.is_nil())
            .ok_or("RequestId is required")?
            .to_string();
        if intent["schema"] != 1 || intent["view"] != "lookup" {
            return Err("Invalid lookup intent".into());
        }
        if let Some((prior, outcome)) = self.history.get(&id) {
            if prior != intent {
                return Err("RequestId already belongs to another lookup request".into());
            }
            let outcome = outcome.clone();
            self.publication.generation += 1;
            self.publication.outcomes = vec![outcome];
            return Ok(self.publication.clone());
        }
        if self.history.len() >= 256 {
            let completed = self
                .history
                .iter()
                .find(|(_, (_, outcome))| outcome.state != "Unknown")
                .map(|(id, _)| id.clone());
            if let Some(id) = completed {
                self.history.remove(&id);
            } else {
                return Err(
                    "Reconcile uncertain lookup outcomes before submitting more requests".into(),
                );
            }
        }
        let action = intent["intent"].as_str().ok_or("Missing lookup action")?;
        let result = if action == "load" {
            reference::load_lookups(backend, registry)
                .await
                .map(|rows| self.publication.rows = rows)
        } else {
            if intent["expectedGeneration"].as_u64() != Some(self.publication.generation) {
                return Err("Lookup definitions changed; reload before editing".into());
            }
            self.mutate(backend, registry, intent, &id).await
        };
        self.publication.schema = 1;
        self.publication.generation += 1;
        self.publication.busy = false;
        let (state, message) = match result {
            Ok(()) => ("Completed", "Completed".to_string()),
            Err(error) => (
                if error.starts_with("OutcomeUnknown:") {
                    "Unknown"
                } else {
                    "Failed"
                },
                error,
            ),
        };
        self.publication.status = message.clone();
        let outcome = EditorOutcome {
            request_id: id.clone(),
            state: state.into(),
            message,
        };
        self.publication.outcomes = vec![outcome.clone()];
        self.history.insert(id, (intent.clone(), outcome));
        Ok(self.publication.clone())
    }
    async fn mutate(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        intent: &Json,
        id: &str,
    ) -> Result<(), String> {
        let action = intent["intent"].as_str().ok_or("Missing action")?;
        let row: LookupRow =
            serde_json::from_value(intent["lookup"].clone()).map_err(|e| e.to_string())?;
        if row.lookup_type_name.trim().is_empty() || row.short_code.trim().is_empty() {
            return Err("Lookup name and short code are required".into());
        }
        if action != "add"
            && !self
                .publication
                .rows
                .iter()
                .any(|r| r.lookup_type_name == row.lookup_type_name && r.order_id == row.order_id)
        {
            return Err("Select an existing lookup definition".into());
        }
        let (name, verb, terminal, error_code) = match action {
            "add" => ("AddLookupTypeCommand", "Add", "Added", 8001),
            "change" => ("ChangeLookupTypeCommand", "Change", "Changed", 8002),
            "remove" => ("RemoveLookupTypeCommand", "Remove", "Removed", 8002),
            _ => return Err("Unsupported lookup action".into()),
        };
        let entity = Value::Array(vec![
            row.lookup_type_name.clone().into(),
            row.order_id.into(),
        ]);
        let subject = ActorSubject {
            kind: ActorKind::Command,
            name: "LookupTypeCommand".into(),
            verb: verb.into(),
            entity: format!("{}.{}", row.lookup_type_name, row.order_id),
        };
        // New records use the authoritative API clock, regardless of any
        // timestamp supplied by a stale UI draft. Existing audit dates survive
        // changes/removals. Failure to read the API clock prevents submission.
        let created = if action == "add" {
            crate::services::business_context::BusinessContext::load(backend).await?.as_of_utc
        } else {
            chrono::DateTime::parse_from_rfc3339(&row.created_on)
                .map_err(|_| "Lookup CreatedOn must be an exact timestamp")?
                .to_utc()
        };
        let model = Value::Array(vec![
            row.lookup_type_name.clone().into(),
            row.short_code.clone().into(),
            row.order_id.into(),
            row.description.clone().into(),
            timestamp_wire(created),
            row.created_by.clone().into(),
        ]);
        let mut fields = BTreeMap::from([
            ("CommandId".into(), id.into()),
            ("Subject".into(), subject.wire()),
            ("PostEvents".into(), false.into()),
            ("EntityId".into(), entity.clone()),
            ("ErrorCode".into(), error_code.into()),
            ("RouteTo".into(), 15.into()),
        ]);
        if action != "add" {
            fields.insert("LookupTypeId".into(), entity);
            fields.insert("Overwrite".into(), false.into());
        }
        if action != "remove" {
            fields.insert("LookupType".into(), model);
        }
        let body = registry.encode(
            &format!("TomasAI.IFM.Domain.Reference.Shared.Commands.{name}"),
            &fields,
        )?;
        let deadline = tokio::time::Instant::now() + backend.default_timeout();
        let outcome = tokio::time::timeout_at(deadline, async {
            let mut subscription = backend.subscribe("Event", "LookupTypeEvent").await?;
            backend.flush_until(deadline).await?;
            let response = backend
                .request_until(&subject, &body, deadline, None)
                .await
                .map_err(|e| format!("OutcomeUnknown: {e}"))?;
            if let Err(error) = service_result(&response) {
                if response
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
            while let Some(event) = subscription.next().await {
                let verb = event.subject.splitn(4, '.').nth(2).unwrap_or_default();
                let success = verb == format!("{terminal}Complete");
                if !success && verb != format!("{terminal}Fail") {
                    continue;
                }
                let value = codec::decode(&event.payload)
                    .map_err(|e| format!("OutcomeUnknown: invalid lookup notification: {e}"))?;
                let named = registry
                    .project(
                        &format!(
                            "TomasAI.IFM.Domain.Reference.Shared.Events.LookupType{verb}Event"
                        ),
                        &value,
                    )
                    .map_err(|e| format!("OutcomeUnknown: invalid lookup notification: {e}"))?;
                if named["CommandId"].as_str() != Some(id) {
                    continue;
                }
                if !success {
                    return Err(format!(
                        "Backend error {}: {}",
                        named["ErrorCode"],
                        named["ErrorMessage"]
                            .as_str()
                            .unwrap_or("Lookup command failed")
                    ));
                }
                if action == "remove" {
                    self.publication.rows.retain(|r| {
                        r.lookup_type_name != row.lookup_type_name || r.order_id != row.order_id
                    });
                } else {
                    let model = &named["LookupType"];
                    let updated = LookupRow {
                        lookup_type_name: model["LookupTypeName"]
                            .as_str()
                            .ok_or("Missing lookup name")?
                            .into(),
                        short_code: model["ShortCode"].as_str().unwrap_or_default().into(),
                        order_id: model["OrderId"]
                            .as_i64()
                            .and_then(|n| i32::try_from(n).ok())
                            .ok_or("Invalid lookup order")?,
                        description: model["Description"].as_str().unwrap_or_default().into(),
                        created_on: model["CreatedOn"].as_str().unwrap_or_default().into(),
                        created_by: model["CreatedBy"].as_str().unwrap_or_default().into(),
                    };
                    self.publication.rows.retain(|r| {
                        r.lookup_type_name != updated.lookup_type_name
                            || r.order_id != updated.order_id
                    });
                    self.publication.rows.push(updated);
                }
                self.publication.rows.sort_by(|a, b| {
                    a.lookup_type_name
                        .encode_utf16()
                        .cmp(b.lookup_type_name.encode_utf16())
                        .then(a.order_id.cmp(&b.order_id))
                });
                return Ok(());
            }
            Err("OutcomeUnknown: lookup notification stream ended".into())
        })
        .await;
        outcome.map_err(|_| "OutcomeUnknown: lookup completion deadline expired; reload and verify before retrying".to_string())?
    }
}
