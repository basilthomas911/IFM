//! MessagePack encoding and versioned publication transport. These modules contain no UI rendering or domain calculations.

/// Sequence-aware reconstruction of ABI v5 MessagePack publications.
pub mod publication_transfer;
/// Shared MessagePack serialization and numeric-display compatibility.
pub mod wire;
