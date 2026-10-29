//! Recovery facts are values, never dataset resources. Capture uses bounded try-send;
//! disk and acknowledgement delivery run in independent root services.
pub mod journal;
pub mod outbox;
pub mod protocol;

#[cfg(test)]
mod tests;
