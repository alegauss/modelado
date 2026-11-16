"""INT8 variants of TripoSR, to measure against fp32 (MOD115).

A public ONNX export of TripoSR ships an INT8 encoder of about 430 MB against 1.7 GB in fp32, with
its quality loss described and never measured. This makes the variants from the exported fp32
models with ONNX Runtime's dynamic quantization (weights to INT8, activations quantized per run, so
no calibration photos are needed) and lays each out as a models folder of its own, the quantized
file standing in as fp32.onnx and the other models hard-linked beside it, so modelado-cli runs it
with --models and scripts/compare_models.py measures it against fp32 on the same photos:

    .venv\\Scripts\\python scripts\\quantize_triposr.py
    .venv\\Scripts\\python scripts\\compare_models.py --no-gpu

MOD116: --install then puts the INT8 encoder into .cache/models as triposr-encoder/int8.onnx,
listed in its meta.json, for publish_models.py to carry as the Light quality variant:

    .venv\\Scripts\\python scripts\\quantize_triposr.py --install

Needs the fp32 models in .cache/models (prepare-models.cmd) and onnxruntime.
"""

from __future__ import annotations

import hashlib
import json
import os
import shutil
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
MODELS = REPO / ".cache" / "models"
VARIANTS = REPO / ".cache" / "variants"
OBJECT_MODELS = ("triposr-encoder", "triposr-decoder", "u2net")

# Each variant: its folder under .cache/variants, and the models quantized in it.
QUANTIZED = {
    "triposr-int8-encoder": ("triposr-encoder",),
    "triposr-int8-decoder": ("triposr-decoder",),
}


def link_or_copy(source: Path, target: Path) -> None:
    """A hard link where the volume allows one, which costs no space, else a copy."""
    target.parent.mkdir(parents=True, exist_ok=True)
    if target.exists():
        target.unlink()
    try:
        os.link(source, target)
    except OSError:
        shutil.copyfile(source, target)


def quantize(source: Path, target: Path) -> None:
    """ONNX Runtime's dynamic INT8 quantization of MatMul and Gemm weights."""
    from onnxruntime.quantization import QuantType, quantize_dynamic

    target.parent.mkdir(parents=True, exist_ok=True)
    quantize_dynamic(str(source), str(target), weight_type=QuantType.QInt8, op_types_to_quantize=["MatMul", "Gemm"])


def build(models: Path = MODELS, variants: Path = VARIANTS, plan: dict[str, tuple[str, ...]] = QUANTIZED, quantizer=quantize) -> list[Path]:
    """Every variant folder in `plan`, each a full models folder for a 3D Object run."""
    missing = [m for m in OBJECT_MODELS if not (models / m / "fp32.onnx").is_file()]
    if missing:
        raise FileNotFoundError(f"{', '.join(missing)}: no fp32.onnx under {models}; run prepare-models.cmd first")
    made = []
    for name, quantized in plan.items():
        folder = variants / name
        for model in OBJECT_MODELS:
            link_or_copy(models / model / "meta.json", folder / model / "meta.json")
            if model in quantized:
                quantizer(models / model / "fp32.onnx", folder / model / "fp32.onnx")
            else:
                link_or_copy(models / model / "fp32.onnx", folder / model / "fp32.onnx")
        made.append(folder)
    return made


def install(models: Path = MODELS, variants: Path = VARIANTS, model: str = "triposr-encoder", folder: str = "triposr-int8-encoder") -> dict:
    """MOD116: the measured INT8 encoder into the models folder as int8.onnx, listed in its meta.json.

    The models folder is what build_manifest.py and publish_models.py stage, so the next publish
    carries the variant, and ModelLibrary finds it on disk as Light quality does. meta.json is
    replaced, not written through, since build() hard-links it into the variant folders.
    """
    source = variants / folder / model / "fp32.onnx"
    if not source.is_file():
        raise FileNotFoundError(f"{source}: no INT8 {model} yet; run this script without --install first")
    target = models / model / "int8.onnx"
    link_or_copy(source, target)
    digest = hashlib.sha256()
    with target.open("rb") as stream:
        for block in iter(lambda: stream.read(1 << 20), b""):
            digest.update(block)
    meta_path = models / model / "meta.json"
    meta = json.loads(meta_path.read_text(encoding="utf-8"))
    described = {"file": "int8.onnx", "bytes": target.stat().st_size, "sha256": digest.hexdigest()}
    meta.setdefault("variants", {})["int8"] = described
    staged = meta_path.with_name("meta.json.tmp")
    staged.write_text(json.dumps(meta, indent=2) + "\n", encoding="utf-8")
    os.replace(staged, meta_path)
    return described


def main() -> int:
    if "--install" in sys.argv[1:]:
        described = install()
        print(f"triposr-encoder int8.onnx: {described['bytes'] / 1e6:.0f} MB, listed in its meta.json")
        return 0

    for folder in build():
        sizes = {m: (folder / m / "fp32.onnx").stat().st_size for m in OBJECT_MODELS}
        print(f"{folder.name}: " + ", ".join(f"{m} {s / 1e6:.0f} MB" for m, s in sizes.items()))
    return 0


if __name__ == "__main__":
    sys.exit(main())
