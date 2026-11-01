"""Regression checks for the documentation/layout tool's syntax boundaries."""
import unittest
from rust_readability import PARSER, code_tokens, format_source, rustfmt


class ReadabilityTests(unittest.TestCase):
    """Keep layout and comment insertion separate from executable Rust tokens."""

    def check_format(self, source):
        """Check syntax, token preservation and repeated formatting together."""
        before = rustfmt(source.encode())
        after = format_source(before, "src/example.rs", preformatted=True).encode()
        self.assertFalse(PARSER.parse(after).root_node.has_error)
        self.assertEqual(code_tokens(before), code_tokens(after))
        self.assertEqual(after.decode(), format_source(after, "src/example.rs"))
        return after.decode()

    def test_spacing_before_attached_attributes_and_comments(self):
        """Separate definitions without detaching an existing comment/attribute."""
        output = self.check_format('fn first() {}\n/// Existing explanation.\n#[inline]\nfn second() {}')
        self.assertIn('}\n\n\n/// Existing explanation.\n#[inline]\nfn second', output)
        self.assertEqual(output.count('Existing explanation.'), 1)

    def test_strings_with_fake_declarations_are_not_annotated(self):
        """Rust-looking text inside raw/escaped strings remains literal data."""
        output = self.check_format('fn text() -> &\'static str { r###"fn fake() {} // struct Fake { x: i32 }"### }')
        self.assertEqual(output.count('///'), 1)
        self.assertIn('fn fake() {} // struct Fake { x: i32 }', output)

    def test_named_fields_keep_serde_attributes(self):
        """Place field documentation before attributes, preserving serde tokens."""
        output = self.check_format('#[derive(Debug)] struct Record { #[serde(default)] value: Option<i64> }')
        self.assertIn('/// Optional value;', output)
        self.assertIn('#[serde(default)]\n    value:', output)

    def test_tuple_fields_keep_visibility_types_and_arity(self):
        """Document each tuple field without changing its visibility or type."""
        output = self.check_format('pub struct Wrapper(pub String, i64);')
        self.assertIn('pub String,', output)
        self.assertIn('i64,', output)
        self.assertEqual(output.count('/// Wrapped'), 2)

    def test_trait_and_foreign_signatures_receive_comments(self):
        """Cover declarations without bodies as well as ordinary definitions."""
        output = self.check_format('trait Read { fn read(&self); } unsafe extern "C" { fn foreign(value: i32); }')
        self.assertIn('/// Reads', output)
        self.assertIn('fn foreign', output)

    def test_module_docs_and_existing_field_docs_are_retained(self):
        """Preserve existing documentation without duplicating field comments."""
        output = self.check_format('//! Module overview.\nstruct Data { /// Original field explanation.\nvalue: i32 }')
        self.assertTrue(output.startswith('//! Module overview.'))
        self.assertEqual(output.count('Original field explanation.'), 1)

    def test_trailing_comments_do_not_document_the_next_field(self):
        """Do not attach a prior field's trailing comment to a different field."""
        output = self.check_format('struct Data { first: i32, // First only.\nsecond: i32 }')
        self.assertIn('/// Second associated', output)

    def test_macro_tokens_are_preserved(self):
        """Leave macro syntax and embedded function-like token sequences intact."""
        self.check_format('macro_rules! make { ($name:ident) => { fn $name() {} }; } make!(example);')


if __name__ == "__main__":
    unittest.main()
