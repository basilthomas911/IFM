//! Workflow presentation only. Backend results, continuation decisions and
//! indicators are displayed as received; no strategy calculation runs here.
use crate::backend::{contracts::*, schema::Registry};
use rmpv::Value;
use serde::Deserialize;
use serde_json::{Value as Json, json};
use std::collections::BTreeMap;
pub const VIEW: &str = "TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.IntrinsicTimeStrategyWorkflowView";
pub const SIGNAL: &str =
    "TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels.FuturesItiSignalV2ReadModel";
const STAGES: [(&str, &str, &str); 5] = [
    ("RegimeDiscovery", "regime-discovery", "Regime Discovery"),
    ("MarketCondition", "market-condition", "Market Condition"),
    ("TradeSelection", "trade-selection", "Trade Selection"),
    ("OrderComposition", "order-composition", "Order Composition"),
    ("RiskManagement", "risk-management", "Risk Management"),
];
fn text<'a>(row: &'a Json, key: &str) -> &'a str {
    row[key].as_str().unwrap_or_default()
}
fn time(value: &Json) -> Result<chrono::DateTime<chrono::Utc>, String> {
    chrono::DateTime::parse_from_rfc3339(value.as_str().ok_or("Missing workflow timestamp")?)
        .map(|v| v.to_utc())
        .map_err(|_| "Invalid workflow timestamp".into())
}
fn time_text(value: &Json) -> Result<String, String> {
    Ok(utc_roundtrip(time(value)?))
}
fn uuid_n(value: &Json) -> Result<String, String> {
    uuid::Uuid::parse_str(value.as_str().ok_or("Missing workflow identity")?)
        .map(|v| v.simple().to_string())
        .map_err(|_| "Invalid workflow identity".into())
}
fn amount(value: &Json) -> Result<f64, String> {
    value
        .as_f64()
        .filter(|v| v.is_finite())
        .ok_or("Invalid workflow display number".into())
}
pub fn signal_identity(signal: &Json) -> Result<String, String> {
    let at = time(&signal["IntrinsicTime"])?;
    let ticks = at.timestamp() as i128 * 10_000_000
        + at.timestamp_subsec_nanos() as i128 / 100
        + 621_355_968_000_000_000i128;
    let mut values = vec![
        text(signal, "ContractId").into(),
        text(signal, "ValueDate").into(),
        text(signal, "TimePeriod").into(),
        ticks.to_string(),
        text(signal, "IntrinsicTimeMode").into(),
        text(signal, "IntrinsicTimeTrend").into(),
    ];
    for name in [
        "IntrinsicPrice",
        "IntrinsicTimeGroupId",
        "IntrinsicTimeLength",
        "TrendPrice",
        "TrendExtreme",
        "TrendReversal",
        "TrendDelta",
        "TargetDelta",
        "Threshold",
        "UpTrendTrigger",
        "DownTrendTrigger",
        "BandLevel",
        "ReversalLevel",
    ] {
        values.push(if let Some(v) = signal[name].as_i64() {
            v.to_string()
        } else {
            amount(&signal[name])?.to_string()
        });
    }
    values.push(text(signal, "TradeState").into());
    Ok(values.join("|"))
}
pub fn row(registry: &Registry, wire: &Value, action: u32) -> Result<Json, String> {
    let view = registry.project(VIEW, wire)?;
    let signal = &view["TriggerEvent"]["FuturesItiSignal"];
    let frame = text(&view["EntityId"]["ItiSignalEntityId"], "TimePeriod");
    let occurred = if time(&view["TriggerEvent"]["CreatedOn"])?.year() == 1 {
        time(&view["StartedAtUtc"])?
    } else {
        time(&view["TriggerEvent"]["CreatedOn"])?
    };
    use chrono::Datelike;
    let local = occurred.with_timezone(&chrono_tz::America::New_York);
    let occurred = if frame == "Daily" {
        local.format("%I:%M:%S%.3f %p")
    } else {
        local.format("%d-%b-%Y %I:%M:%S%.3f %p")
    }
    .to_string();
    let status = text(&view, "Status");
    let outcome = text(&view, "Outcome");
    let resolved = if outcome != "None" {
        outcome
    } else {
        match status {
            "Failed" => "PipelineFailed",
            "TimedOut" => "TimedOut",
            "Cancelled" => "Cancelled",
            _ => "None",
        }
    };
    let end = if status == "Started" {
        "In Progress"
    } else {
        match resolved {
            "Completed" => "Approved",
            "NoTrade" => "No Trade",
            "PipelineFailed" => "Pipeline Failed",
            "InvalidResult" => "Invalid Result",
            "TimedOut" => "Timed Out",
            "Cancelled" => "Cancelled",
            "ConsistencyFault" => "Consistency Fault",
            _ => "Unknown",
        }
    };
    let mut actors = vec![];
    for ((key, _, name), short) in STAGES.iter().zip(["RD", "MC", "TS", "OC", "RM"]) {
        let stage = &view[*key];
        let processing = text(stage, "ProcessingStatus");
        let decision = text(stage, "ContinuationDecision");
        if processing == "NotStarted" {
            continue;
        }
        let display = match (processing, decision) {
            ("Processing", _) => "Processing",
            ("Completed", "Proceed") => "Continued",
            ("Completed", "Stop") => "Stopped",
            ("Completed", _) => "Processing",
            _ => "Stopped",
        };
        let mut accessible = format!("{name}: {processing}; continuation {decision}");
        for (key, label) in [("CompletedAtUtc", "completed"), ("FailedAtUtc", "failed")] {
            if !stage[key].is_null() {
                accessible.push_str(&format!("; {label} {}", time_text(&stage[key])?));
            }
        }
        if !stage["Failure"].is_null() {
            let failure = &stage["Failure"];
            accessible.push_str(&format!(
                "; {} {}: {}",
                text(failure, "ErrorType"),
                failure["ErrorCode"],
                text(failure, "ErrorMessage")
            ));
        }
        actors.push(json!({"name":short,"value":accessible,"state":display}));
    }
    Ok(
        json!({"id":uuid_n(&view["WorkflowId"]["Value"])?,"action":action,"triggerIdentity":signal_identity(signal)?,"occurredOn":occurred,"signal":signal["IntrinsicTimeMode"],"trend":signal["IntrinsicTimeTrend"],"futuresPrice":format!("{:.2}",amount(&signal["IntrinsicPrice"])?),"outcome":outcome,"endState":end,"actors":actors}),
    )
}
#[derive(Deserialize)]
struct DisplayShape {
    name: String,
    properties: Vec<DisplayProperty>,
    #[serde(default, rename = "opaqueByteArray")]
    opaque_byte_array: bool,
}
#[derive(Deserialize)]
struct DisplayProperty {
    name: String,
    #[serde(rename = "type")]
    kind: String,
    #[serde(rename = "DisplayName")]
    label: String,
    #[serde(rename = "IsBrowsable")]
    browsable: bool,
    key: Option<usize>,
}
fn display_shapes() -> Result<&'static BTreeMap<String, DisplayShape>, String> {
    static SHAPES: std::sync::OnceLock<Result<BTreeMap<String, DisplayShape>, String>> =
        std::sync::OnceLock::new();
    SHAPES
        .get_or_init(|| {
            serde_json::from_str(include_str!(
                "../../tests/fixtures/backend/strategy-display-shapes.json"
            ))
            .map_err(|e| e.to_string())
        })
        .as_ref()
        .map_err(Clone::clone)
}
pub fn field_names(kind: &str) -> Result<Vec<String>, String> {
    Ok(display_shapes()?
        .get(kind)
        .ok_or_else(|| format!("Missing display fields for {kind}"))?
        .properties
        .iter()
        .map(|p| p.name.clone())
        .collect())
}
pub(crate) fn raw_property(
    registry: &Registry,
    kind: &str,
    wire: &Value,
    name: &str,
) -> Result<Value, String> {
    member(registry, kind, wire, name).or_else(|_| derived(registry, kind, wire, name))
}
/// Ordered display cells, using the same generated public properties and value
/// formatting as the hybrid's BridgeDocument.Fields. These are presentation
/// strings only; commands retain their independent typed immutable DTOs.
pub fn fields(
    registry: &Registry,
    kind: &str,
    wire: &Value,
) -> Result<Vec<(String, String)>, String> {
    let shape = display_shapes()?
        .get(kind)
        .ok_or_else(|| format!("Missing display fields for {kind}"))?;
    shape
        .properties
        .iter()
        .map(|property| {
            let value = member(registry, kind, wire, &property.name)
                .or_else(|_| derived(registry, kind, wire, &property.name))?;
            Ok((
                property.name.clone(),
                evidence(registry, &property.kind, &value)?,
            ))
        })
        .collect()
}
fn scalar(kind: &str, registry: &Registry) -> bool {
    kind.starts_with("System.")
        && matches!(
            kind,
            "System.String"
                | "System.Decimal"
                | "System.Guid"
                | "System.DateTime"
                | "System.DateOnly"
                | "System.TimeSpan"
                | "System.Boolean"
                | "System.Byte"
                | "System.SByte"
                | "System.Int16"
                | "System.UInt16"
                | "System.Int32"
                | "System.UInt32"
                | "System.Int64"
                | "System.UInt64"
                | "System.Single"
                | "System.Double"
                | "System.Char"
        )
        || kind == "TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId"
        || registry.describe(kind).is_ok_and(|v| v.is_enum)
}
fn simple(kind: &str, wire: &Value, registry: &Registry) -> Result<String, String> {
    if wire.is_nil() {
        return Ok("null".into());
    }
    if kind == "TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId" {
        return uuid_n(&registry.project(kind, wire)?["Value"]);
    }
    let value = registry.project(kind, wire)?;
    Ok(match kind {
        "System.DateTime" => utc_roundtrip(timestamp(wire)?),
        "System.DateOnly" => date_only(wire)?.format("%m/%d/%Y").to_string(),
        "System.TimeSpan" => crate::services::parameter_fields::timespan(
            wire.as_i64().ok_or("Invalid workflow duration")?,
        ),
        "System.Boolean" => if wire.as_bool() == Some(true) {
            "True"
        } else {
            "False"
        }
        .into(),
        "System.Single" | "System.Double" => amount(&value)?.to_string(),
        _ => value
            .as_str()
            .map(str::to_string)
            .unwrap_or_else(|| value.to_string()),
    })
}
fn member(registry: &Registry, kind: &str, wire: &Value, name: &str) -> Result<Value, String> {
    if let Value::Map(entries) = wire {
        return entries
            .iter()
            .find(|(key, _)| key.as_str() == Some(name))
            .map(|(_, value)| value.clone())
            .ok_or_else(|| format!("Missing workflow field {name}"));
    }
    let shape = registry.describe(kind)?;
    let (_, index, _) = shape
        .members
        .iter()
        .find(|(n, _, _)| n == name)
        .ok_or_else(|| format!("Missing workflow field {name}"))?;
    wire.as_array()
        .and_then(|v| v.get(*index))
        .cloned()
        .ok_or("Incomplete workflow value".into())
}
fn derived(registry: &Registry, kind: &str, wire: &Value, name: &str) -> Result<Value, String> {
    if kind == "TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyReference"
        && name == "IsValid"
    {
        // Published DTO display getter; command authorization stays on the backend.
        let row = registry.project(kind, wire)?;
        let key = &row["CatalogDeployment"];
        let valid = if key.is_null() {
            row["TradeStrategyFamilyId"].as_i64().is_some_and(|v| v > 0)
                && row["DefinitionVersion"].as_i64().is_some_and(|v| v > 0)
        } else {
            key["Kind"] == "Deployment"
                && key["Version"].as_i64().is_some_and(|v| v > 0)
                && key["Id"]
                    .as_str()
                    .and_then(|v| uuid::Uuid::parse_str(v).ok())
                    .is_some_and(|v| !v.is_nil())
                && row["TradeStrategyFamilyId"] == 0
                && row["DefinitionVersion"] == 0
        };
        return Ok(valid.into());
    }
    if name == "State" && kind.ends_with("WorkflowStrategyStateUpdatedEvent") {
        return member(registry, kind, wire, "WorkflowDefinition");
    }
    if kind == "System.ReadOnlyMemory`1[System.Byte]" {
        let length = match wire {
            Value::Binary(bytes) => bytes.len(),
            _ => return Err("Invalid memory evidence".into()),
        };
        return match name {
            "Length" => Ok((length as u64).into()),
            "IsEmpty" => Ok((length == 0).into()),
            _ => Err(format!("Unsupported memory field {name}")),
        };
    }
    if kind == SIGNAL {
        if matches!(name, "EntityId" | "Id") {
            let contract = member(registry, kind, wire, "ContractId")?;
            let contract = if contract.is_nil() {
                "".into()
            } else {
                contract
            };
            let date = member(registry, kind, wire, "ValueDate")?;
            let frame = member(registry, kind, wire, "TimePeriod")?;
            return if name == "EntityId" {
                let start = member(registry, kind, wire, "TimeFrameStartValueDate")?;
                Ok(Value::Array(vec![
                    contract,
                    if start.as_i64() == Some(0) {
                        date
                    } else {
                        start
                    },
                    frame,
                ]))
            } else {
                Ok(Value::Array(vec![
                    contract,
                    date,
                    frame,
                    member(registry, kind, wire, "IntrinsicTime")?,
                ]))
            };
        }
        if name == "IsValid" {
            let contract = member(registry, kind, wire, "ContractId")?;
            let date = member(registry, kind, wire, "ValueDate")?;
            return Ok((contract.as_str().is_some_and(|v| !v.is_empty())
                && date.as_i64().is_some_and(|v| v > 0))
            .into());
        }
    }
    match name {
        "ActorId" | "MailboxId" | "ActorTypeId" | "ThreadId" => {
            let actor = member(registry, kind, wire, "ActorType")?;
            let actor_name = member(registry, kind, wire, "Name")?;
            let mut fields = vec![("ActorType".into(), actor), ("Name".into(), actor_name)];
            if name == "ActorTypeId" {
                fields.push(("Verb".into(), member(registry, kind, wire, "Verb")?));
            }
            if name == "ThreadId" {
                fields.push(("EntityId".into(), member(registry, kind, wire, "EntityId")?));
            }
            Ok(Value::Map(fields))
        }
        "StreamId" => {
            let subject = if kind == "TomasAI.IFM.Shared.EventModelActor.ActorSubject" {
                wire.clone()
            } else {
                member(registry, kind, wire, "Subject")?
            };
            let row =
                registry.project("TomasAI.IFM.Shared.EventModelActor.ActorSubject", &subject)?;
            Ok(format!(
                "{}.{}.{}",
                text(&row, "ActorType"),
                text(&row, "Name"),
                text(&row, "EntityId")
            )
            .into())
        }
        // The managed getter is process-local display metadata, not the event's
        // author or an authorization identity. Match it without changing wire data.
        "UserName" => Ok(format!(
            "{}\\{}",
            std::env::var("USERDOMAIN").unwrap_or_default(),
            std::env::var("USERNAME").unwrap_or_default()
        )
        .into()),
        "EventName" => Ok(kind.rsplit('.').next().unwrap_or_default().into()),
        "EventType" => Ok(registry
            .enum_value("TomasAI.IFM.Shared.EventSourcing.EventType", "DomainEvent")?
            .into()),
        "TimeFrameStartValueDate" => member(registry, kind, wire, "ValueDate"),
        "ContentSize" => {
            let shape = registry.describe(kind)?;
            for (name, index, inner) in shape.members.iter().filter(|(n, _, _)| {
                matches!(
                    n.as_str(),
                    "RiskResult"
                        | "CompositionResult"
                        | "SelectionResult"
                        | "AssessmentResult"
                        | "RegimeResult"
                )
            }) {
                let value = wire
                    .as_array()
                    .and_then(|v| v.get(*index))
                    .ok_or("Incomplete result envelope")?;
                if !value.is_nil() {
                    let mut bytes = vec![];
                    rmpv::encode::write_value(&mut bytes, value).map_err(|e| e.to_string())?;
                    let _ = (name, inner);
                    return Ok((bytes.len() as i64).into());
                }
            }
            let payload = member(registry, kind, wire, "Payload")?;
            Ok((payload.as_slice().map(|v| v.len()).unwrap_or_default() as i64).into())
        }
        "HasContent" => {
            let size = derived(registry, kind, wire, "ContentSize")?;
            Ok((size.as_i64().unwrap_or_default() > 0).into())
        }
        "EventSource" | "CommandName" | "CommandType" => {
            if name == "CommandName" {
                return Ok(kind.rsplit('.').next().unwrap_or_default().into());
            }
            if let Ok((actor, _)) = registry.route(kind) {
                return Ok(actor.into());
            }
            let subject = member(registry, kind, wire, "Subject")?;
            let values = subject.as_array().ok_or("Missing event subject")?;
            Ok(values.get(1).cloned().unwrap_or(Value::Nil))
        }
        _ => Err(format!("No native display getter for {kind}.{name}")),
    }
}
/// The hybrid evidence pane uses BridgeDocument.Display, not JSON. Preserve
/// generated property order and its scalar/collection conventions so a frozen
/// backend result has the same readable evidence in either frontend.
pub fn evidence(registry: &Registry, kind: &str, wire: &Value) -> Result<String, String> {
    let shapes = display_shapes()?;
    fn render(
        registry: &Registry,
        shapes: &BTreeMap<String, DisplayShape>,
        kind: &str,
        wire: &Value,
        depth: usize,
        budget: &mut usize,
    ) -> Result<String, String> {
        if depth > 64 || *budget == 0 {
            return Err("Evidence display exceeds its bounded document size".into());
        }
        *budget -= 1;
        if wire.is_nil() {
            return Ok(String::new());
        }
        let kind = kind
            .strip_prefix("System.Nullable`1[")
            .and_then(|v| v.strip_suffix(']'))
            .unwrap_or(kind);
        if kind == "System.Byte[]" {
            return Ok("System.Byte[]".into());
        }
        if shapes
            .get(kind)
            .is_some_and(|shape| shape.opaque_byte_array)
        {
            return Ok(kind.into());
        }
        if kind == "System.DateOnly" {
            return Ok(date_only(wire)?.to_string());
        }
        if kind == "System.DateTimeOffset" {
            let value = datetime_offset(wire)?;
            return Ok(format!(
                "{}.{:07}{}",
                value.format("%Y-%m-%dT%H:%M:%S"),
                value.timestamp_subsec_nanos() / 100,
                value.format("%:z")
            ));
        }
        if kind == "TomasAI.IFM.Domain.Trade.Shared.StrategyWorkflowId" {
            return render(
                registry,
                shapes,
                "System.Guid",
                &member(registry, kind, wire, "Value")?,
                depth + 1,
                budget,
            )
            .map(|v| format!("{{ Value: {v} }}"));
        }
        if scalar(kind, registry) {
            return simple(kind, wire, registry);
        }
        if let Some(inner) = kind.strip_suffix("[]").or_else(|| {
            kind.strip_prefix("System.Collections.Immutable.ImmutableArray`1[")
                .and_then(|v| v.strip_suffix(']'))
        }) {
            let items = wire.as_array().ok_or("Invalid evidence array")?;
            let values = items
                .iter()
                .map(|v| render(registry, shapes, inner, v, depth + 1, budget))
                .collect::<Result<Vec<_>, _>>()?;
            return Ok(format!("[ {} ]", values.join(", ")));
        }
        let (kind, wire) = if let Ok(shape) = registry.describe(kind) {
            if !shape.unions.is_empty() {
                let parts = wire
                    .as_array()
                    .filter(|v| v.len() == 2)
                    .ok_or("Invalid evidence union")?;
                let tag = parts[0].as_i64().ok_or("Invalid evidence union tag")?;
                let concrete = &shape
                    .unions
                    .iter()
                    .find(|(v, _)| *v == tag)
                    .ok_or("Unknown evidence union variant")?
                    .1;
                return render(registry, shapes, concrete, &parts[1], depth + 1, budget);
            }
            (kind, wire)
        } else {
            (kind, wire)
        };
        let shape = shapes
            .get(kind)
            .ok_or_else(|| format!("Missing evidence display descriptor {kind}"))?;
        let mut values = vec![];
        for property in &shape.properties {
            let child = member(registry, kind, wire, &property.name)
                .or_else(|_| derived(registry, kind, wire, &property.name))?;
            values.push(format!(
                "{}: {}",
                property.name,
                render(registry, shapes, &property.kind, &child, depth + 1, budget)?
            ));
        }
        Ok(format!("{{ {} }}", values.join(", ")))
    }
    let result = render(registry, &shapes, kind, wire, 0, &mut 100_000)?;
    if result.len() > 4 * 1024 * 1024 {
        return Err("Evidence display exceeds 4 MiB".into());
    }
    Ok(result)
}
fn node(
    registry: &Registry,
    shapes: &BTreeMap<String, DisplayShape>,
    kind: &str,
    wire: &Value,
    id: &str,
    label: &str,
    depth: usize,
    budget: &mut usize,
    root: bool,
) -> Result<Json, String> {
    if *budget == 0 || depth > 12 {
        return Ok(
            json!({"id":id,"label":format!("{label} (display limit)"),"properties":[],"children":[]}),
        );
    }
    *budget -= 1;
    let kind = kind
        .strip_prefix("System.Nullable`1[")
        .and_then(|v| v.strip_suffix(']'))
        .unwrap_or(kind);
    let mut rows = vec![];
    let mut children = vec![];
    if wire.is_nil() {
        rows.push(json!({"name":"Value","value":"Unavailable"}));
    } else if kind == "System.ReadOnlyMemory`1[System.Byte]" {
        let count = wire.as_slice().map(|v| v.len()).unwrap_or_default();
        rows.push(json!({"name":"Length","value":count.to_string()}));
        rows.push(json!({"name":"IsEmpty","value":if count==0{"True"}else{"False"}}));
    } else if scalar(kind, registry) {
        rows.push(json!({"name":"Value","value":simple(kind,wire,registry)?}));
    } else if let Some(inner) = kind.strip_suffix("[]").or_else(|| {
        kind.strip_prefix("System.Collections.Immutable.ImmutableArray`1[")
            .and_then(|v| v.strip_suffix(']'))
    }) {
        let values = if let Value::Binary(bytes) = wire {
            bytes.iter().map(|v| Value::from(*v)).collect::<Vec<_>>()
        } else {
            wire.as_array()
                .ok_or("Invalid workflow collection")?
                .clone()
        };
        for (index, value) in values.iter().enumerate() {
            if index >= 200 || *budget == 0 {
                rows.push(json!({"name":"Display limit","value":"Additional items omitted"}));
                break;
            }
            children.push(node(
                registry,
                shapes,
                inner,
                value,
                &format!("{id}/{index}"),
                &format!("[{index}]"),
                depth + 1,
                budget,
                false,
            )?);
        }
    } else {
        let (kind, wire) = if let Ok(shape) = registry.describe(kind) {
            if !shape.unions.is_empty() {
                let parts = wire
                    .as_array()
                    .filter(|v| v.len() == 2)
                    .ok_or("Invalid workflow union")?;
                let tag = parts[0].as_i64().ok_or("Invalid workflow tag")?;
                let target = shape
                    .unions
                    .iter()
                    .find(|(n, _)| *n == tag)
                    .ok_or("Unknown workflow tag")?;
                (target.1.clone(), parts[1].clone())
            } else {
                (kind.into(), wire.clone())
            }
        } else {
            (kind.into(), wire.clone())
        };
        let shape = shapes
            .get(&kind)
            .ok_or_else(|| format!("No generated workflow display schema for {kind}"))?;
        for p in &shape.properties {
            if !p.browsable
                || p.name == "StageSummary"
                || root
                    && matches!(
                        p.name.as_str(),
                        "RegimeDiscovery"
                            | "MarketCondition"
                            | "TradeSelection"
                            | "OrderComposition"
                            | "RiskManagement"
                            | "TriggerEvent"
                            | "Sections"
                    )
            {
                continue;
            }
            let value = if let Some(index) = p.key {
                wire.as_array()
                    .and_then(|v| v.get(index))
                    .cloned()
                    .ok_or("Missing workflow display slot".into())
            } else {
                member(registry, &kind, &wire, &p.name)
                    .or_else(|_| derived(registry, &kind, &wire, &p.name))
            };
            let value = match value {
                Ok(value) => value,
                Err(error) => {
                    rows.push(json!({"name":p.label,"value":format!("Unavailable: {error}")}));
                    continue;
                }
            };
            let child_kind = p
                .kind
                .strip_prefix("System.Nullable`1[")
                .and_then(|v| v.strip_suffix(']'))
                .unwrap_or(&p.kind);
            let display = if value.is_nil() {
                "null".into()
            } else if scalar(child_kind, registry) {
                simple(child_kind, &value, registry)?
            } else if child_kind.ends_with("[]") {
                child_kind.rsplit('.').next().unwrap_or(child_kind).into()
            } else {
                shapes
                    .get(child_kind)
                    .map(|s| s.name.clone())
                    .unwrap_or_else(|| child_kind.rsplit('.').next().unwrap_or(child_kind).into())
            };
            rows.push(json!({"name":p.label,"value":display}));
            if !value.is_nil() && !scalar(child_kind, registry) && *budget > 0 {
                children.push(node(
                    registry,
                    shapes,
                    child_kind,
                    &value,
                    &format!("{id}/{}", p.name),
                    &p.label,
                    depth + 1,
                    budget,
                    false,
                )?);
            }
        }
    }
    Ok(json!({"id":id,"label":label,"properties":rows,"children":children}))
}
pub fn details(registry: &Registry, wire: &Value) -> Result<Json, String> {
    let shapes = display_shapes()?;
    let view = registry.project(VIEW, wire)?;
    let prefix = uuid_n(&view["WorkflowId"]["Value"])?;
    let mut budget = 10000;
    let workflow = node(
        registry,
        &shapes,
        VIEW,
        wire,
        &format!("{prefix}/workflow"),
        "Workflow",
        0,
        &mut budget,
        true,
    )?;
    let trigger = member(registry, VIEW, wire, "TriggerEvent")?;
    let signal = node(
        registry,
        &shapes,
        "TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events.FuturesItiSignalGeneratedEvent",
        &trigger,
        &format!("{prefix}/signal"),
        "ITI Signal",
        0,
        &mut budget,
        false,
    )?;
    let mut stages = vec![];
    for (key, path, title) in STAGES {
        stages.push(node(registry,&shapes,"TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model.StrategyWorkflowStageState",&member(registry,VIEW,wire,key)?,&format!("{prefix}/pipeline/{path}"),title,0,&mut budget,false)?);
    }
    Ok(
        json!({"workflowId":prefix,"revision":view["WorkflowRevision"],"roots":[workflow,signal,{"id":format!("{prefix}/pipeline"),"label":"Workflow Pipeline","properties":[],"children":stages}],"header":header(&view)?}),
    )
}
pub fn header(view: &Json) -> Result<String, String> {
    let entity = &view["EntityId"];
    let iti = &entity["ItiSignalEntityId"];
    let day = chrono::NaiveDate::parse_from_str(text(iti, "ValueDate"), "%Y-%m-%d")
        .map_err(|_| "Invalid workflow entity date")?;
    let mut out = "=== WORKFLOW ===\r\n".to_string();
    let entity = format!(
        "{}.{}.{}.{}",
        text(entity, "WorkflowDefinitionId"),
        text(iti, "ContractId"),
        day.format("%Y%m%d"),
        text(iti, "TimePeriod")
    );
    for (label, value) in [
        ("Workflow ID", view["WorkflowId"]["Value"].clone()),
        ("Workflow entity", json!(entity)),
        ("Trigger event ID", view["TriggerEventId"].clone()),
        ("Correlation ID", view["CorrelationId"].clone()),
        ("Causation ID", view["CausationId"].clone()),
        (
            "Definition",
            view["EntityId"]["WorkflowDefinitionId"].clone(),
        ),
        (
            "Definition version",
            view["WorkflowDefinitionVersion"].clone(),
        ),
        ("Revision", view["WorkflowRevision"].clone()),
        ("Machine status", view["Status"].clone()),
        ("Business outcome", view["Outcome"].clone()),
        ("Current stage", view["CurrentStage"].clone()),
        ("Stop reason", view["StopReasonCode"].clone()),
        ("Started UTC", view["StartedAtUtc"].clone()),
        ("Updated UTC", view["UpdatedAtUtc"].clone()),
        ("Expires UTC", view["ExpiresAtUtc"].clone()),
        ("Terminal UTC", view["TerminalAtUtc"].clone()),
    ] {
        let display = if value.is_null() || value.as_str().is_some_and(|v| v.trim().is_empty()) {
            "None".into()
        } else if label.ends_with("UTC") {
            time_text(&value)?
        } else {
            value
                .as_str()
                .map(str::to_string)
                .unwrap_or_else(|| value.to_string())
        };
        out.push_str(&format!("{label}: {display}\r\n"));
    }
    out.push_str("\r\n");
    Ok(out)
}
