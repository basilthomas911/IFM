//! Main-window layout and sidebar views. The shell composes feature renderers and returns user actions to the host.

/// Three-pane application shell and published dashboard model.
pub mod shell;
/// Economic calendar, status console, and feed-control publications. Rows retain stable selection identities, keyboard navigation stays within bounds, and new backend errors are surfaced once per error sequence.
pub mod sidebar;
