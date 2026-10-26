"""Publish the ONNX models to Hugging Face, where the app downloads them from.

Every model Modelado runs is under a license that allows redistribution (MIT, Apache-2.0), so
all of them are hosted in one public model repository, alegauss/modelado-models by default.
build_manifest.py refuses any other license before anything is staged (MOD25), dry runs
included; --allow-license <license> --reason "<why>" lets one more through for that run, and the
reason is written into the model card beside it.
Two commits:

  1. every variant and its meta.json under the flat names build_manifest.py stages, and a model
     card naming each model's license;
  2. manifest.json, built with resolve/<commit 1>/ URLs, so no later upload can change a file
     the app verifies by SHA-256.

The pinned manifest is also copied into src/Modelado.Inference/models-manifest.json, which the
app embeds: rebuild the app afterwards and it knows every size and hash without the network.
That copy waits on verify_published.py's check of the repository as it now stands (MOD26).

    .venv\\Scripts\\python scripts\\publish_models.py --token-file D:\\Keys\\hf.txt
    .venv\\Scripts\\python scripts\\publish_models.py --repo me/my-models --token-file hf.txt
    .venv\\Scripts\\python scripts\\publish_models.py --dry-run

The token (a Hugging Face write token) is read from the file, or from the HF_TOKEN variable,
and never printed. The result goes to scripts/published-models.json.

--model publishes the named models instead of the five built-ins, for a repository of one's
own: a model scripts/add_model.py added, say (MOD24). That manifest is printed for the app's
--manifest and touches neither the embedded copy nor the record, which belong to the built-ins.

    .venv\\Scripts\\python scripts\\publish_models.py --repo me/my-models --model my-depth --token-file hf.txt
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
import verify_published as vp  # noqa: E402

REPO_ID = "alegauss/modelado-models"
VERSION = "1"
HOSTED = ["triposr-encoder", "triposr-decoder", "depth-anything-v2-small", "u2net", "realesrgan-x4plus"]


def _size(size: int) -> str:
    return f"{size / 1e6:.0f} MB" if size >= 10e6 else f"{size / 1e6:.1f} MB"


def model_card(manifest: dict, allow: dict[str, str] | None = None) -> str:
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
    allowed = [(m, allow[m["license"]]) for m in manifest["models"] if m["license"] in (allow or {})]
    if allowed:
        lines += ["## Licenses published by exception", ""]
        lines += [f"- **{model['name']}** ({model['license']}): {reason}" for model, reason in allowed]
        lines.append("")
    return "\n".join(lines)


def stage(models: Path, stage_dir: Path, base_url: str, only: list[str] = HOSTED, allow: dict[str, str] | None = None) -> dict:
    """The files to upload, with manifest.json and README.md, for the given base URL."""
    if stage_dir.exists():
        shutil.rmtree(stage_dir)
    manifest = bm.build(models, stage_dir, base_url, VERSION, only=only, allow=allow)
    (stage_dir / "README.md").write_text(model_card(manifest, allow), encoding="utf-8", newline="\n")
    return manifest


def resolve_base(repo_id: str, commit: str) -> str:
    return f"https://huggingface.co/{repo_id}/resolve/{commit}"


def upload(token: str, models: Path, stage_dir: Path, repo_id: str, private: bool, only: list[str] = HOSTED, allow: dict[str, str] | None = None) -> dict:
    """The two commits; returns what scripts/published-models.json records."""
    from huggingface_hub import HfApi

    api = HfApi(token=token)
    api.create_repo(repo_id, repo_type="model", private=private, exist_ok=True)

    # Commit 1: the files and the card. The manifest staged beside them names a placeholder base,
    # so it is left out here and rebuilt against this commit below.
    stage(models, stage_dir, resolve_base(repo_id, "pending"), only, allow)
    (stage_dir / "manifest.json").unlink()
    files = api.upload_folder(repo_id=repo_id, folder_path=str(stage_dir), commit_message=f"Modelado models, manifest version {VERSION}")
    files_commit = files.oid

    # Commit 2: manifest.json with every URL pinned to commit 1.
    manifest = stage(models, stage_dir, resolve_base(repo_id, files_commit), only, allow)
    pinned = api.upload_file(
        path_or_fileobj=str(stage_dir / "manifest.json"),
        path_in_repo="manifest.json",
        repo_id=repo_id,
        commit_message=f"manifest.json pinned to {files_commit[:12]}",
    )
    # MOD26: read back what landed before the app is told to trust it. A failure leaves the
    # embedded manifest as it was.
    _, problems = vp.verify_repo(repo_id, pinned.oid, token)
    if problems:
        raise vp.VerifyError(f"{repo_id}@{pinned.oid[:12]} does not hold what was uploaded:\n  " + "\n  ".join(problems))
    if only == HOSTED:
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
    parser.add_argument("--model", action="append", default=[], metavar="NAME", help="publish this model instead of the built-ins; repeatable")
    bm.add_allow_arguments(parser)
    args = parser.parse_args(argv)
    only = args.model or HOSTED
    if only != HOSTED and args.repo == REPO_ID:
        print(f"ERROR --model publishes to a repository of your own; {REPO_ID} holds the built-ins: pass --repo", file=sys.stderr)
        return 2
    try:
        allow = bm.allowed_by(args)
        if args.dry_run:
            manifest = stage(args.models, args.stage, resolve_base(args.repo, "DRY-RUN"), only, allow)
            print(f"staged {len(manifest['models'])} models in {args.stage}; nothing uploaded")
            return 0
        token = read_token(args.token_file)
        if token is None:
            print("ERROR a write token is required: --token-file or HF_TOKEN", file=sys.stderr)
            return 2
        record = upload(token, args.models, args.stage, args.repo, args.private, only, allow)
    except (bm.ManifestError, vp.VerifyError) as error:
        print(f"ERROR {error}", file=sys.stderr)
        return 1
    if only != HOSTED:
        print(f"published {len(record['models'])} models; give the app --manifest {record['manifest_url']}")
        return 0
    RECORD.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"published {len(record['models'])} models; the app pins {record['manifest_url']}")
    print(f"rebuild the app: {EMBEDDED.relative_to(REPO)} now carries this manifest")
    return 0


if __name__ == "__main__":
    sys.exit(main())
