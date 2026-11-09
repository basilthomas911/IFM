//! Front/back VX history from the server's active rollover catalogue.
use super::*;
use crate::models::trade_contracts::FuturesContractV3ReadModel;


/// Selects the authoritative front and the next active expiry, independent of row order.
pub fn contracts(source: &[FuturesContractV3ReadModel]) -> Result<(String, Option<String>), String> {
    let active: Vec<_> = source.iter().filter(|row| row.symbol.as_deref() == Some("VX")
        && row.rollover == Some(true)).collect();
    let fronts: Vec<_> = active.iter().filter(|row| row.on_the_run == Some(true)).collect();
    if fronts.len() != 1 { return Err("VX front-month assignment is missing or ambiguous".into()); }
    let front = fronts[0];
    let front_id = front.contract_id.as_deref().filter(|id| !id.is_empty()).ok_or("VX front contract unavailable")?;
    let expiry = day(&front.cell("LastTradeDate"))?;
    let mut backs = Vec::new();
    for row in active.iter().filter(|row| row.on_the_run == Some(false)) {
        let date = day(&row.cell("LastTradeDate"))?;
        if date > expiry && let Some(id) = row.contract_id.as_deref().filter(|id| !id.is_empty() && *id != front_id) {
            backs.push((date, id));
        }
    }
    backs.sort_unstable();
    Ok((front_id.into(), backs.first().map(|(_, id)| (*id).into())))
}


/// Combines independently loaded legs; a missing leg cannot hide its healthy sibling.
pub fn chart(
    start: DateTime<Utc>, end: DateTime<Utc>,
    front: (&str, Result<ChartSnapshot, String>),
    back: (&str, Result<ChartSnapshot, String>),
) -> ChartSnapshot {
    let mut result = ChartSnapshot { start: market_seconds(start), end: market_seconds(end), ..Default::default() };
    let mut messages = Vec::new();
    for ((contract, value), role, color) in [(front, "Front month", "Fuchsia"), (back, "Back month", "LightBlue")] {
        match value {
            Ok(mut snapshot) => {
                if !snapshot.status.is_empty() { messages.push(format!("{role}: {}", snapshot.status)); }
                if let Some(low) = snapshot.minimum { result.minimum = Some(result.minimum.map_or(low, |old| old.min(low))); }
                if let Some(high) = snapshot.maximum { result.maximum = Some(result.maximum.map_or(high, |old| old.max(high))); }
                if let Some(mut line) = snapshot.series.drain(..).next() {
                    line.name = format!("{role} · {contract}");
                    line.color = color.into();
                    result.series.push(line);
                }
            }
            Err(error) => messages.push(format!("{role}: {error}")),
        }
    }
    result.status = messages.join("; ");
    result
}


/// Loads both assigned months concurrently under one bounded refresh budget.
pub(super) async fn load(backend: &Backend, registry: &Registry, context: &BusinessContext, deadline: tokio::time::Instant)
    -> Result<ChartSnapshot, String> {
    let wire = tokio::time::timeout_at(deadline, actor_requests::query(backend, registry,
        "TomasAI.IFM.Domain.MarketData.Shared.Queries.GetRolloverFuturesContractsQuery", "VX",
        BTreeMap::from([("Symbol".into(), "VX".into())])))
        .await.map_err(|_| "VX contract lookup deadline expired")??;
    let (front, back) = contracts(&Vec::<FuturesContractV3ReadModel>::from_backend(&wire)?)?;
    let read = |contract| async move {
        tokio::time::timeout_at(deadline, load_contract_bars(backend, registry, context, "VX", contract))
            .await.unwrap_or_else(|_| Err("History refresh deadline expired".into()))
    };
    let (front_result, back_result) = tokio::join!(read(front.as_str()), async {
        match back.as_deref() { Some(back) => read(back).await, None => Err("No active back-month assignment".into()) }
    });
    Ok(chart(context.as_of_utc - chrono::Duration::hours(6), context.as_of_utc,
        (&front, front_result), (back.as_deref().unwrap_or(""), back_result)))
}
