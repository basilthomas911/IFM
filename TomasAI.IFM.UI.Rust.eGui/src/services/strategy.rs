//! Read-only strategy history boundary. A historical item is hydrated from the
//! backend's complete state payload with identity/revision checks before use.
use crate::backend::{codec, contracts::*, schema::Registry, transport::Backend};
use crate::services::business_context::BusinessContext;
use chrono::{DateTime, Datelike, NaiveDate, Utc};
use rmpv::Value;
use std::collections::BTreeMap;
pub const ROOT: &str = "TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime";
pub fn kind(section: &str, name: &str) -> String {
    format!("{ROOT}.{section}.{name}")
}
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
pub async fn workflow_page(
    backend: &Backend,
    registry: &Registry,
    symbol: &str,
    frame: &str,
    from: DateTime<Utc>,
    to: DateTime<Utc>,
    page: u32,
    deadline: tokio::time::Instant,
) -> Result<(Vec<Value>, usize), String> {
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
    let (actor, verb) = registry.route(&query)?;
    let subject = ActorSubject::query(actor, verb, &entity_key);
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
    let value = service_result(&reply)?;
    let model = kind(
        "ViewModels",
        "IntrinsicTimeStrategyWorkflowHistoryPageReadModel",
    );
    let row = registry.project(&model, value)?;
    if row["PageNumber"] != page || row["PageSize"] != 50 {
        return Err("Strategy history page metadata mismatch".into());
    }
    let total = row["TotalCount"]
        .as_u64()
        .filter(|v| *v <= 100_000)
        .ok_or("Invalid strategy history total")? as usize;
    let items = member(registry, &model, value, "Items")?;
    let items = items
        .as_array()
        .filter(|v| v.len() <= 50)
        .ok_or("Invalid strategy history page")?;
    let histories = items
        .iter()
        .map(|v| {
            registry.project(
                &kind(
                    "ViewModels",
                    "IntrinsicTimeStrategyWorkflowHistoryReadModel",
                ),
                v,
            )
        })
        .collect::<Result<Vec<_>, _>>()?;
    if histories.is_empty() {
        return Ok((vec![], total));
    }
    let mut ids = vec![];
    let mut revisions = vec![];
    let mut seen = std::collections::BTreeSet::new();
    for row in &histories {
        let id = row["WorkflowId"]["Value"]
            .as_str()
            .ok_or("Missing workflow history identity")?;
        if !seen.insert(id.to_string()) {
            return Err("Duplicate strategy history identity".into());
        }
        let revision = row["WorkflowRevision"]
            .as_i64()
            .filter(|v| *v > 0)
            .ok_or("Invalid workflow history revision")?;
        ids.push(Value::Array(vec![id.into()]));
        revisions.push(revision.into());
    }
    let query = kind("Queries", "GetIntrinsicTimeStrategyWorkflowsByIdsQuery");
    let (actor, verb) = registry.route(&query)?;
    let key = uuid::Uuid::new_v4().simple().to_string();
    let subject = ActorSubject::query(actor, verb, &key);
    let body = registry.encode(
        &query,
        &BTreeMap::from([
            ("Subject".into(), subject.wire()),
            (
                "EntityId".into(),
                Value::Array(vec![0.into(), Value::Array(vec![key.into()])]),
            ),
            ("WorkflowIds".into(), Value::Array(ids)),
            ("MinimumRevisions".into(), Value::Array(revisions)),
        ]),
    )?;
    let reply = backend
        .request_until(&subject, &body, deadline, None)
        .await?;
    let rows = service_result(&reply)?
        .as_array()
        .filter(|v| v.len() == histories.len())
        .ok_or("Workflow detail batch length differs from request")?;
    let mut views = vec![];
    for (wire, history) in rows.iter().zip(histories) {
        let kind = kind("ViewModels", "IntrinsicTimeStrategyWorkflowReadModel");
        let row = registry.project(&kind, wire)?;
        if row["WorkflowId"] != history["WorkflowId"]
            || row["WorkflowRevision"].as_i64() < history["WorkflowRevision"].as_i64()
        {
            return Err("Workflow batch identity or revision mismatch".into());
        }
        let bytes = member(registry, &kind, wire, "StatePayload")?;
        let bytes = bytes
            .as_slice()
            .filter(|v| v.len() <= 16 * 1024 * 1024)
            .ok_or("Invalid workflow state payload")?;
        let view = codec::decode(bytes)?;
        let projected = registry.project(crate::services::strategy_display::VIEW, &view)?;
        if projected["WorkflowId"] != row["WorkflowId"]
            || projected["WorkflowRevision"] != row["WorkflowRevision"]
        {
            return Err("Workflow payload identity or revision mismatch".into());
        }
        views.push(view);
    }
    Ok((views, total))
}
