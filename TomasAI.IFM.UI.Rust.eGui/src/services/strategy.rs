//! Read-only strategy history boundary. A historical item is hydrated from the
//! backend's complete state payload with identity/revision checks before use.
use crate::backend::{codec, contracts::*, schema::Registry, transport::Backend};
use crate::models::{
    strategy::{StageFailure, StageHeader, WorkflowView, workflow_grid_row},
    trade_contracts::FuturesItiSignalV2ReadModel,
    wire_types::FromBackend,
};
use crate::services::business_context::BusinessContext;
use crate::strategy_workspace::WorkflowRow;
use chrono::{DateTime, Datelike, NaiveDate, Utc};
use rmpv::Value;
use std::collections::BTreeMap;

/// A compact workflow grid row and the metadata needed to select and order it.
#[derive(Clone)]
pub struct GridWorkflow {
    pub row: WorkflowRow,
    pub revision: u64,
    pub occurred: DateTime<Utc>,
    pub signal_sequence: i64,
    pub history_order_at: Option<DateTime<Utc>>,
}

#[derive(Clone, Debug)]
pub struct QueueAssignment {
    pub key: String,
    pub fund: String,
    pub family: String,
    pub root: String,
    pub frame: String,
}

impl QueueAssignment {
    pub fn accepts(&self, contract: &str) -> bool {
        let Some(suffix) = contract.strip_prefix(&self.root) else { return false; };
        (suffix.len() == 8 && suffix.bytes().all(|byte| byte.is_ascii_digit()))
            || ((2..=5).contains(&suffix.len())
                && b"FGHJKMNQUVXZ".contains(&suffix.as_bytes()[0])
                && suffix.as_bytes()[1..].iter().all(u8::is_ascii_digit))
    }
}

pub fn assignment_key(value: &Value) -> Result<String, String> {
    let fields = value.as_array().filter(|fields| fields.len() >= 6)
        .ok_or("Invalid Strategy Updates assignment")?;
    Ok(format!("~{}~{}~{}",
        fields[0].as_u64().ok_or("Invalid assignment portfolio")?,
        fields[1].as_u64().ok_or("Invalid assignment fund")?,
        grid_text(&fields[3])?))
}

pub struct QueueScope<'a> {
    pub contract: &'a str,
    pub assignment: &'a str,
    pub before: Option<&'a GridWorkflow>,
}

pub const ROOT: &str = "TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime";

/// Provides kind for the strategy implementation.
pub fn kind(section: &str, name: &str) -> String {
    format!("{ROOT}.{section}.{name}")
}

/// Looks up the requested member in the supplied record.
pub fn member(registry: &Registry, kind: &str, wire: &Value, name: &str) -> Result<Value, String> {
    if let Value::Map(entries) = wire {
        return entries
            .iter()
            .find(|(key, _)| key.as_str() == Some(name))
            .map(|(_, v)| v.clone())
            .ok_or_else(|| format!("Missing {kind}.{name}"));
    }
    let shape = registry.describe(kind)?;
    let (_, index, _) = shape
        .members
        .iter()
        .find(|(n, _, _)| n == name)
        .ok_or_else(|| format!("Missing {kind}.{name}"))?;
    wire.as_array()
        .and_then(|v| v.get(*index))
        .cloned()
        .ok_or("Incomplete strategy payload".into())
}

/// Provides graph window for the strategy implementation.
pub fn graph_window(
    context: &BusinessContext,
    frame: &str,
) -> Result<(DateTime<Utc>, DateTime<Utc>), String> {
    let end = context.as_of_utc;
    let day = context.value_date;
    let start = match frame {
        "Daily" => {
            return Ok((
                end.checked_sub_signed(chrono::Duration::hours(8))
                    .ok_or("Graph start outside clock range")?,
                end,
            ));
        }
        "Weekly" => day
            .checked_sub_signed(chrono::Duration::days(6))
            .ok_or("Graph start outside date range")?,
        "Monthly" => {
            NaiveDate::from_ymd_opt(day.year(), day.month(), 1).ok_or("Invalid graph month")?
        }
        _ => return Err("Unsupported strategy timeframe".into()),
    };
    use chrono::TimeZone;
    let start = chrono_tz::America::New_York
        .from_local_datetime(&start.and_hms_opt(0, 0, 0).unwrap())
        .single()
        .ok_or("Ambiguous strategy graph boundary")?
        .to_utc();
    Ok((start, end))
}

