"""Publish the ONNX models to Hugging Face, where the app downloads them from.

Every model Modelado runs is under a license that allows redistribution (MIT, Apache-2.0), so
all of them are hosted in one public model repository, alegauss/modelado-models by default.
Two commits:

  1. every variant and its meta.json under the flat names build_manifest.py stages, and a model
     card naming each model's license;
  2. manifest.json, built with resolve/<commit 1>/ URLs, so no later upload can change a file
     the app verifies by SHA-256.

The pinned manifest is also copied into src/Modelado.Inference/models-manifest.json, which the
app embeds: rebuild the app afterwards and it knows every size and hash without the network.

    .venv\\Scripts\\python scripts\\publish_models.py --token-file D:\\Keys\\hf.txt
    .venv\\Scripts\\python scripts\\publish_models.py --repo me/my-models --token-file hf.txt
    .venv\\Scripts\\python scripts\\publish_models.py --dry-run

The token (a Hugging Face write token) is read from the file, or from the HF_TOKEN variable,
and never printed. The result goes to scripts/published-models.json.
Requirements: huggingface_hub (scripts/requirements.txt).
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import sys
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
MODELS = REPO / ".cache" / "models"
STAGE = REPO / ".cache" / "publish"
RECORD = SCRIPTS / "published-models.json"
EMBEDDED = REPO / "src" / "Modelado.Inference" / "models-manifest.json"

sys.path.insert(0, str(SCRIPTS))
import build_manifest as bm  # noqa: E402

REPO_ID = "alegauss/modelado-models"
VERSION = "1"
HOSTED = ["triposr-encoder", "triposr-decoder", "depth-anything-v2-small", "u2net"]


def _size(size: int) -> str:
    return f"{size / 1e6:.0f} MB" if size >= 10e6 else f"{size / 1e6:.1f} MB"


def model_card(manifest: dict) -> str:
    """The repository's README: what each file is, where it came from, and under which license."""
    lines = [
        "---",
        "license: other",
        "license_name: per-model",
        "pipeline_tag: image-to-3d",
        "tags: [onnx, image-to-3d, depth-estimation, triposr, modelado]",
        "---",
        "",
        "# Modelado models (ONNX)",
        "",
        "The ONNX models [Modelado](https://github.com/alegauss/modelado) downloads to turn a photo",
        "into a 3D model on Windows, with DirectML or on the CPU. They are converted from each",
        "upstream's own weights and checked against PyTorch by `scripts/export_models.py`. Every",
        "file is listed in `manifest.json` with its size and SHA-256, and the app refuses one that",
        "differs. Each model keeps its upstream license:",
        "",
        "| Model | Task | Variants | License |",
        "| --- | --- | --- | --- |",
    ]
    for model in manifest["models"]:
        variants = ", ".join(f"{v} ({_size(model['variants'][v]['bytes'])})" for v in model["variants"])
        lines.append(f"| {model['name']} | {model['task']} | {variants} | [{model['license']}]({model['license_url']}) |")
    lines += [
        "",
        "`triposr-encoder` and `triposr-decoder` are [TripoSR](https://huggingface.co/stabilityai/TripoSR)",
        "split in two: the encoder turns a 512 x 512 photo of an object into a triplane, the decoder",
        "returns density and colour at any point of it, and the app extracts the mesh with marching cubes.",
        "",
    ]
    return "\n".join(lines)


def stage(models: Path, stage_dir: Path, base_url: str) -> dict:
    """The files to upload, with manifest.json and README.md, for the given base URL."""
    if stage_dir.exists():
        shutil.rmtree(stage_dir)
    manifest = bm.build(models, stage_dir, base_url, VERSION, only=HOSTED)
    (stage_dir / "README.md").write_text(model_card(manifest), encoding="utf-8", newline="\n")
    return manifest


def resolve_base(repo_id: str, commit: str) -> str:
    return f"https://huggingface.co/{repo_id}/resolve/{commit}"


def upload(token: str, models: Path, stage_dir: Path, repo_id: str, private: bool) -> dict:
    """The two commits; returns what scripts/published-models.json records."""
    from huggingface_hub import HfApi

    api = HfApi(token=token)
    api.create_repo(repo_id, repo_type="model", private=private, exist_ok=True)

    # Commit 1: the files and the card. The manifest staged beside them names a placeholder base,
    # so it is left out here and rebuilt against this commit below.
    stage(models, stage_dir, resolve_base(repo_id, "pending"))
    (stage_dir / "manifest.json").unlink()
    files = api.upload_folder(repo_id=repo_id, folder_path=str(stage_dir), commit_message=f"Modelado models, manifest version {VERSION}")
    files_commit = files.oid

    # Commit 2: manifest.json with every URL pinned to commit 1.
    manifest = stage(models, stage_dir, resolve_base(repo_id, files_commit))
    pinned = api.upload_file(
        path_or_fileobj=str(stage_dir / "manifest.json"),
        path_in_repo="manifest.json",
        repo_id=repo_id,
        commit_message=f"manifest.json pinned to {files_commit[:12]}",
    )
    shutil.copyfile(stage_dir / "manifest.json", EMBEDDED)
    return {
        "repo": repo_id,
        "version": VERSION,
        "files_commit": files_commit,
        "manifest_commit": pinned.oid,
        "manifest_url": f"{resolve_base(repo_id, pinned.oid)}/manifest.json",
        "models": [m["name"] for m in manifest["models"]],
    }


def read_token(path: Path | None) -> str | None:
    if path is not None:
        return path.read_text(encoding="utf-8").strip()
    return os.environ.get("HF_TOKEN") or None


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Publish Modelado's ONNX models to Hugging Face.")
    parser.add_argument("--token-file", type=Path, help="a file holding a write token; HF_TOKEN otherwise; never printed")
    parser.add_argument("--repo", default=REPO_ID, help=f"the model repository (default: {REPO_ID})")
    parser.add_argument("--private", action="store_true", help="create the repository private (the app then cannot download)")
    parser.add_argument("--models", type=Path, default=MODELS, help="export output (default: .cache/models)")
    parser.add_argument("--stage", type=Path, default=STAGE, help="folder to stage into (default: .cache/publish)")
    parser.add_argument("--dry-run", action="store_true", help="stage and write the card and manifest, upload nothing")
    args = parser.parse_args(argv)
    try:
        if args.dry_run:
            manifest = stage(args.models, args.stage, resolve_base(args.repo, "DRY-RUN"))
            print(f"staged {len(manifest['models'])} models in {args.stage}; nothing uploaded")
            return 0
        token = read_token(args.token_file)
        if token is None:
            print("ERROR a write token is required: --token-file or HF_TOKEN", file=sys.stderr)
            return 2
        record = upload(token, args.models, args.stage, args.repo, args.private)
    except bm.ManifestError as error:
        print(f"ERROR {error}", file=sys.stderr)
        return 1
    RECORD.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"published {len(record['models'])} models; the app pins {record['manifest_url']}")
    print(f"rebuild the app: {EMBEDDED.relative_to(REPO)} now carries this manifest")
    return 0


if __name__ == "__main__":
    sys.exit(main())
