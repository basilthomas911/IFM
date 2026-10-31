//! Workflow presentation only. Backend results, continuation decisions and
//! indicators are displayed as received; no strategy calculation runs here.
use crate::backend::{contracts::*, schema::Registry};
use rmpv::Value;
use serde::Deserialize;
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
use crate::models::{strategy::WorkflowHeader, trade_contracts::FuturesItiSignalV2ReadModel};
use crate::{Node, Property, Snapshot};
pub fn signal_identity(signal: &FuturesItiSignalV2ReadModel) -> Result<String, String> {
    crate::models::strategy::signal_identity(signal)
}
pub fn row(
    registry: &Registry,
    wire: &Value,
    action: u32,
) -> Result<crate::strategy_workspace::WorkflowRow, String> {
    WorkflowHeader::from_backend(registry, wire)?.row(action)
}
fn uuid_n(value: &str) -> Result<String, String> {
    uuid::Uuid::parse_str(value)
        .map(|value| value.simple().to_string())
        .map_err(|_| "Invalid workflow identity".into())
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
        return uuid_n(
            member(registry, kind, wire, "Value")?
                .as_str()
                .ok_or("Missing workflow identity")?,
        );
    }
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
        "System.Single" | "System.Double" => wire
            .as_f64()
            .filter(|v| v.is_finite())
            .ok_or("Invalid workflow display number")?
            .to_string(),
        _ if registry.describe(kind).is_ok_and(|shape| shape.is_enum) => {
            registry.enum_name(kind, wire.as_i64().ok_or("Invalid workflow enum")?)?
        }
        _ => wire
            .as_str()
            .map(str::to_owned)
            .or_else(|| wire.as_i64().map(|value| value.to_string()))
            .or_else(|| wire.as_u64().map(|value| value.to_string()))
            .ok_or_else(|| format!("Invalid scalar evidence {kind}"))?,
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
        let key = member(registry, kind, wire, "CatalogDeployment")?;
        let family = member(registry, kind, wire, "TradeStrategyFamilyId")?.as_i64();
        let version = member(registry, kind, wire, "DefinitionVersion")?.as_i64();
        let valid = if key.is_nil() {
            family.is_some_and(|value| value > 0) && version.is_some_and(|value| value > 0)
        } else {
            let kind = registry.field_kind(kind, "CatalogDeployment")?;
            let deployment =
                registry.enum_value(registry.field_kind(kind, "Kind")?, "Deployment")?;
            member(registry, kind, &key, "Kind")?.as_i64() == Some(deployment)
                && member(registry, kind, &key, "Version")?
                    .as_i64()
                    .is_some_and(|value| value > 0)
                && member(registry, kind, &key, "Id")?
                    .as_str()
                    .and_then(|value| uuid::Uuid::parse_str(value).ok())
                    .is_some_and(|value| !value.is_nil())
                && family == Some(0)
                && version == Some(0)
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
            use crate::models::{cells::Cells, wire_types::FromBackend};
            let row = crate::models::trade_contracts::ActorSubject::from_backend(&subject)?;
            Ok(format!(
                "{}.{}.{}",
                row.cell("ActorType").text(),
                row.cell("Name").text(),
                row.cell("EntityId").text()
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
) -> Result<Node, String> {
    if *budget == 0 || depth > 12 {
        return Ok(Node {
            id: id.into(),
            label: format!("{label} (display limit)"),
            properties: vec![],
            children: vec![],
        });
    }
    *budget -= 1;
    let kind = kind
        .strip_prefix("System.Nullable`1[")
        .and_then(|v| v.strip_suffix(']'))
        .unwrap_or(kind);
    let mut rows = vec![];
    let mut children = vec![];
    if wire.is_nil() {
        rows.push(Property {
            name: "Value".into(),
            value: "Unavailable".into(),
        });
    } else if kind == "System.ReadOnlyMemory`1[System.Byte]" {
        let count = wire.as_slice().map(|v| v.len()).unwrap_or_default();
        rows.push(Property {
            name: "Length".into(),
            value: count.to_string(),
        });
        rows.push(Property {
            name: "IsEmpty".into(),
            value: if count == 0 { "True" } else { "False" }.into(),
        });
    } else if scalar(kind, registry) {
        rows.push(Property {
            name: "Value".into(),
            value: simple(kind, wire, registry)?,
        });
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
                rows.push(Property {
                    name: "Display limit".into(),
                    value: "Additional items omitted".into(),
                });
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
                    rows.push(Property {
                        name: p.label.clone(),
                        value: format!("Unavailable: {error}"),
                    });
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
            rows.push(Property {
                name: p.label.clone(),
                value: display,
            });
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
    Ok(Node {
        id: id.into(),
        label: label.into(),
        properties: rows,
        children,
    })
}
pub fn details(registry: &Registry, wire: &Value) -> Result<Snapshot, String> {
    let shapes = display_shapes()?;
    let view = WorkflowHeader::from_backend(registry, wire)?;
    let prefix = view.id.clone();
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
    Ok(Snapshot {
        workflow_id: prefix.clone(),
        revision: view.revision,
        roots: vec![
            workflow,
            signal,
            Node {
                id: format!("{prefix}/pipeline"),
                label: "Workflow Pipeline".into(),
                properties: vec![],
                children: stages,
            },
        ],
        header: view.header(),
        ..Default::default()
    })
}
