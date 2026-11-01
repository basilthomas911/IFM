//! Error dialogs retain distinct failures until acknowledged. Replayed request
//! outcomes cannot reopen a dismissed dialog.
use crate::lookup_editor::EditorOutcome;
use std::collections::VecDeque;

/// Error Dialogs used by the errors component.
#[derive(Default)]
pub struct ErrorDialogs {
    /// Pending associated with this error dialogs.
    pending: VecDeque<String>,
    /// Seen associated with this error dialogs.
    seen: VecDeque<String>,
}
impl ErrorDialogs {
    /// Provides context for the error dialogs implementation.
    pub fn context(&mut self, view: &str, context: &std::collections::BTreeMap<String, String>) {
        for (key, message) in context {
            if key.ends_with("Error") || key == "error" {
                self.report(format!("{view}:{key}:{message}"), message.clone());
            }
        }
    }


    /// Provides report for the error dialogs implementation.
    pub fn report(&mut self, identity: String, message: String) {
        if message.is_empty() || self.seen.contains(&identity) {
            return;
        }
        if self.seen.len() == 1024 {
            self.seen.pop_front();
        }
        self.seen.push_back(identity);
        self.pending.push_back(message);
    }


    /// Provides outcomes for the error dialogs implementation.
    pub fn outcomes(&mut self, view: &str, outcomes: &[EditorOutcome]) {
        for outcome in outcomes {
            if matches!(outcome.state.as_str(), "Failed" | "Unknown" | "Rejected") {
                self.report(
                    format!("{view}:{}:{}", outcome.request_id, outcome.state),
                    if outcome.message.is_empty() {
                        format!("{view} request {}", outcome.state.to_lowercase())
                    } else {
                        outcome.message.clone()
                    },
                );
            }
        }
    }


    /// Provides message for the error dialogs implementation.
    pub fn message(&self) -> Option<&str> {
        self.pending.front().map(String::as_str)
    }


    /// Provides acknowledge for the error dialogs implementation.
    pub fn acknowledge(&mut self) {
        self.pending.pop_front();
    }
}

#[cfg(test)]
mod tests {
    use super::*;


    /// Verifies that failures are retained and replayed receipts do not reopen them.
    #[test]
    fn failures_are_retained_and_replayed_receipts_do_not_reopen_them() {
        let mut dialogs = ErrorDialogs::default();
        let outcomes: Vec<EditorOutcome> = serde_json::from_value(serde_json::json!([
            {"requestId":"one","state":"Failed","message":"Emulator unavailable"},
            {"requestId":"two","state":"Unknown","message":"Reconcile the order"},
            {"requestId":"three","state":"Completed","message":"Saved"}
        ]))
        .unwrap();
        dialogs.outcomes("trade-orders", &outcomes);
        assert_eq!(dialogs.message(), Some("Emulator unavailable"));
        dialogs.acknowledge();
        dialogs.outcomes("trade-orders", &outcomes);
        assert_eq!(dialogs.message(), Some("Reconcile the order"));
        dialogs.acknowledge();
        assert_eq!(dialogs.message(), None);
    }
}
