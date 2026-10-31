//! Scheduler UI ownership. Cron evaluation, execution and installation remain
//! backend operations. Run/output cursors are scoped to their selected schedule.
use ifm_ui_egui::backend::schema::Registry;
use rmpv::Value;
use serde_json::{Value as Json, json};
const ROOT: &str = "TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.";
fn model(name: &str) -> String {
    format!("{ROOT}Contracts.{name}")
}
fn field<'a>(intent: &'a Json, name: &str) -> &'a str {
    intent["fields"][name].as_str().unwrap_or_default().trim()
}
fn text(row: &Json, name: &str) -> String {
    row[name].as_str().unwrap_or_default().into()
}
fn display(v: &Json) -> String {
    match v {
        Json::Null => String::new(),
        Json::String(v) => v.clone(),
        Json::Bool(v) => if *v { "True" } else { "False" }.into(),
        _ => v.to_string(),
    }
}
fn id_wire(id: &str) -> Result<Value, String> {
    let id = uuid::Uuid::parse_str(id).map_err(|_| "Invalid scheduled task identity")?;
    Ok(Value::Array(vec![id.to_string().into()]))
}
fn id(row: &Json) -> Result<String, String> {
    row["Id"]["Value"]
        .as_str()
        .filter(|s| uuid::Uuid::parse_str(s).is_ok_and(|id| !id.is_nil()))
        .map(str::to_string)
        .ok_or("Missing scheduled task identity".into())
}
fn member<'a>(wire: &'a Value, index: usize) -> Result<&'a Value, String> {
    wire.as_array()
        .and_then(|v| v.get(index))
        .ok_or("Incomplete scheduled-task payload".into())
}
fn offset(wire: &Value, index: usize) -> Result<Json, String> {
    let value = member(wire, index)?;
    Ok(if value.is_nil() {
        Json::Null
    } else {
        json!(ifm_ui_egui::backend::contracts::reference_offset(value)?)
    })
}
fn document<T: serde::Serialize>(value: &T) -> Result<serde_bytes::ByteBuf, String> {
    ifm_ui_egui::wire::encode_value(&serde_json::to_value(value).map_err(|e| e.to_string())?)
        .map(Into::into)
}
/// Map one persisted run exactly as the hybrid service; no business outcomes or
/// EOD receipts are recomputed on the client. Raw timestamps retain their zones.
pub fn run_document(registry: &Registry, wire: &Value) -> Result<Json, String> {
    let row = registry.project(&model("ScheduledTaskRun"), wire)?;
    let mut result = serde_json::Map::new();
    result.insert("id".into(), json!(id(&row)?));
    for (key, name) in [
        ("status", "Status"),
        ("stage", "Stage"),
        ("detail", "Detail"),
        ("exitCode", "ExitCode"),
        ("valueDate", "ValueDate"),
        ("revision", "Revision"),
        ("operationCommandId", "OperationCommandId"),
        ("processId", "ProcessId"),
        ("standardOutputTail", "StandardOutputTail"),
        ("standardErrorTail", "StandardErrorTail"),
        ("outputDirectory", "OutputDirectory"),
    ] {
        result.insert(key.into(), row[name].clone());
    }
    for (key, index) in [
        ("intendedFireTimeUtc", 7),
        ("startedAtUtc", 10),
        ("finishedAtUtc", 11),
    ] {
        result.insert(key.into(), offset(wire, index)?);
    }
    Ok(Json::Object(result))
}
