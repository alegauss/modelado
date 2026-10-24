"""publish_models.py stages every hosted model, writes the card, and never prints the token."""

from __future__ import annotations

import io
import json
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import publish_models as pm  # noqa: E402
from test_build_manifest import write_model  # noqa: E402


class PublishModelsTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        self.root = root
        self.models, self.stage = root / "models", root / "stage"
        for name in pm.HOSTED:
            variants = {"fp32": f"{name} fp32".encode()}
            if name in ("triposr-encoder", "depth-anything-v2-small", "realesrgan-x4plus"):
                variants["fp16"] = f"{name} fp16".encode()
            write_model(self.models, name, variants)

    def test_every_hosted_model_is_staged_with_its_variants(self):
        manifest = pm.stage(self.models, self.stage, pm.resolve_base(pm.REPO_ID, "c0ffee"))
        staged = sorted(p.name for p in self.stage.glob("*.onnx"))
        self.assertEqual(8, len(staged))
        self.assertIn("triposr-encoder-fp16.onnx", staged)
        urls = {m["name"]: m["variants"]["fp32"]["url"] for m in manifest["models"]}
        self.assertEqual(f"https://huggingface.co/{pm.REPO_ID}/resolve/c0ffee/u2net-fp32.onnx", urls["u2net"])
        self.assertEqual(sorted(pm.HOSTED), sorted(urls))

    def test_the_card_names_every_model_and_license(self):
        pm.stage(self.models, self.stage, pm.resolve_base(pm.REPO_ID, "c0ffee"))
        card = (self.stage / "README.md").read_text(encoding="utf-8")
        for name in pm.HOSTED:
            self.assertIn(f"| {name} |", card)
        self.assertIn("pipeline_tag: image-to-3d", card)

    def test_dry_run_uploads_nothing(self):
        with mock.patch.dict(sys.modules, {"huggingface_hub": None}), redirect_stdout(io.StringIO()):
            code = pm.main(["--dry-run", "--models", str(self.models), "--stage", str(self.stage)])
        self.assertEqual(0, code)
        self.assertTrue((self.stage / "manifest.json").is_file())

    def test_without_a_token_nothing_is_uploaded(self):
        with mock.patch.dict(os.environ, {}, clear=True), redirect_stderr(io.StringIO()) as err:
            code = pm.main(["--models", str(self.models), "--stage", str(self.stage)])
        self.assertEqual(2, code)
        self.assertIn("token", err.getvalue())

    def test_the_token_comes_from_the_file_and_is_never_printed(self):
        secret = self.root / "hf.txt"
        secret.write_text("hf_secret_value\n", encoding="utf-8")
        self.assertEqual("hf_secret_value", pm.read_token(secret))
        calls = {}

        def fake_upload(token, models, stage_dir, repo_id, private):
            calls["token"] = token
            return {"repo": repo_id, "version": pm.VERSION, "files_commit": "a", "manifest_commit": "b", "manifest_url": "u", "models": []}

        out, err = io.StringIO(), io.StringIO()
        with mock.patch.object(pm, "upload", fake_upload), mock.patch.object(pm, "RECORD", self.root / "record.json"), redirect_stdout(out), redirect_stderr(err):
            code = pm.main(["--token-file", str(secret), "--models", str(self.models), "--stage", str(self.stage), "--repo", "me/models"])
        self.assertEqual(0, code)
        self.assertEqual("hf_secret_value", calls["token"])
        self.assertNotIn("hf_secret_value", out.getvalue() + err.getvalue())
        self.assertEqual("me/models", json.loads((self.root / "record.json").read_text(encoding="utf-8"))["repo"])


if __name__ == "__main__":
    unittest.main()