/// Provides signal history for the strategy implementation.
pub async fn signal_history(
    backend: &Backend,
    registry: &Registry,
    symbol: &str,
    context: &BusinessContext,
    frame: &str,
    deadline: tokio::time::Instant,
) -> Result<Vec<Value>, String> {
    let parameter = "TomasAI.IFM.Domain.MarketData.Analytics.Shared.QueryParameters.GetFuturesItiSignalHistoryParameter";
    let period = registry.enum_value(
        "TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType",
        frame,
    )?;
    let subject = ActorSubject::query(
        "FuturesItiSignalQuery",
        "GetFuturesItiSignalHistory",
        &format!("{symbol}.{}.{frame}", context.value_date),
    );
    let fields = BTreeMap::from([
        ("Symbol".into(), symbol.into()),
        ("ValueDate".into(), date_only_wire(context.value_date)),
        ("TimePeriod".into(), period.into()),
    ]);
    let _ = registry.encode(parameter, &fields)?;
    let mut fields = fields;
    // The public managed query rebuilds its typed identity in the serialization
    // constructor. That parameter is not a declared IActorEntityId wire variant;
    // its union formatter writes nil. Scope comes from the explicit query slots.
    fields.insert("EntityId".into(), Value::Nil);
    fields.insert("Subject".into(), subject.wire());
    let body = registry.encode(
        "TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries.GetFuturesItiSignalHistoryQuery",
        &fields,
    )?;
    let reply = backend
        .request_until(&subject, &body, deadline, None)
        .await?;
    service_result(&reply)?
        .as_array()
        .filter(|v| v.len() <= 100_000)
        .cloned()
        .ok_or("Invalid or oversized ITI history".into())
}

/// Provides workflow page for the strategy implementation.
pub async fn workflow_page(
    backend: &Backend,
    registry: &Registry,
    symbol: &str,
    frame: &str,
    from: DateTime<Utc>,
    to: DateTime<Utc>,
    page: u32,
    deadline: tokio::time::Instant,
) -> Result<(Vec<GridWorkflow>, usize), String> {
    let (rows, total, _) = workflow_page_inner(backend, registry, symbol, frame, from, to, page, deadline, None).await?;
    Ok((rows, total))
}

pub async fn workflow_queue_page(
    backend: &Backend, registry: &Registry, symbol: &str, frame: &str,
    from: DateTime<Utc>, to: DateTime<Utc>, page: u32, deadline: tokio::time::Instant,
    scope: QueueScope<'_>,
) -> Result<(Vec<GridWorkflow>, bool), String> {
    let (rows, _, has_more) = workflow_page_inner(backend, registry, symbol, frame, from, to, page, deadline, Some(scope)).await?;
    Ok((rows, has_more))
}

