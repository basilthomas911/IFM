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
        // The outcome and its inline context can report the same failure in one publication.
        // Retain the first dialog and remember both identities so refreshes cannot reopen it.
        let reason = |text: &str| text.strip_prefix("Portfolio declined the request: ").unwrap_or(text).to_owned();
        if self.pending.iter().any(|pending| reason(pending) == reason(&message)) {
            return;
        }
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

    /// A failed placement is reported once even when its outcome is also published inline.
    #[test]
    fn placement_outcome_and_inline_error_share_the_first_dialog() {
        let mut dialogs = ErrorDialogs::default();
        let message = "Portfolio declined the request: Backend error 34131: Close declined.";
        let outcomes: Vec<EditorOutcome> = serde_json::from_value(serde_json::json!([
            {"requestId":"placement","state":"Failed","message":message}
        ])).unwrap();
        let context = [("submissionError".into(), message.into())].into_iter().collect();
        dialogs.outcomes("trade-orders", &outcomes);
        dialogs.context("trade-orders", &context);
        assert_eq!(dialogs.message(), Some(message));
        dialogs.acknowledge();
        assert_eq!(dialogs.message(), None);
        dialogs.context("trade-orders", &context);
        dialogs.outcomes("trade-orders", &outcomes);
        assert_eq!(dialogs.message(), None);
        dialogs.report("new-placement".into(), message.into());
        assert_eq!(dialogs.message(), Some(message));
    }

    /// A controller wrapper must not queue a second dialog behind the original backend rejection.
    #[test]
    fn backend_rejection_is_kept_when_the_controller_adds_context() {
        let mut dialogs = ErrorDialogs::default();
        let original = "Backend error 34131: Close declined.";
        dialogs.report("backend".into(), original.into());
        dialogs.report("controller".into(), format!("Portfolio declined the request: {original}"));
        assert_eq!(dialogs.message(), Some(original));
        dialogs.acknowledge();
        assert_eq!(dialogs.message(), None);
    }


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
