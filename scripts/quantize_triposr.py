"""INT8 variants of TripoSR, to measure against fp32 (MOD115).

A public ONNX export of TripoSR ships an INT8 encoder of about 430 MB against 1.7 GB in fp32, with
its quality loss described and never measured. This makes the variants from the exported fp32
models with ONNX Runtime's dynamic quantization (weights to INT8, activations quantized per run, so
no calibration photos are needed) and lays each out as a models folder of its own, the quantized
file standing in as fp32.onnx and the other models hard-linked beside it, so modelado-cli runs it
with --models and scripts/compare_models.py measures it against fp32 on the same photos:

    .venv\\Scripts\\python scripts\\quantize_triposr.py
    .venv\\Scripts\\python scripts\\compare_models.py --no-gpu

Needs the fp32 models in .cache/models (prepare-models.cmd) and onnxruntime.
"""

from __future__ import annotations

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


def main() -> int:
    for folder in build():
        sizes = {m: (folder / m / "fp32.onnx").stat().st_size for m in OBJECT_MODELS}
        print(f"{folder.name}: " + ", ".join(f"{m} {s / 1e6:.0f} MB" for m, s in sizes.items()))
    return 0


if __name__ == "__main__":
    sys.exit(main())
