"""Build the manifest the app downloads models from, and stage the files to publish.

Reads every <models>/<name>/meta.json export_models.py wrote and writes <stage>/manifest.json
plus, beside it, each file under the flat name a Hugging Face repository holds:
<name>-<variant>.onnx and <name>-meta.json. publish_models.py uploads the stage folder; this
script alone only builds it, so the result can be checked before anything leaves the PC.

    .venv\\Scripts\\python scripts\\build_manifest.py --base-url https://example.org/models --version 1

Standard library only.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
MODELS = REPO / ".cache" / "models"
STAGE = REPO / ".cache" / "publish"
MANIFEST_SCHEMA = 1
# What a public repository may carry without asking (MOD25). Depth Anything V2 Base and Large,
# CC BY-NC 4.0, are what this refuses: model-sources.json says why only Small is used.
REDISTRIBUTABLE = ("MIT", "Apache-2.0", "BSD-2-Clause", "BSD-3-Clause", "CC-BY-4.0")


class ManifestError(Exception):
    """A model folder is not ready to publish."""


def redistributable(license_text: str, allowed: set[str]) -> bool:
    """Whether every part of a license line is one `allowed` names.

    A line names parts as meta.json writes them: "Apache-2.0 (U-2-Net weights), MIT (rembg
    export)" is two, each with a note, and both must pass; "MIT OR Apache-2.0" passes on either.
    The whole line may also be allowed as it is spelled.
    """
    if license_text.strip() in allowed:
        return True
    parts = [p for chunk in license_text.split(",") for p in chunk.split(" AND ")]
    return bool(parts) and all(
        any(re.sub(r"\([^)]*\)", "", choice).strip() in allowed for choice in part.split(" OR ")) for part in parts
    )


def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as f:
        while chunk := f.read(1 << 20):
            digest.update(chunk)
    return digest.hexdigest()


def check_licenses(metas, allowed: set[str]) -> None:
    """Refuse the first model a public repository may not carry, naming it and its license."""
    for meta in metas:
        license_text = meta["upstream"]["license"]
        if not redistributable(license_text, allowed):
            raise ManifestError(
                f"{meta['name']}: its license, {license_text}, is not one a public repository may carry "
                f"({', '.join(REDISTRIBUTABLE)}); if this one allows it, pass --allow-license \"{license_text}\" --reason \"<why>\""
            )


def build(
    models: Path,
    stage: Path,
    base_url: str,
    version: str,
    copy: bool = True,
    only: list[str] | None = None,
    variants_wanted: set[str] | None = None,
    allow: dict[str, str] | None = None,
) -> dict:
    """The manifest over every model, or `only` those, with every variant or `variants_wanted`.

    A model whose license is not in REDISTRIBUTABLE, nor in `allow` (license to the reason it
    may be published anyway), is refused before anything is staged.
    """
    allowed = set(REDISTRIBUTABLE) | set(allow or {})
    folders = sorted(p.parent for p in models.glob("*/meta.json"))
    if only is not None:
        missing = [name for name in only if not (models / name / "meta.json").is_file()]
        if missing:
            raise ManifestError(f"{', '.join(missing)}: no meta.json under {models}; run scripts/export_models.py first")
        folders = [models / name for name in only]
    if not folders:
        raise ManifestError(f"no meta.json under {models}; run scripts/export_models.py first")
    metas = {folder: json.loads((folder / "meta.json").read_text(encoding="utf-8")) for folder in folders}
    check_licenses(metas.values(), allowed)
    base = base_url.rstrip("/")
    stage.mkdir(parents=True, exist_ok=True)
    entries = []
    for folder in folders:
        meta_path = folder / "meta.json"
        meta = metas[folder]
        name = meta["name"]
        variants = {}
        for variant, described in meta["variants"].items():
            if variants_wanted is not None and variant not in variants_wanted:
                continue
            source = folder / described["file"]
            if not source.is_file():
                raise ManifestError(f"{name}: {source} is listed in meta.json but missing")
            if source.stat().st_size != described["bytes"] or sha256_of(source) != described["sha256"]:
                raise ManifestError(f"{name}: {source} differs from meta.json; re-run export_models.py")
            asset = f"{name}-{variant}.onnx"
            variants[variant] = {"url": f"{base}/{asset}", "bytes": described["bytes"], "sha256": described["sha256"]}
            if copy:
                shutil.copyfile(source, stage / asset)
        if not variants:
            raise ManifestError(f"{name}: none of {sorted(variants_wanted or [])} is in its meta.json")
        meta_asset = f"{name}-meta.json"
        if copy:
            shutil.copyfile(meta_path, stage / meta_asset)
        entries.append(
            {
                "name": name,
                "task": meta["task"],
                "meta": {"url": f"{base}/{meta_asset}", "bytes": meta_path.stat().st_size, "sha256": sha256_of(meta_path)},
                "variants": variants,
                "license": meta["upstream"]["license"],
                "license_url": meta["upstream"]["license_url"],
            }
        )
    manifest = {"schema": MANIFEST_SCHEMA, "version": version, "base_url": base, "models": entries}
    (stage / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
    return manifest


def add_allow_arguments(parser: argparse.ArgumentParser) -> None:
    parser.add_argument("--allow-license", metavar="LICENSE", help="publish a model under this license too, for this run")
    parser.add_argument("--reason", help="why --allow-license's license allows redistribution; it goes into the model card")


def allowed_by(args: argparse.Namespace) -> dict[str, str]:
    """--allow-license with its --reason, which the one is refused without."""
    if (args.allow_license is None) != (args.reason is None):
        raise ManifestError("--allow-license and --reason go together: name the license and say why it may be published")
    if args.allow_license is not None and not args.reason.strip():
        raise ManifestError("--reason is empty: say why the license allows redistribution")
    return {} if args.allow_license is None else {args.allow_license: args.reason.strip()}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Build manifest.json and stage the model files to publish.")
    parser.add_argument("--base-url", required=True, help="where the staged files will be served from")
    parser.add_argument("--version", required=True, help="the manifest version the app pins")
    parser.add_argument("--models", type=Path, default=MODELS, help="export output (default: .cache/models)")
    parser.add_argument("--stage", type=Path, default=STAGE, help="folder to stage into (default: .cache/publish)")
    parser.add_argument("--manifest-only", action="store_true", help="write manifest.json without copying files")
    add_allow_arguments(parser)
    args = parser.parse_args(argv)
    try:
        allow = allowed_by(args)
        manifest = build(args.models, args.stage, args.base_url, args.version, copy=not args.manifest_only, allow=allow)
    except ManifestError as error:
        print(f"ERROR {error}", file=sys.stderr)
        return 1
    files = sum(len(m["variants"]) + 1 for m in manifest["models"])
    print(f"manifest {args.version}: {len(manifest['models'])} models, {files} files -> {args.stage}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
