use crate::backend::{contracts::*, transport::Backend};
use rmpv::Value;

#[derive(Debug)]
pub struct FeedStatus {
    pub running: bool,
    /// Active trading date reported by the backend, never a local date.
    pub active_date: Option<chrono::NaiveDate>,
    pub observed_at: chrono::DateTime<chrono::Utc>,
    pub terminal_error: Option<String>,
    pub recovering: bool,
}
impl FeedStatus {
    pub async fn load(backend: &Backend) -> Result<Self, String> {
        let subject =
            ActorSubject::query("MarketDataFeedQuery", "GetRuntimeStatus", "runtime-status");
        let result = backend
            .request(
                &subject,
                &Value::Array(vec![subject.wire(), Value::Nil]),
                None,
                None,
            )
            .await?;
        Self::from_wire(service_result(&result)?)
    }
    pub fn from_wire(value: &Value) -> Result<Self, String> {
        let values = value.as_array().ok_or("Invalid feed runtime status")?;
        if values.len() < 13 {
            return Err("Incomplete feed runtime status".into());
        }
        let running = values[0].as_bool().ok_or("Invalid feed running state")?;
        let active_date = if values[1].is_nil() {
            None
        } else {
            Some(date_only(&values[1])?)
        };
        if running != active_date.is_some() {
            return Err("Invalid feed active value date".into());
        }
        let observed_at = datetime_offset(&values[2])?;
        let deadline = if values[12].is_nil() {
            None
        } else {
            Some(datetime_offset(&values[12])?)
        };
        let recovering = deadline.is_some_and(|deadline| observed_at < deadline);
        let terminal_error = if recovering {
            None
        } else {
            [3, 9]
                .into_iter()
                .filter_map(|index| values[index].as_str())
                .find(|message| !message.is_empty())
                .map(str::to_owned)
        };
        Ok(Self {
            running,
            active_date: active_date,
            observed_at,
            terminal_error,
            recovering,
        })
    }
}
