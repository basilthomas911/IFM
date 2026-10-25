//! Single-console queue batches are acknowledged only after the UI accepts them for rendering.
use crate::backend::health_http::HealthClient;
use std::time::Duration;
use std::sync::atomic::{AtomicBool, Ordering};

/// Shared by the worker publication and GUI. Dropping an unrendered batch never acknowledges it.
#[derive(Debug)]
pub struct ConsoleDelivery {
    receipts: Vec<String>,
    displayed: AtomicBool,
    rendered: tokio::sync::Notify,
}
impl ConsoleDelivery {
    pub fn new(receipts: Vec<String>) -> Self { Self { receipts, displayed: AtomicBool::new(false), rendered: tokio::sync::Notify::new() } }
    pub fn mark_displayed(&self) {
        if !self.displayed.swap(true, Ordering::AcqRel) { self.rendered.notify_one(); }
    }
    pub async fn wait_displayed(&self) { if !self.is_displayed() { self.rendered.notified().await; } }
    pub fn is_displayed(&self) -> bool { self.displayed.load(Ordering::Acquire) }
    pub fn receipts(&self) -> &[String] { &self.receipts }
}

/// Set up the durable push subscription once; no timer queries this endpoint.
pub async fn connect(client: &HealthClient, endpoint: Option<&str>, timeout: Duration) -> Result<(), String> {
    let mut url = reqwest::Url::parse(endpoint.ok_or("Console queue endpoint is not configured")?)
        .map_err(|_| "Invalid console queue endpoint")?;
    url.set_query(None);
    url.set_path(&format!("{}/connect", url.path().trim_end_matches('/')));
    client.post_json(url.as_str(), &serde_json::json!({}), tokio::time::Instant::now() + timeout).await
}

pub async fn acknowledge(client: &HealthClient, endpoint: Option<&str>, receipts: &[String], timeout: Duration) -> Result<(), String> {
    if receipts.is_empty() || receipts.len() > 100 { return Err("Invalid console acknowledgement batch".into()); }
    let mut url = reqwest::Url::parse(endpoint.ok_or("Console queue endpoint is not configured")?)
        .map_err(|_| "Invalid console queue endpoint")?;
    url.set_query(None);
    url.set_path(&format!("{}/ack", url.path().trim_end_matches('/')));
    client.post_json(url.as_str(), &serde_json::json!({"acknowledgements":receipts}),
        tokio::time::Instant::now() + timeout).await
}