async fn workflow_page_inner(
    backend: &Backend, registry: &Registry, symbol: &str, frame: &str,
    from: DateTime<Utc>, to: DateTime<Utc>, page: u32, deadline: tokio::time::Instant,
    scope: Option<QueueScope<'_>>,
) -> Result<(Vec<GridWorkflow>, usize, bool), String> {
    if page == 0 || page > 2000 {
        return Err("Invalid strategy history page".into());
    }
    let entity_key = format!(
        "{}.{frame}.{}.{}.{page}",
        scope.as_ref().map_or(symbol, |value| value.contract),
        from.format("%Y%m%d%H%M%S"),
        to.format("%Y%m%d%H%M%S")
    );
    let query = kind(
        "Queries",
        "GetIntrinsicTimeStrategyWorkflowHistoryPageQuery",
    );
    // The grid query shares the existing history request's numeric key layout.
    let subject = ActorSubject::query(
        "IntrinsicTimeStrategyWorkflowQuery",
        "GetStrategyUpdatesGrid",
        &entity_key,
    );
    let mut body = registry.encode(
        &query,
        &BTreeMap::from([
            ("Subject".into(), subject.wire()),
            (
                "EntityId".into(),
                Value::Array(vec![0.into(), Value::Array(vec![entity_key.into()])]),
            ),
            ("Symbol".into(), symbol.into()),
            (
                "TimePeriod".into(),
                registry
                    .enum_value(
                        "TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType",
                        frame,
                    )?
                    .into(),
            ),
            ("FromUtc".into(), timestamp_wire(from)),
            ("ToUtc".into(), timestamp_wire(to)),
            ("PageNumber".into(), page.into()),
            ("PageSize".into(), 50.into()),
        ]),
    )?;
    if let Some(scope) = &scope {
        let mut parts = body.as_array().cloned().ok_or("Invalid strategy request layout")?;
        parts.extend([scope.contract.into(), scope.assignment.into(), false.into()]);
        if let Some(before) = scope.before {
            parts.extend([timestamp_wire(before.history_order_at.unwrap_or(before.occurred)), before.signal_sequence.into(), before.row.id.as_str().into()]);
        }
        body = Value::Array(parts);
    }
    let reply = backend.request_until(&subject, &body, deadline, None).await?;
    let page_fields = service_result(&reply)?.as_array().ok_or("Invalid Strategy Updates page")?;
    let [items, page_number, page_size, total, ..] = page_fields.as_slice() else {
        return Err("Invalid Strategy Updates page layout".into());
    };
    let has_more = page_fields.get(6).and_then(Value::as_bool).unwrap_or(false);
    if page_number.as_u64() != Some(page.into()) || page_size.as_u64() != Some(50) {
        return Err("Strategy grid page metadata mismatch".into());
    }
    let total = total
        .as_u64()
        .filter(|value| *value <= 100_000)
        .ok_or("Invalid strategy grid total")? as usize;
    let items = items
        .as_array()
        .filter(|values| values.len() <= 50)
        .ok_or("Invalid strategy grid page")?;
    let mut seen = std::collections::BTreeSet::new();
    let mut rows = Vec::with_capacity(items.len());
    for item in items {
        let item = item.as_array().ok_or("Invalid Strategy Updates row")?;
        let [id, revision, occurred, status, outcome, signal, stages, ..] = item.as_slice() else {
            return Err("Invalid Strategy Updates row layout".into());
        };
        let signal_sequence = item.get(7).and_then(Value::as_i64).unwrap_or_default();
        let [id] = fields::<1>(id)?;
        let id = uuid::Uuid::parse_str(grid_text(id)?)
            .map_err(|_| "Invalid strategy grid workflow ID")?;
        if id.is_nil() || !seen.insert(id) {
            return Err("Invalid or duplicate strategy grid workflow ID".into());
        }
        let revision = revision
            .as_u64()
            .filter(|value| *value > 0)
            .ok_or("Invalid strategy grid workflow revision")?;
        let occurred = timestamp(occurred)?;
        let signal = FuturesItiSignalV2ReadModel::from_backend(signal)?;
        if let Some(scope) = &scope
            && signal.contract_id.as_deref() != Some(scope.contract)
        {
            return Err("Strategy Updates row has a different signal contract".into());
        }
        let stages = fields::<5>(stages)?
            .iter()
            .map(|stage| {
                let [
                    processing,
                    continuation,
                    completed,
                    failed,
                    error_type,
                    error_code,
                    message,
                    extra @ ..,
                ] = stage.as_array().map(Vec::as_slice)
                    .filter(|values| (7..=9).contains(&values.len()))
                    .ok_or("Invalid Strategy Updates stage wire layout")?
                else {
                    return Err("Invalid Strategy Updates stage wire layout".into());
                };
                let optional_time =
                    |value: &Value| (!value.is_nil()).then(|| timestamp(value)).transpose();
                Ok::<_, String>(StageHeader {
                    processing: grid_text(processing)?.into(),
                    continuation: grid_text(continuation)?.into(),
                    completed: optional_time(completed)?,
                    failed: optional_time(failed)?,
                    failure: message.as_str().map(|message| StageFailure {
                        error_type: error_type.as_str().unwrap_or_default().into(),
                        error_code: error_code.as_str().unwrap_or_default().into(),
                        error_message: message.into(),
                    }),
                    reason_sentence: extra.get(1).and_then(Value::as_str)
                        .unwrap_or_default().to_owned(),
                })
            })
            .collect::<Result<Vec<_>, _>>()?;
        let mut row = workflow_grid_row(
            &id.simple().to_string(),
            0,
            occurred,
            frame,
            grid_text(status)?,
            grid_text(outcome)?,
            &signal,
            &stages,
        )?;
        row.legacy_unassigned = item.get(8).and_then(Value::as_bool).unwrap_or(false);
        let history_order_at = item.get(9).filter(|value| !value.is_nil()).map(timestamp).transpose()?;
        rows.push(GridWorkflow {
            row,
            revision,
            occurred,
            signal_sequence,
            history_order_at,
        });
    }
    Ok((rows, total, has_more))
}

