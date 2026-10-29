//! Push decoders cannot retain untrusted huge metadata. Public pull decoders
//! stream large metadata; low-level push entry points reject before growing.
use databento::dbn::decode::dbn::fsm::{DbnFsm, ProcessResult};
#[test]
fn push_fsm_rejects_large_wire_length_without_buffer_growth() {
    for length in [65529_u32, 1024 * 1024, u32::MAX] {
        let mut decoder = DbnFsm::new(65536, 0);
        let mut header = b"DBN\x03".to_vec();
        header.extend_from_slice(&length.to_le_bytes());
        decoder.write_all(&header);
        assert!(matches!(decoder.process(), ProcessResult::Err(_)));
        assert!(decoder.space().len() <= 65536);
    }
}

#[tokio::test]
async fn push_fsm_counts_cannot_read_beyond_declared_metadata_into_records() {
    use databento::dbn::{MetadataBuilder, SType, Schema, encode::dbn::AsyncMetadataEncoder};
    let m = MetadataBuilder::new()
        .dataset("GLBX.MDP3")
        .schema(Some(Schema::Trades))
        .stype_in(None)
        .stype_out(SType::InstrumentId)
        .start(0)
        .build();
    let mut valid = Vec::new();
    AsyncMetadataEncoder::new(&mut valid)
        .encode(&m)
        .await
        .unwrap();
    for (offset, bytes) in [
        (4, 100_u32.to_le_bytes().to_vec()),
        (53, vec![0, 0]),
        (112, u32::MAX.to_le_bytes().to_vec()),
        (116, u32::MAX.to_le_bytes().to_vec()),
        (120, u32::MAX.to_le_bytes().to_vec()),
        (124, 1_u32.to_le_bytes().to_vec()),
    ] {
        let mut input = valid.clone();
        input[offset..offset + bytes.len()].copy_from_slice(&bytes);
        input.extend_from_slice(&[0; 1024]); // These bytes must not satisfy metadata counts.
        let mut decoder = DbnFsm::new(65536, 0);
        decoder.write_all(&input);
        assert!(
            matches!(decoder.process(), ProcessResult::Err(_)),
            "offset={offset}"
        );
    }
}
