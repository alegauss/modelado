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


def write_model(models: Path, name: str, variants: dict[str, bytes], task: str = "depth", license_id: str = "MIT") -> None:
    folder = models / name
    folder.mkdir(parents=True)
    described = {}
    for variant, payload in variants.items():
        (folder / f"{variant}.onnx").write_bytes(payload)
        described[variant] = {"file": f"{variant}.onnx", "bytes": len(payload), "sha256": hashlib.sha256(payload).hexdigest()}
    meta = {"name": name, "task": task, "variants": described, "upstream": {"license": license_id, "license_url": "https://example.org/LICENSE"}}
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


class LicenseGateTests(unittest.TestCase):
    """MOD25: a license a public repository may not carry is refused before anything is staged."""

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        self.models, self.stage = root / "models", root / "stage"
        write_model(self.models, "tiny", {"fp32": b"fp32"})
        write_model(self.models, "depth-anything-v2-base", {"fp32": b"base"}, license_id="CC-BY-NC-4.0")

    def test_a_non_commercial_license_is_refused_with_the_model_and_its_license(self):
        with self.assertRaisesRegex(bm.ManifestError, "depth-anything-v2-base: its license, CC-BY-NC-4.0, .*--allow-license"):
            bm.build(self.models, self.stage, "https://host", "1")
        self.assertFalse((self.stage / "tiny-fp32.onnx").exists())

    def test_allowed_by_hand_it_is_staged(self):
        manifest = bm.build(self.models, self.stage, "https://host", "1", allow={"CC-BY-NC-4.0": "the author granted it in writing"})
        self.assertEqual(["depth-anything-v2-base", "tiny"], [m["name"] for m in manifest["models"]])

    def test_every_built_in_license_passes(self):
        manifest = json.loads((bm.REPO / "src" / "Modelado.Inference" / "models-manifest.json").read_text(encoding="utf-8"))
        for model in manifest["models"]:
            with self.subTest(model=model["name"]):
                self.assertTrue(bm.redistributable(model["license"], set(bm.REDISTRIBUTABLE)))

    def test_every_part_of_a_line_must_pass_and_either_side_of_an_or(self):
        allowed = set(bm.REDISTRIBUTABLE)
        self.assertTrue(bm.redistributable("Apache-2.0 (U-2-Net weights), MIT (rembg export)", allowed))
        self.assertFalse(bm.redistributable("MIT (code), CC-BY-NC-4.0 (weights)", allowed))
        self.assertTrue(bm.redistributable("GPL-3.0 OR MIT", allowed))
        self.assertFalse(bm.redistributable("MIT AND GPL-3.0", allowed))
        self.assertFalse(bm.redistributable("", allowed))

    def test_the_cli_takes_a_license_only_with_a_reason(self):
        import io
        from contextlib import redirect_stderr, redirect_stdout

        common = ["--models", str(self.models), "--stage", str(self.stage), "--base-url", "https://host", "--version", "1"]
        for extra in (["--allow-license", "CC-BY-NC-4.0"], ["--reason", "why"], ["--allow-license", "CC-BY-NC-4.0", "--reason", " "]):
            with self.subTest(extra=extra), redirect_stderr(io.StringIO()) as err:
                self.assertEqual(1, bm.main(common + extra))
                self.assertIn("--reason", err.getvalue())
        with redirect_stdout(io.StringIO()):
            self.assertEqual(0, bm.main(common + ["--allow-license", "CC-BY-NC-4.0", "--reason", "granted in writing"]))


if __name__ == "__main__":
    unittest.main()