pub async fn queue_catalog(
    backend: &Backend, registry: &Registry, symbol: &str, deadline: tokio::time::Instant,
) -> Result<Option<(Vec<QueueAssignment>, Vec<String>)>, String> {
    let subject = ActorSubject::query("IntrinsicTimeStrategyWorkflowQuery", "GetStrategyUpdatesGrid", &format!("{symbol}.queues"));
    let query = kind("Queries", "GetIntrinsicTimeStrategyWorkflowHistoryPageQuery");
    let body = registry.encode(&query, &BTreeMap::from([
        ("Subject".into(), subject.wire()),
        ("EntityId".into(), Value::Array(vec![0.into(), Value::Array(vec![format!("{symbol}.queues").into()])])),
        ("Symbol".into(), symbol.into()),
        ("TimePeriod".into(), registry.enum_value("TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType", "Daily")?.into()),
        ("FromUtc".into(), timestamp_wire(Utc::now())),
        ("ToUtc".into(), timestamp_wire(Utc::now())),
        ("PageNumber".into(), 1.into()),
        ("PageSize".into(), 50.into()),
    ]))?;
    let mut parts = body.as_array().cloned().ok_or("Invalid strategy request layout")?;
    parts.extend([Value::Nil, Value::Nil, true.into()]);
    let reply = backend.request_until(&subject, &Value::Array(parts), deadline, None).await?;
    let page = service_result(&reply)?.as_array().ok_or("Invalid Strategy Updates catalog")?;
    let Some(assignments) = page.get(4).and_then(Value::as_array) else { return Ok(None); };
    let timeframe = "TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType";
    let names = ["Daily", "Weekly", "Monthly"].map(|name| {
        registry.enum_value(timeframe, name).map(|value| (value as i64, name))
    });
    let names = names.into_iter().collect::<Result<BTreeMap<_, _>, _>>()?;
    let assignments = assignments.iter().map(|value| {
        let key = assignment_key(value)?;
        let fields = value.as_array().unwrap();
        Ok::<_, String>(QueueAssignment {
            key, fund: grid_text(&fields[2])?.into(),
            family: grid_text(&fields[3])?.into(), root: grid_text(&fields[4])?.into(),
            frame: names.get(&fields[5].as_i64().ok_or("Invalid assignment timeframe")?)
                .ok_or("Unsupported assignment timeframe")?.to_string(),
        })
    }).collect::<Result<Vec<_>, _>>()?;
    let contracts = page.get(5).and_then(Value::as_array).ok_or("Missing Strategy Updates contracts")?
        .iter().map(|value| grid_text(value).map(str::to_owned)).collect::<Result<Vec<_>, _>>()?;
    Ok(Some((assignments, contracts)))
}

fn fields<const N: usize>(value: &Value) -> Result<&[Value; N], String> {
    value
        .as_array()
        .and_then(|items| items.as_slice().try_into().ok())
        .ok_or_else(|| "Invalid Strategy Updates wire layout".into())
}

fn grid_text(value: &Value) -> Result<&str, String> {
    value
        .as_str()
        .ok_or_else(|| "Invalid Strategy Updates text field".into())
}

/// Loads the selected execution and fences its identity and minimum revision.
pub async fn workflow_detail(
    backend: &Backend,
    registry: &Registry,
    id: &str,
    minimum_revision: u64,
) -> Result<WorkflowView, String> {
    let query = kind("Queries", "GetIntrinsicTimeStrategyWorkflowByIdQuery");
    let (actor, verb) = registry.route(&query)?;
    let subject = ActorSubject::query(actor, verb, id);
    let wire_id = uuid::Uuid::parse_str(id)
        .map_err(|_| "Invalid strategy detail workflow ID")?
        .hyphenated()
        .to_string();
    let body = registry.encode(
        &query,
        &BTreeMap::from([
            ("Subject".into(), subject.wire()),
            (
                "EntityId".into(),
                Value::Array(vec![0.into(), Value::Array(vec![id.into()])]),
            ),
            ("WorkflowId".into(), Value::Array(vec![wire_id.into()])),
            ("MinimumWorkflowRevision".into(), minimum_revision.into()),
        ]),
    )?;
    let reply = backend
        .request_until(
            &subject,
            &body,
            tokio::time::Instant::now() + backend.default_timeout(),
            None,
        )
        .await?;
    let model = kind("ViewModels", "IntrinsicTimeStrategyWorkflowReadModel");
    let payload = member(registry, &model, service_result(&reply)?, "StatePayload")?;
    let bytes = payload
        .as_slice()
        .filter(|v| v.len() <= 16 * 1024 * 1024)
        .ok_or("Invalid workflow state payload")?;
    let view = WorkflowView::from_backend(registry, &codec::decode(bytes)?)?;
    if view.header.id != id || view.header.revision < minimum_revision {
        return Err("Workflow detail identity or revision mismatch".into());
    }
    Ok(view)
}
