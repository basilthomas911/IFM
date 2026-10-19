//! Native executable support. The runtime loader stays private to the binary; reusable view code lives in the library.

/// In-process .NET 10 hosting and ABI lifetime management.
pub(crate) mod hosting;
