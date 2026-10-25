"""Reject incomplete/mismatching desktop evidence; these are tool tests, not UI acceptance."""
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


class EvidenceGate(unittest.TestCase):
    def compare(self, missing_capture=False, changed_value=False, required_value=None):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            row = {"type": "Edit", "name": "Quantity", "enabled": True,
                   "value": "1", "x": 10, "y": 20, "width": 80, "height": 25}
            for variant in ("Hybrid", "Standalone"):
                folder = root / variant
                folder.mkdir()
                value = dict(row)
                if changed_value and variant == "Standalone":
                    value["value"] = "2"
                (folder / "screen.json").write_text(json.dumps([value]), encoding="utf-8")
            captures = ["screen", "missing"] if missing_capture else ["screen"]
            spec = root / "spec.json"
            comparison={"captures":captures}
            if required_value is not None:comparison["requiredValues"]={"screen":[required_value]}
            spec.write_text(json.dumps({"comparison": comparison}), encoding="utf-8")
            result = subprocess.run([sys.executable, str(Path(__file__).with_name("compare_desktop.py")),
                                     str(root), str(spec)], capture_output=True, text=True)
            return result.returncode, json.loads((root / "comparison.json").read_text())

    def test_identical_complete_evidence_passes(self):
        code, report = self.compare()
        self.assertEqual((code, report["status"]), (0, "Passed"))

    def test_a_missing_required_capture_cannot_pass(self):
        code, report = self.compare(missing_capture=True)
        self.assertNotEqual(code, 0)
        self.assertEqual(len(report["missingCaptures"]), 2)

    def test_changed_business_value_cannot_pass(self):
        code, report = self.compare(changed_value=True)
        self.assertNotEqual(code, 0)
        self.assertTrue(report["cases"][0]["missing"])

    def test_identical_empty_results_cannot_replace_required_business_values(self):
        code,report=self.compare(required_value="99")
        self.assertNotEqual(code,0)
        self.assertEqual(report["cases"][0]["missingRequiredValues"],{"Hybrid":["99"],"Standalone":["99"]})


if __name__ == "__main__":
    unittest.main()
