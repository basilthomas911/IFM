import importlib.util
import struct
import unittest
from pathlib import Path
spec = importlib.util.spec_from_file_location("repair", Path(__file__).with_name("repair-development-option-tick-prices.py"))
repair = importlib.util.module_from_spec(spec)
spec.loader.exec_module(repair)

class PriceRepairTests(unittest.TestCase):
    def test_exact_fixture_double_bytes(self):
        expected = dict(optionprice=100.5, bidprice=100.0, askprice=101.0, underlyingprice=150.0)
        for name, value in expected.items():
            self.assertEqual(value, repair.decode_price(bytes.fromhex(repair.FIXTURE_BYTES[name]), name, repair.FIXTURE_KEY))

    def test_real_decimal_and_negative_decimal(self):
        self.assertEqual(100.5, repair.decode_price(struct.pack(">ii", 1, 1005), "optionprice", ("other", "2026-10-10", 1)))
        self.assertEqual(-12.34, repair.decode_price(struct.pack(">ii", 2, -1234), "optionprice", ("other", "2026-10-10", 1)))

    def test_unknown_double_bytes_are_not_guessed(self):
        with self.assertRaises(ValueError):
            repair.decode_price(bytes.fromhex(repair.FIXTURE_BYTES["optionprice"]), "optionprice", ("other", "2026-10-10", 1))

    def test_invalid_and_null(self):
        self.assertIsNone(repair.decode_price(None, "optionprice", repair.FIXTURE_KEY))
        with self.assertRaises(ValueError):
            repair.decode_price(b"abc", "optionprice", repair.FIXTURE_KEY)

if __name__ == "__main__":
    unittest.main()
