"""Writes cases/fixtures/download: a 1.2 MB stand-in model (30 s at the server's 40 KB/s), a stand-in meta.json, and a manifest
listing the five models Modelado downloads, every variant pointing at the stand-in on the fixture
server's port.

    python cases/fixtures/download/make_fixture.py
"""
import hashlib
import json
import pathlib
import random

root = pathlib.Path(__file__).resolve().parent
root.mkdir(parents=True, exist_ok=True)
base = "http://127.0.0.1:47631/files"

# Seeded, so regenerating writes the same bytes and the manifest's digest does not move.
standin = random.Random(11).randbytes(1200 * 1024)
(root / "standin.onnx").write_bytes(standin)
# A meta.json ModelMeta accepts (MOD23), so a finished stand-in download is listed in About; its
# ONNX file is random bytes, so an operation run on it fails at the session, which the cases expect.
meta = (json.dumps({
    "schema": 1,
    "name": "standin",
    "task": "stand-in",
    "inputs": [{"name": "input", "shape": [1, 3, 64, 64]}],
    "outputs": [{"name": "output", "shape": [1, 1, 64, 64]}],
    "preprocess": {"value_range": [0, 1]},
    "params": {},
    "variants": {"fp32": {"file": "fp32.onnx", "bytes": len(standin), "sha256": hashlib.sha256(standin).hexdigest()}},
    "upstream": {"url": "https://example.org/standin", "license": "MIT", "license_url": "https://opensource.org/license/mit"},
}, indent=2) + "\n").encode("utf-8")
(root / "standin-meta.json").write_bytes(meta)


def entry(name: str, data: bytes) -> dict:
    return {"url": f"{base}/{name}", "bytes": len(data), "sha256": hashlib.sha256(data).hexdigest()}


models = ["triposr-encoder", "triposr-decoder", "u2net", "depth-anything-v2-small", "realesrgan-x4plus"]
manifest = {
    "schema": 1,
    "version": "cases",
    "models": [
        {
            "name": name,
            "task": "stand-in",
            "meta": entry("standin-meta.json", meta),
            "variants": {"fp32": entry("standin.onnx", standin), "fp16": entry("standin.onnx", standin)},
            "license": "MIT",
            "license_url": "https://opensource.org/license/mit",
        }
        for name in models
    ],
}
(root / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")

# MOD27: a source added in About, listing one depth model of its own beside the built-in ones. Its
# meta.json names the depth task, so the Relief card offers it; the ONNX file is the same stand-in.
depth_meta = (json.dumps({**json.loads(meta), "name": "my-depth", "task": "depth"}, indent=2) + "\n").encode("utf-8")
(root / "standin-depth-meta.json").write_bytes(depth_meta)
custom = {
    "schema": 1,
    "version": "custom",
    "models": [
        {
            "name": "my-depth",
            "task": "depth",
            "meta": entry("standin-depth-meta.json", depth_meta),
            "variants": {"fp32": entry("standin.onnx", standin)},
            "license": "MIT",
            "license_url": "https://opensource.org/license/mit",
        }
    ],
}
(root / "custom-manifest.json").write_text(json.dumps(custom, indent=2) + "\n", encoding="utf-8", newline="\n")
print(f"wrote {root}")
