"""quantize_triposr.py lays each INT8 variant out as a models folder of its own (MOD115)."""

from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import quantize_triposr as qt  # noqa: E402


class QuantizeTripoSRTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        self.models, self.variants = root / "models", root / "variants"
        for model in qt.OBJECT_MODELS:
            (self.models / model).mkdir(parents=True)
            (self.models / model / "fp32.onnx").write_bytes(model.encode())
            (self.models / model / "meta.json").write_text("{}", encoding="utf-8")

    def test_each_variant_quantizes_its_models_and_keeps_the_others_as_they_are(self):
        quantized = []

        def stand_in(source: Path, target: Path) -> None:
            quantized.append(source.parent.name)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(b"int8 " + source.read_bytes())

        made = qt.build(self.models, self.variants, quantizer=stand_in)

        self.assertEqual(["triposr-int8-encoder", "triposr-int8-decoder"], [f.name for f in made])
        self.assertEqual(["triposr-encoder", "triposr-decoder"], quantized)
        encoder = self.variants / "triposr-int8-encoder"
        self.assertEqual(b"int8 triposr-encoder", (encoder / "triposr-encoder" / "fp32.onnx").read_bytes())
        self.assertEqual(b"triposr-decoder", (encoder / "triposr-decoder" / "fp32.onnx").read_bytes())
        self.assertEqual(b"u2net", (encoder / "u2net" / "fp32.onnx").read_bytes())
        for model in qt.OBJECT_MODELS:
            self.assertTrue((encoder / model / "meta.json").is_file(), model)

    def test_install_puts_the_int8_encoder_in_the_models_folder_and_lists_it(self):
        # MOD116: what publish_models.py stages, with its size and hash in meta.json.
        import hashlib
        import json

        (self.models / "triposr-encoder" / "meta.json").write_text('{"variants": {"fp32": {"file": "fp32.onnx"}}}', encoding="utf-8")
        qt.build(self.models, self.variants, quantizer=lambda s, t: t.write_bytes(b"int8 weights"))

        described = qt.install(self.models, self.variants)

        self.assertEqual(b"int8 weights", (self.models / "triposr-encoder" / "int8.onnx").read_bytes())
        expected = {"file": "int8.onnx", "bytes": 12, "sha256": hashlib.sha256(b"int8 weights").hexdigest()}
        self.assertEqual(expected, described)
        meta = json.loads((self.models / "triposr-encoder" / "meta.json").read_text(encoding="utf-8"))
        self.assertEqual({"fp32", "int8"}, set(meta["variants"]))
        # The variant folder's linked meta.json is not written through.
        self.assertNotIn("int8", json.loads((self.variants / "triposr-int8-encoder" / "triposr-encoder" / "meta.json").read_text(encoding="utf-8")).get("variants", {}))

    def test_install_before_a_build_says_what_to_run(self):
        with self.assertRaisesRegex(FileNotFoundError, "without --install"):
            qt.install(self.models, self.variants)

    def test_a_missing_model_is_named(self):
        (self.models / "u2net" / "fp32.onnx").unlink()
        with self.assertRaisesRegex(FileNotFoundError, "u2net"):
            qt.build(self.models, self.variants, quantizer=lambda s, t: None)


if __name__ == "__main__":
    unittest.main()
