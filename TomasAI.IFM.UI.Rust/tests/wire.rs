use ifm_ui_rust::wire;
use serde::Deserialize;

#[test]
fn intents_use_native_numbers_and_preserve_exact_ids_and_text() {
    #[derive(Deserialize)]
    #[serde(rename_all = "camelCase")]
    struct Intent { schema: u32, expected_generation: u64, request_id: String, fields: std::collections::BTreeMap<String,String> }
    let bytes = wire::encode_intent(r#"{"schema":1,"expectedGeneration":18446744073709551615,"requestId":"01234567-89ab-cdef-0123-456789abcdef","fields":{"amount":"123456789012345.6789","message":"ES / \u03b4 / \u65e5\u672c","empty":""}}"#).unwrap();
    assert!((0x80..=0x8f).contains(&bytes[0]));
    let result: Intent = wire::decode(&bytes).unwrap();
    assert_eq!(result.schema, 1);
    assert_eq!(result.expected_generation, u64::MAX);
    assert_eq!(result.request_id,"01234567-89ab-cdef-0123-456789abcdef");
    assert_eq!(result.fields["amount"],"123456789012345.6789");
    assert_eq!(result.fields["message"],"ES / \u{03b4} / \u{65e5}\u{672c}");
    assert!(wire::decode::<Intent>(&bytes[..bytes.len()-1]).is_err());
}

#[test]
fn malformed_documents_are_not_submitted() {
    assert!(wire::encode_intent("not an intent").is_err());
    assert!(wire::decode::<serde_json::Value>(&[0xc1]).is_err());
}

#[test]
fn nested_documents_are_messagepack_binary_with_exact_decimal_text() {
    #[derive(Deserialize)]
    struct Intent { documents: std::collections::BTreeMap<String,serde_bytes::ByteBuf> }
    let bytes=wire::encode_intent(r#"{"documents":{"changes":[{"Path":"/Risk/Limit","Value":"12345678901234567890.123456789","Kind":4}]}}"#).unwrap();
    let intent:Intent=wire::decode(&bytes).unwrap();
    let changes:serde_json::Value=wire::decode(&intent.documents["changes"]).unwrap();
    assert_eq!(changes[0]["Value"],"12345678901234567890.123456789");
    assert_eq!(changes[0]["Kind"],4);
}
