"""add_model.py admits a custom ONNX model only when the task's pipeline could run it (MOD24)."""

from __future__ import annotations

import hashlib
import importlib.util
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import add_model as am  # noqa: E402
import build_manifest as bm  # noqa: E402

HAS_ONNX = all(importlib.util.find_spec(m) is not None for m in ("onnx", "onnxruntime", "numpy"))

# The depth contract, as export_models.py writes depth-anything-v2-small's meta.json.
DEPTH = {
    "schema": 1,
    "name": "depth-anything-v2-small",
    "task": "depth",
    "inputs": [{"name": "image", "shape": [1, 3, 518, 518]}],
    "outputs": [{"name": "depth", "shape": [1, 518, 518]}],
    "preprocess": {"size": 518, "value_range": [0.0, 1.0]},
    "params": {},
}
UPSCALE = {
    "name": "realesrgan-x4plus",
    "task": "upscale",
    "inputs": [{"name": "image", "shape": [1, 3, "height", "width"]}],
    "outputs": [{"name": "upscaled", "shape": [1, 3, "out_height", "out_width"]}],
}


def graph(inputs: list, outputs: list) -> dict:
    return {
        "inputs": [{"name": f"in{i}", "shape": s, "elem": am.FLOAT} for i, s in enumerate(inputs)],
        "outputs": [{"name": f"out{i}", "shape": s, "elem": am.FLOAT} for i, s in enumerate(outputs)],
    }


class ContractTests(unittest.TestCase):
    def test_a_graph_shaped_like_the_built_in_passes_under_its_own_names(self):
        am.check_contract(DEPTH, graph([[1, 3, 518, 518]], [[1, 518, 518]]), "custom")

    def test_a_dynamic_axis_where_the_built_in_fixes_one_passes(self):
        am.check_contract(DEPTH, graph([[1, 3, "h", "w"]], [[1, "h", "w"]]), "custom")

    def test_another_size_is_refused_with_both_shapes(self):
        with self.assertRaisesRegex(am.AddError, "1 x 3 x 384 x 384; depth expects 1 x 3 x 518 x 518"):
            am.check_contract(DEPTH, graph([[1, 3, 384, 384]], [[1, 384, 384]]), "custom")

    def test_another_rank_is_refused(self):
        with self.assertRaisesRegex(am.AddError, "output 0 .* rank 4"):
            am.check_contract(DEPTH, graph([[1, 3, 518, 518]], [[1, 1, 518, 518]]), "custom")

    def test_another_count_of_outputs_is_refused(self):
        with self.assertRaisesRegex(am.AddError, "2 outputs; depth takes 1"):
            am.check_contract(DEPTH, graph([[1, 3, 518, 518]], [[1, 518, 518], [1]]), "custom")

    def test_a_fixed_axis_where_the_app_feeds_any_size_is_refused(self):
        with self.assertRaisesRegex(am.AddError, "fixes axis 2 at 256"):
            am.check_contract(UPSCALE, graph([[1, 3, 256, 256]], [[1, 3, 1024, 1024]]), "custom")

    def test_a_run_may_answer_numbers_on_the_dynamic_axes(self):
        am.check_contract(UPSCALE, graph([[1, 3, "h", "w"]], [[1, 3, 256, 256]]), "custom", ran=True)

    def test_a_tensor_that_is_not_float32_is_refused(self):
        wrong = graph([[1, 3, 518, 518]], [[1, 518, 518]])
        wrong["inputs"][0]["elem"] = 10  # float16
        with self.assertRaisesRegex(am.AddError, "not float32"):
            am.check_contract(DEPTH, wrong, "custom")


class TemplateTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.models = Path(temporary.name)
        body = json.dumps(DEPTH).encode()
        self.body = body
        self.manifest = {"models": [{"name": DEPTH["name"], "task": "depth", "meta": {"url": "https://host/depth-meta.json", "sha256": hashlib.sha256(body).hexdigest()}}]}

    def test_an_unknown_task_names_the_ones_the_app_knows(self):
        with self.assertRaisesRegex(am.AddError, "knows depth"):
            am.template_for("segmentation", self.manifest, self.models)

    def test_the_exported_built_in_is_read_first(self):
        (self.models / DEPTH["name"]).mkdir()
        (self.models / DEPTH["name"] / "meta.json").write_text(json.dumps({**DEPTH, "params": {"local": 1}}), encoding="utf-8")
        self.assertEqual({"local": 1}, am.template_for("depth", self.manifest, self.models, fetch=self.fail)["params"])

    def test_otherwise_it_is_downloaded_and_checked_against_the_manifest(self):
        self.assertEqual(DEPTH, am.template_for("depth", self.manifest, self.models, fetch=lambda url: io.BytesIO(self.body)))
        with self.assertRaisesRegex(am.AddError, "SHA-256"):
            am.template_for("depth", self.manifest, self.models, fetch=lambda url: io.BytesIO(self.body + b" "))

    def test_the_embedded_manifest_names_a_built_in_for_every_task_the_pipelines_run(self):
        known = am.tasks(json.loads(am.EMBEDDED.read_text(encoding="utf-8")))
        self.assertEqual({"image-to-triplane", "triplane-decoder", "depth", "background-removal", "upscale"}, set(known))


class AddTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        self.models, self.stage = root / "models", root / "stage"
        self.models.mkdir()
        self.onnx = root / "custom.onnx"
        self.onnx.write_bytes(b"stand-in graph")
        self.shaped = graph([[1, 3, 518, 518]], [[1, 518, 518]])

    def added(self, **overrides) -> dict:
        arguments = dict(
            name="my-depth", task="depth", fp32=self.onnx, fp16=None, license_id="MIT", license_url="https://example.org/LICENSE",
            source="https://example.org/weights", template=DEPTH, out=self.models, run=lambda *a: None, read=lambda path: self.shaped,
        )
        return am.add(**{**arguments, **overrides})

    def test_the_folder_is_what_build_manifest_publishes(self):
        meta = self.added()
        self.assertEqual("in0", meta["inputs"][0]["name"])
        self.assertEqual(DEPTH["preprocess"], meta["preprocess"])
        self.assertEqual(hashlib.sha256(b"stand-in graph").hexdigest(), meta["variants"]["fp32"]["sha256"])
        manifest = bm.build(self.models, self.stage, "https://host/models", "custom-1")
        model = manifest["models"][0]
        self.assertEqual(("my-depth", "depth", "MIT"), (model["name"], model["task"], model["license"]))
        self.assertEqual(b"stand-in graph", (self.stage / "my-depth-fp32.onnx").read_bytes())

    def test_a_name_with_a_path_in_it_is_refused(self):
        for name in ("../depth", "a\\b", "Depth"):
            with self.subTest(name=name), self.assertRaisesRegex(am.AddError, "not a model name"):
                self.added(name=name)

    def test_a_model_already_there_is_kept_unless_replaced(self):
        self.added()
        with self.assertRaisesRegex(am.AddError, "--replace"):
            self.added()
        self.added(replace=True)

    def test_a_refused_graph_writes_nothing(self):
        with self.assertRaises(am.AddError):
            self.added(read=lambda path: graph([[1, 3, 384, 384]], [[1, 384, 384]]))
        self.assertEqual([], list(self.models.iterdir()))

    def test_fp16_named_differently_from_fp32_is_refused(self):
        fp16 = self.onnx.with_name("custom16.onnx")
        fp16.write_bytes(b"half")
        renamed = graph([[1, 3, 518, 518]], [[1, 518, 518]])
        renamed["outputs"][0]["name"] = "other"
        with self.assertRaisesRegex(am.AddError, "named differently"):
            self.added(fp16=fp16, read=lambda path: renamed if path == fp16 else self.shaped)


@unittest.skipUnless(HAS_ONNX, "onnx, onnxruntime and numpy are not installed (scripts/requirements.txt)")
class OnnxTests(unittest.TestCase):
    """A real graph: the channel mean of the image, which is depth-shaped."""

    def setUp(self):
        import onnx
        from onnx import TensorProto, helper

        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)

        def save(name: str, side: int) -> Path:
            node = helper.make_node("ReduceMean", ["pixels"], ["nearness"], axes=[1], keepdims=0)
            model = helper.make_model(
                helper.make_graph(
                    [node], "mean",
                    [helper.make_tensor_value_info("pixels", TensorProto.FLOAT, [1, 3, side, side])],
                    [helper.make_tensor_value_info("nearness", TensorProto.FLOAT, [1, side, side])],
                ),
                opset_imports=[helper.make_opsetid("", 17)],
                ir_version=8,  # what export_models.py's opset 17 exports carry, and ONNX Runtime reads
            )
            path = self.root / name
            onnx.save(model, str(path))
            return path

        self.good, self.small = save("good.onnx", 518), save("small.onnx", 64)

    def test_a_graph_that_fits_is_checked_run_and_described_under_its_own_names(self):
        meta = am.add("mean-depth", "depth", self.good, None, "MIT", "https://example.org/LICENSE", "https://example.org/w", DEPTH, self.root / "models")
        self.assertEqual([{"name": "pixels", "shape": [1, 3, 518, 518]}], meta["inputs"])
        self.assertEqual([{"name": "nearness", "shape": [1, 518, 518]}], meta["outputs"])
        self.assertTrue((self.root / "models" / "mean-depth" / "fp32.onnx").is_file())

    def test_a_graph_onnx_runtime_cannot_load_is_refused(self):
        broken = self.root / "broken.onnx"
        broken.write_bytes(self.good.read_bytes()[:-8])
        with self.assertRaises(am.AddError):
            am.add("broken-depth", "depth", broken, None, "MIT", "u", "s", DEPTH, self.root / "models")
        self.assertFalse((self.root / "models" / "broken-depth").exists())

    def test_a_graph_of_another_size_is_refused_before_it_runs(self):
        with self.assertRaisesRegex(am.AddError, "1 x 3 x 64 x 64"):
            am.add("small-depth", "depth", self.small, None, "MIT", "u", "s", DEPTH, self.root / "models")


if __name__ == "__main__":
    unittest.main()
