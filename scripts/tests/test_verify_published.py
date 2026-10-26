"""verify_published.py names every published file that differs from its manifest (MOD26)."""

from __future__ import annotations

import hashlib
import io
import json
import sys
import unittest
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import verify_published as vp  # noqa: E402

LFS = b"onnx bytes kept in LFS"
META = b'{"name": "tiny"}'


def entry(url: str, body: bytes) -> dict:
    return {"url": url, "bytes": len(body), "sha256": hashlib.sha256(body).hexdigest()}


MANIFEST = {
    "version": "1",
    "models": [{"name": "tiny", "meta": entry("https://host/tiny-meta.json", META), "variants": {"fp32": entry("https://host/tiny-fp32.onnx", LFS)}}],
}


class Repository:
    """Hugging Face as verify_published.py reads it: LFS files answer a HEAD with their hash."""

    def __init__(self, lfs: bytes = LFS, meta: bytes = META):
        self.lfs, self.meta, self.downloaded = lfs, meta, []

    def head(self, url: str) -> dict:
        if url.endswith(".onnx"):
            return {"x-linked-size": str(len(self.lfs)), "x-linked-etag": f'"{hashlib.sha256(self.lfs).hexdigest()}"'}
        return {"x-linked-etag": '"209e7d99503bec1768ca1b56937cc3667d8df369"'}  # a git blob's SHA-1

    def get(self, url: str) -> bytes:
        self.downloaded.append(url)
        return self.lfs if url.endswith(".onnx") else self.meta


class VerifyTests(unittest.TestCase):
    def test_a_repository_that_holds_the_manifest_passes_downloading_only_what_lfs_does_not_hash(self):
        repo = Repository()
        self.assertEqual([], vp.verify(MANIFEST, repo.head, repo.get))
        self.assertEqual(["https://host/tiny-meta.json"], repo.downloaded)

    def test_an_lfs_file_replaced_by_hand_is_named_with_its_hash(self):
        repo = Repository(lfs=b"replaced by hand, same size!!"[: len(LFS)])
        problems = vp.verify(MANIFEST, repo.head, repo.get)
        self.assertEqual(1, len(problems))
        self.assertRegex(problems[0], r"^tiny fp32: https://host/tiny-fp32\.onnx has SHA-256 [0-9a-f]{64}; the manifest says")

    def test_a_truncated_lfs_file_is_named_with_its_size(self):
        repo = Repository(lfs=LFS[:-4])
        self.assertEqual([f"tiny fp32: https://host/tiny-fp32.onnx is {len(LFS) - 4} bytes; the manifest says {len(LFS)}"], vp.verify(MANIFEST, repo.head, repo.get))

    def test_a_meta_json_that_changed_is_named(self):
        repo = Repository(meta=b'{"name": "TINY"}')
        self.assertRegex(vp.verify(MANIFEST, repo.head, repo.get)[0], r"^tiny meta\.json: .* has SHA-256 ")

    def test_every_file_is_listed_once(self):
        self.assertEqual(["tiny meta.json", "tiny fp32"], [label for label, _ in vp.files_of(MANIFEST)])

    def test_the_cli_reads_the_recorded_repository_and_exits_1_on_a_difference(self):
        calls = {}

        def verify_repo(repo_id, revision, token):
            calls["at"] = (repo_id, revision)
            return MANIFEST, ["tiny fp32: differs"]

        record = json.loads(vp.RECORD.read_text(encoding="utf-8"))
        with mock.patch.object(vp, "verify_repo", verify_repo), redirect_stderr(io.StringIO()) as err, redirect_stdout(io.StringIO()):
            self.assertEqual(1, vp.main([]))
        self.assertEqual((record["repo"], record["manifest_commit"]), calls["at"])
        self.assertIn("ERROR tiny fp32: differs", err.getvalue())
        with mock.patch.object(vp, "verify_repo", lambda *a: (MANIFEST, [])), redirect_stdout(io.StringIO()) as out:
            self.assertEqual(0, vp.main(["--repo", "me/models"]))
        self.assertIn("me/models@main: all 2 files match", out.getvalue())


if __name__ == "__main__":
    unittest.main()
