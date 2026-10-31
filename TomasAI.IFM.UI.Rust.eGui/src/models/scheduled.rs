//! Declared scheduler display documents. Execution and cron evaluation remain server operations.
use super::{labels::Label, wire_types::*};
use serde::{Deserialize, Serialize};
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Preview {
    pub valid: Option<bool>,
    pub errors: Option<Vec<String>>,
    pub next_fire_times_utc: Vec<String>,
}
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Project {
    pub task_key: Option<String>,
    pub name: Option<String>,
    pub available: Option<bool>,
    pub platform: Option<String>,
    pub manifest_version: Option<String>,
}
#[derive(Clone, Debug, Default, Serialize, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Run {
    pub id: String,
    pub status: Option<Label>,
    pub stage: Option<String>,
    pub detail: Option<String>,
    pub exit_code: Option<i32>,
    pub value_date: Option<DateText>,
    pub revision: Option<i64>,
    pub operation_command_id: Option<String>,
    pub process_id: Option<i32>,
    pub standard_output_tail: Option<String>,
    pub standard_error_tail: Option<String>,
    pub output_directory: Option<String>,
    pub intended_fire_time_utc: Option<String>,
    pub started_at_utc: Option<String>,
    pub finished_at_utc: Option<String>,
}
