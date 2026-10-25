//! Paged console replay uses the API's sequence watermark, never a UI clock.
use crate::backend::health_http::HealthClient;
use chrono::{DateTime, Utc};
use std::time::Duration;

pub struct Message {
    pub id: String,
    pub date: DateTime<Utc>,
    pub message: String,
}
pub struct Page {
    pub next: u64,
    pub through: u64,
    pub more: bool,
    pub messages: Vec<Message>,
}

pub async fn read_page(
    client: &HealthClient,
    endpoint: Option<&str>,
    after: u64,
    through: Option<u64>,
    timeout: Duration,
) -> Result<Page, String> {
    let mut url =
        reqwest::Url::parse(endpoint.ok_or("Console history endpoint is not configured")?)
            .map_err(|_| "Invalid console history endpoint")?;
    url.set_query(None);
    url.query_pairs_mut()
        .append_pair("after", &after.to_string())
        .append_pair("limit", "100");
    if let Some(through) = through {
        url.query_pairs_mut()
            .append_pair("through", &through.to_string());
    }
    let value = client
        .get(
            url.as_str(),
            8 * 1024 * 1024,
            tokio::time::Instant::now() + timeout,
        )
        .await?;
    let next = value["nextSequence"]
        .as_u64()
        .ok_or("Missing console history cursor")?;
    let end = value["throughSequence"]
        .as_u64()
        .ok_or("Missing console history watermark")?;
    let more = value["hasMore"]
        .as_bool()
        .ok_or("Missing console history completion")?;
    if next < after
        || next > end
        || through.is_some_and(|through| end != through)
        || more != (next < end)
        || (more && next == after)
    {
        return Err("Console history cursor did not advance consistently".into());
    }
    let rows = value["messages"]
        .as_array()
        .ok_or("Missing console history messages")?;
    if rows.len() > 100 {
        return Err("Console history page exceeds its bound".into());
    }
    let mut messages = Vec::with_capacity(rows.len());
    let mut prior = after;
    for row in rows {
        let sequence = row["sequence"]
            .as_u64()
            .ok_or("Missing console message sequence")?;
        if sequence != prior.saturating_add(1) || sequence > next {
            return Err("Invalid console message sequence".into());
        }
        prior = sequence;
        let id = row["id"]
            .as_str()
            .and_then(|id| uuid::Uuid::parse_str(id).ok())
            .filter(|id| !id.is_nil())
            .ok_or("Invalid console message identity")?
            .to_string();
        let date = DateTime::parse_from_rfc3339(
            row["statusDate"]
                .as_str()
                .ok_or("Missing console message time")?,
        )
        .map_err(|_| "Invalid console message time")?
        .to_utc();
        let message = row["message"]
            .as_str()
            .ok_or("Missing console message text")?
            .to_owned();
        messages.push(Message { id, date, message });
    }
    if next > after && prior != next {
        return Err("Incomplete console history page".into());
    }
    Ok(Page {
        next,
        through: end,
        more,
        messages,
    })
}
