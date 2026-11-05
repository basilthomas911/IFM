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
pub struct GridWorkflow {
    pub row: WorkflowRow,
    pub revision: u64,
    pub occurred: DateTime<Utc>,
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
    if page == 0 || page > 2000 {
        return Err("Invalid strategy history page".into());
    }
    let entity_key = format!(
        "{symbol}.{frame}.{}.{}.{page}",
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
    let body = registry.encode(
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
    let reply = backend
        .request_until(&subject, &body, deadline, None)
        .await?;
    let page_fields = fields::<4>(service_result(&reply)?)?;
    let [items, page_number, page_size, total] = page_fields;
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
        let [id, revision, occurred, status, outcome, signal, stages] = fields::<7>(item)?;
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
                ] = fields::<7>(stage)?;
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
                })
            })
            .collect::<Result<Vec<_>, _>>()?;
        let row = workflow_grid_row(
            &id.simple().to_string(),
            0,
            occurred,
            frame,
            grid_text(status)?,
            grid_text(outcome)?,
            &signal,
            &stages,
        )?;
        rows.push(GridWorkflow {
            row,
            revision,
            occurred,
        });
    }
    Ok((rows, total))
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
