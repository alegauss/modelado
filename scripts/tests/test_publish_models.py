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

        def fake_upload(token, models, stage_dir, repo_id, private, only, allow):
            calls["token"] = token
            return {"repo": repo_id, "version": pm.VERSION, "files_commit": "a", "manifest_commit": "b", "manifest_url": "u", "models": []}

        out, err = io.StringIO(), io.StringIO()
        with mock.patch.object(pm, "upload", fake_upload), mock.patch.object(pm, "RECORD", self.root / "record.json"), redirect_stdout(out), redirect_stderr(err):
            code = pm.main(["--token-file", str(secret), "--models", str(self.models), "--stage", str(self.stage), "--repo", "me/models"])
        self.assertEqual(0, code)
        self.assertEqual("hf_secret_value", calls["token"])
        self.assertNotIn("hf_secret_value", out.getvalue() + err.getvalue())
        self.assertEqual("me/models", json.loads((self.root / "record.json").read_text(encoding="utf-8"))["repo"])

    def test_model_stages_only_the_named_models_and_card(self):
        write_model(self.models, "my-depth", {"fp32": b"custom"})
        with redirect_stdout(io.StringIO()):
            code = pm.main(["--dry-run", "--models", str(self.models), "--stage", str(self.stage), "--repo", "me/models", "--model", "my-depth"])
        self.assertEqual(0, code)
        self.assertEqual(["my-depth-fp32.onnx"], sorted(p.name for p in self.stage.glob("*.onnx")))
        self.assertIn("| my-depth |", (self.stage / "README.md").read_text(encoding="utf-8"))

    def test_a_license_allowed_by_hand_is_published_with_its_reason_in_the_card(self):
        write_model(self.models, "nc-depth", {"fp32": b"nc"}, license_id="CC-BY-NC-4.0")
        args = ["--dry-run", "--models", str(self.models), "--stage", str(self.stage), "--repo", "me/models", "--model", "nc-depth"]
        with redirect_stderr(io.StringIO()) as err:
            self.assertEqual(1, pm.main(args))
        self.assertIn("nc-depth: its license, CC-BY-NC-4.0", err.getvalue())
        with redirect_stdout(io.StringIO()):
            self.assertEqual(0, pm.main([*args, "--allow-license", "CC-BY-NC-4.0", "--reason", "the author granted it in writing"]))
        card = (self.stage / "README.md").read_text(encoding="utf-8")
        self.assertIn("- **nc-depth** (CC-BY-NC-4.0): the author granted it in writing", card)

    def test_model_is_refused_on_the_built_ins_repository(self):
        with redirect_stderr(io.StringIO()) as err:
            code = pm.main(["--dry-run", "--models", str(self.models), "--stage", str(self.stage), "--model", "u2net"])
        self.assertEqual(2, code)
        self.assertIn("--repo", err.getvalue())

    def test_upload_copies_the_manifest_into_the_app_only_for_the_built_ins(self):
        write_model(self.models, "my-depth", {"fp32": b"custom"})

        class Api:
            def __init__(self, token):
                pass

            def create_repo(self, *args, **kwargs):
                pass

            def upload_folder(self, **kwargs):
                return mock.Mock(oid="files0000commit")

            def upload_file(self, **kwargs):
                return mock.Mock(oid="manifest000commit")

        embedded = self.root / "embedded.json"
        hub = mock.Mock(HfApi=Api)
        with mock.patch.dict(sys.modules, {"huggingface_hub": hub}), mock.patch.object(pm, "EMBEDDED", embedded):
            pm.upload("t", self.models, self.stage, "me/models", False, ["my-depth"])
            self.assertFalse(embedded.exists())
            pm.upload("t", self.models, self.stage, "me/models", False)
            self.assertEqual(sorted(pm.HOSTED), sorted(m["name"] for m in json.loads(embedded.read_text(encoding="utf-8"))["models"]))

    def test_a_custom_publish_leaves_the_record_alone_and_names_its_manifest(self):
        write_model(self.models, "my-depth", {"fp32": b"custom"})
        seen = {}

        def fake_upload(token, models, stage_dir, repo_id, private, only, allow):
            seen["only"] = only
            return {"repo": repo_id, "version": pm.VERSION, "files_commit": "a", "manifest_commit": "b", "manifest_url": "https://u/manifest.json", "models": only}

        record = self.root / "record.json"
        out = io.StringIO()
        with mock.patch.object(pm, "upload", fake_upload), mock.patch.object(pm, "RECORD", record), mock.patch.dict(os.environ, {"HF_TOKEN": "t"}), redirect_stdout(out):
            code = pm.main(["--models", str(self.models), "--stage", str(self.stage), "--repo", "me/models", "--model", "my-depth"])
        self.assertEqual(0, code)
        self.assertEqual(["my-depth"], seen["only"])
        self.assertFalse(record.exists())
        self.assertIn("--manifest https://u/manifest.json", out.getvalue())


if __name__ == "__main__":
    unittest.main()
