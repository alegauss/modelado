"""build_manifest.py turns meta.json folders into a manifest and flat files to upload."""

from __future__ import annotations

import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import build_manifest as bm  # noqa: E402


def write_model(models: Path, name: str, variants: dict[str, bytes], task: str = "depth") -> None:
    folder = models / name
    folder.mkdir(parents=True)
    described = {}
    for variant, payload in variants.items():
        (folder / f"{variant}.onnx").write_bytes(payload)
        described[variant] = {"file": f"{variant}.onnx", "bytes": len(payload), "sha256": hashlib.sha256(payload).hexdigest()}
    meta = {"name": name, "task": task, "variants": described, "upstream": {"license": "MIT", "license_url": "https://example.org/LICENSE"}}
    (folder / "meta.json").write_text(json.dumps(meta), encoding="utf-8")


class BuildManifestTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        self.models, self.stage = root / "models", root / "stage"
        write_model(self.models, "tiny", {"fp32": b"fp32 bytes", "fp16": b"fp16"})

    def test_manifest_lists_urls_sizes_and_hashes_per_variant(self):
        manifest = bm.build(self.models, self.stage, "https://host/models/", "1")
        model = manifest["models"][0]
        self.assertEqual("https://host/models/tiny-fp32.onnx", model["variants"]["fp32"]["url"])
        self.assertEqual("https://host/models/tiny-fp16.onnx", model["variants"]["fp16"]["url"])
        self.assertEqual(hashlib.sha256(b"fp16").hexdigest(), model["variants"]["fp16"]["sha256"])
        self.assertEqual("https://host/models/tiny-meta.json", model["meta"]["url"])
        self.assertEqual("depth", model["task"])
        self.assertEqual(manifest, json.loads((self.stage / "manifest.json").read_text(encoding="utf-8")))

    def test_files_are_staged_under_flat_names(self):
        bm.build(self.models, self.stage, "https://host", "1")
        self.assertEqual(b"fp32 bytes", (self.stage / "tiny-fp32.onnx").read_bytes())
        self.assertTrue((self.stage / "tiny-meta.json").is_file())

    def test_variants_can_be_narrowed(self):
        manifest = bm.build(self.models, self.stage, "https://host", "1", variants_wanted={"fp32"})
        self.assertEqual(["fp32"], list(manifest["models"][0]["variants"]))
        self.assertFalse((self.stage / "tiny-fp16.onnx").exists())

    def test_a_file_that_differs_from_meta_json_is_refused(self):
        (self.models / "tiny" / "fp32.onnx").write_bytes(b"changed!!!")
        with self.assertRaises(bm.ManifestError):
            bm.build(self.models, self.stage, "https://host", "1")

    def test_a_missing_model_is_named(self):
        with self.assertRaisesRegex(bm.ManifestError, "absent"):
            bm.build(self.models, self.stage, "https://host", "1", only=["tiny", "absent"])

    def test_no_models_is_refused(self):
        with self.assertRaises(bm.ManifestError):
            bm.build(self.stage, self.stage, "https://host", "1")


if __name__ == "__main__":
    unittest.main()
