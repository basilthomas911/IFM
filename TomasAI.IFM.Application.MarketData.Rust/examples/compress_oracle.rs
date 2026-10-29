//! Reproducible streaming compression for immutable qualification oracle JSON.
//! This executable is a development tool and is never loaded by the datafeed.
use std::{fs::File, io};
fn main() -> io::Result<()> {
    let args = std::env::args().collect::<Vec<_>>();
    if args.len() != 3 {
        return Err(io::Error::new(
            io::ErrorKind::InvalidInput,
            "Expected input JSON and output Zstandard paths",
        ));
    }
    let input = File::open(&args[1])?;
    let mut output = File::create(&args[2])?;
    zstd::stream::copy_encode(input, &mut output, 9)
}
