"""Convert the upstream weights to the ONNX models Modelado runs, and check each against PyTorch.

Reads what download_models.py left in .cache/weights and writes, per model,
<out>/<name>/fp32.onnx, where it holds up fp16.onnx, and meta.json (default out: .cache/models,
git-ignored). Every file passes onnx.checker and is then run in ONNX Runtime on the model's own
test image against the PyTorch model; an export past its tolerance fails the script instead of
writing a model that is quietly wrong.

    triposr-encoder          1 x 3 x 512 x 512 RGB in [0, 1], the object already cut out,
                             centred and laid on grey 0.5; out the triplane 1 x 3 x 40 x 64 x 64
    triposr-decoder          the triplane and N x 3 points in [-0.87, 0.87]; out the density
                             (N) and the colour (N x 3, in [0, 1]); fp32 only, it is 0.2 MB
    depth-anything-v2-small  1 x 3 x 518 x 518 RGB in [0, 1], the photo stretched to it; out
                             the relative inverse depth 1 x 518 x 518 (larger is nearer)
    u2net                    rembg's own ONNX export, adopted once it finds the chair; 1 x 3 x
                             320 x 320, ImageNet-normalised; out the saliency map
    realesrgan-x4plus        the texture and photo upscaler: 1 x 3 x H x W RGB in [0, 1], any
                             size (the app tiles); out 1 x 3 x 4H x 4W in [0, 1]

    .venv\\Scripts\\python scripts\\export_models.py
    .venv\\Scripts\\python scripts\\export_models.py --only triposr-decoder --no-fp16

Requirements: scripts/requirements.txt.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import shutil
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Callable

import numpy as np
import onnx
import onnxruntime as ort
import torch
import torch.nn.functional as F
from onnxconverter_common import float16
from PIL import Image
from torch import nn

SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parent
WEIGHTS = REPO / ".cache" / "weights"
OUT = REPO / ".cache" / "models"
SOURCES = SCRIPTS / "model-sources.json"
OPSET = 17
META_SCHEMA = 1

IMAGENET_MEAN = (0.485, 0.456, 0.406)
IMAGENET_STD = (0.229, 0.224, 0.225)

# TripoSR's own values: config.yaml (radius) and tsr/system.py and run.py (the rest).
TRIPOSR_SIZE = 512
TRIPOSR_RADIUS = 0.87
TRIPOSR_THRESHOLD = 25.0
TRIPOSR_FOREGROUND_RATIO = 0.85
TRIPOSR_BACKGROUND = 0.5
# Points per decoder run the app starts from: 256^3 points take 64 runs, each about 30 MB.
DECODER_CHUNK = 262144

# The share of grid cells whose occupancy (density above the threshold) may differ from PyTorch's.
OCCUPANCY_FP32_MAX = 0.002
OCCUPANCY_FP16_MAX = 0.02
DEPTH_SIZE = 518
# Depth is compared after both maps are scaled to [0, 1], as the relief uses them.
DEPTH_FP32_MAX = 1e-3
DEPTH_FP16_MIN_PSNR = 35.0
# U-2-Net on the chair laid on white, against the chair's own alpha.
U2NET_MIN_IOU = 0.9


class ExportError(Exception):
    """An export did not produce a model that matches its PyTorch source."""


@dataclass(frozen=True)
class Spec:
    name: str
    task: str
    source: str
    fp16: bool
    export: Callable[["Spec", Path, Path, bool], dict]


# --- TripoSR -----------------------------------------------------------------------------------


def _triposr_modules():
    sys.path.insert(0, str(SCRIPTS / "archs"))
    from triposr.models.nerf_renderer import TriplaneNeRFRenderer
    from triposr.models.network_utils import NeRFMLP, TriplaneUpsampleNetwork
    from triposr.models.tokenizers.image import DINOSingleImageTokenizer
    from triposr.models.tokenizers.triplane import Triplane1DTokenizer
    from triposr.models.transformer.transformer_1d import Transformer1D

    return DINOSingleImageTokenizer, Triplane1DTokenizer, Transformer1D, TriplaneUpsampleNetwork, NeRFMLP, TriplaneNeRFRenderer


class TripoSR(nn.Module):
    """tsr/system.py's TSR without its preprocessing, rendering and mesh steps."""

    def __init__(self, weights: Path) -> None:
        super().__init__()
        from omegaconf import OmegaConf

        folder = weights / "triposr"
        for needed in ("model.ckpt", "config.yaml", "dino-vitb16-config.json"):
            if not (folder / needed).is_file():
                raise ExportError(f"{folder / needed} is missing; run scripts/download_models.py first")
        cfg = OmegaConf.load(folder / "config.yaml")
        OmegaConf.resolve(cfg)
        # transformers reads a config folder or file; the pinned config.json is renamed on disk.
        dino = folder / "dino-vitb16"
        dino.mkdir(exist_ok=True)
        shutil.copyfile(folder / "dino-vitb16-config.json", dino / "config.json")
        image_tok, tri_tok, backbone, post, mlp, renderer = _triposr_modules()
        self.image_tokenizer = image_tok({"pretrained_model_name_or_path": str(dino)})
        self.tokenizer = tri_tok(cfg.tokenizer)
        self.backbone = backbone(cfg.backbone)
        self.post_processor = post(cfg.post_processor)
        self.decoder = mlp(cfg.decoder)
        self.renderer = renderer(cfg.renderer)
        state = torch.load(folder / "model.ckpt", map_location="cpu", weights_only=True)
        self.load_state_dict(_vit_keys(state, self.state_dict().keys()), strict=True)
        if abs(float(cfg.renderer.radius) - TRIPOSR_RADIUS) > 1e-6:
            raise ExportError(f"config.yaml radius is {cfg.renderer.radius}, the app assumes {TRIPOSR_RADIUS}")
        self.eval()

    def scene_codes(self, image: torch.Tensor) -> torch.Tensor:
        """1 x 3 x 512 x 512 in [0, 1] to the triplane 1 x 3 x 40 x 64 x 64."""
        tokens_in = self.image_tokenizer(image[:, None])  # B x 1 x C x Nt
        tokens_in = tokens_in[:, 0].permute(0, 2, 1)  # B x Nt x C
        tokens = self.tokenizer(image.shape[0])
        tokens = self.backbone(tokens, encoder_hidden_states=tokens_in)
        return self.post_processor(self.tokenizer.detokenize(tokens))

    def query(self, triplane: torch.Tensor, points: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
        """The renderer's query_triplane for one scene: density (N) and colour (N x 3)."""
        out = self.renderer.query_triplane(self.decoder, points, triplane[0])
        return out["density_act"][..., 0], out["color"]


# transformers 5 renamed ViT's layers; the checkpoint was saved with 4.35's names.
_VIT_RENAMES = [
    (".encoder.layer.", ".layers."),
    (".attention.attention.query.", ".attention.q_proj."),
    (".attention.attention.key.", ".attention.k_proj."),
    (".attention.attention.value.", ".attention.v_proj."),
    (".attention.output.dense.", ".attention.o_proj."),
    (".intermediate.dense.", ".mlp.fc1."),
    (".output.dense.", ".mlp.fc2."),
]


def _vit_keys(state: dict, wanted) -> dict:
    """The checkpoint under the names this transformers uses, whichever version saved it."""
    wanted = set(wanted)
    renamed = {}
    for key, value in state.items():
        if key not in wanted and key.startswith("image_tokenizer.model.encoder."):
            for old, new in _VIT_RENAMES:
                key = key.replace(old, new)
        renamed[key] = value
    return renamed


class Encoder(nn.Module):
    def __init__(self, model: TripoSR) -> None:
        super().__init__()
        self.model = model

    def forward(self, image: torch.Tensor) -> torch.Tensor:
        return self.model.scene_codes(image)


class Decoder(nn.Module):
    """query_triplane written out for export, the same arithmetic in the same order.

    grid_sample takes each point's (x, y), (x, z) and (y, z) on the three planes, the features
    are concatenated plane by plane, and the MLP returns density and colour logits.
    """

    def __init__(self, model: TripoSR) -> None:
        super().__init__()
        self.decoder = model.decoder
        self.radius = float(model.renderer.cfg.radius)
        self.density_bias = float(model.renderer.cfg.density_bias)

    def forward(self, triplane: torch.Tensor, points: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor]:
        planes = triplane[0]  # 3 x C x H x W
        x = points / self.radius
        uv = torch.stack((x[:, [0, 1]], x[:, [0, 2]], x[:, [1, 2]]), dim=0)  # 3 x N x 2
        sampled = F.grid_sample(planes, uv[:, None], align_corners=False, mode="bilinear")  # 3 x C x 1 x N
        features = sampled[:, :, 0].permute(2, 0, 1).reshape(points.shape[0], -1)  # N x 3C
        out = self.decoder.layers(features)
        density = torch.exp(out[:, 0] + self.density_bias)
        color = torch.sigmoid(out[:, 1:4])
        return density, color


def composite_for_triposr(rgba: np.ndarray, ratio: float = TRIPOSR_FOREGROUND_RATIO) -> np.ndarray:
    """run.py's resize_foreground and grey composite, then the 512 resize: H x W x 4 uint8 to 1 x 3 x 512 x 512."""
    alpha = rgba[..., 3]
    ys, xs = np.nonzero(alpha > 0)
    if ys.size == 0:
        raise ExportError("the test image has no foreground")
    fg = rgba[ys.min() : ys.max(), xs.min() : xs.max()]
    side = max(fg.shape[0], fg.shape[1])
    square = np.zeros((side, side, 4), dtype=np.uint8)
    top, left = (side - fg.shape[0]) // 2, (side - fg.shape[1]) // 2
    square[top : top + fg.shape[0], left : left + fg.shape[1]] = fg
    padded_side = int(side / ratio)
    padded = np.zeros((padded_side, padded_side, 4), dtype=np.uint8)
    offset = (padded_side - side) // 2
    padded[offset : offset + side, offset : offset + side] = square
    image = padded.astype(np.float32) / 255.0
    rgb = image[..., :3] * image[..., 3:4] + (1 - image[..., 3:4]) * TRIPOSR_BACKGROUND
    tensor = torch.from_numpy(rgb).permute(2, 0, 1)[None]
    return F.interpolate(tensor, (TRIPOSR_SIZE, TRIPOSR_SIZE), mode="bilinear", align_corners=False, antialias=True).numpy()


def grid_points(resolution: int) -> np.ndarray:
    """isosurface.py's grid, scaled to the radius: resolution^3 x 3, x slowest."""
    axis = np.linspace(-TRIPOSR_RADIUS, TRIPOSR_RADIUS, resolution, dtype=np.float32)
    x, y, z = np.meshgrid(axis, axis, axis, indexing="ij")
    return np.stack([x.ravel(), y.ravel(), z.ravel()], axis=-1)


def _session(path: Path) -> ort.InferenceSession:
    return ort.InferenceSession(str(path), providers=["CPUExecutionProvider"])


def _decode(session: ort.InferenceSession, triplane: np.ndarray, points: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    densities, colors = [], []
    for start in range(0, len(points), DECODER_CHUNK):
        d, c = session.run(None, {"triplane": triplane, "points": points[start : start + DECODER_CHUNK]})
        densities.append(d)
        colors.append(c)
    return np.concatenate(densities), np.concatenate(colors)


def _occupancy_mismatch(a: np.ndarray, b: np.ndarray) -> float:
    return float(np.mean((a > TRIPOSR_THRESHOLD) != (b > TRIPOSR_THRESHOLD)))


_TRIPOSR: dict[Path, TripoSR] = {}


def _triposr(weights: Path) -> TripoSR:
    if weights not in _TRIPOSR:
        _TRIPOSR[weights] = TripoSR(weights)
    return _TRIPOSR[weights]


def _chair(weights: Path) -> np.ndarray:
    return composite_for_triposr(_rgba(weights / "test-images" / "chair.png"))


def export_encoder(spec: Spec, weights: Path, folder: Path, fp16: bool) -> dict:
    model = _triposr(weights)
    sample = torch.from_numpy(_chair(weights))
    target = folder / "fp32.onnx"
    torch.onnx.export(Encoder(model), (sample,), str(target), input_names=["image"], output_names=["triplane"], opset_version=OPSET, dynamo=False)
    onnx.checker.check_model(str(target), full_check=True)

    with torch.no_grad():
        expected = model.scene_codes(sample).numpy()
        points = torch.from_numpy(grid_points(64))
        density_torch = model.query(torch.from_numpy(expected), points)[0].numpy()
    decoder = _session(_ensure_decoder(spec, weights, folder.parent))
    validation = {"image": "chair.png", "grid": 64}
    for variant in ("fp32", "fp16") if fp16 else ("fp32",):
        path = folder / f"{variant}.onnx"
        if variant == "fp16":
            _to_fp16(target, path)
        got = _session(path).run(None, {"image": sample.numpy()})[0]
        error = float(np.max(np.abs(got - expected)) / max(1e-6, float(np.max(np.abs(expected)))))
        mismatch = _occupancy_mismatch(_decode(decoder, got, grid_points(64))[0], density_torch)
        limit = OCCUPANCY_FP32_MAX if variant == "fp32" else OCCUPANCY_FP16_MAX
        print(f"{spec.name} {variant}: triplane relative error {error:.2e}, occupancy differs in {mismatch:.3%} of cells (limit {limit:.1%})")
        if mismatch > limit:
            if variant == "fp32":
                raise ExportError(f"{spec.name}: fp32 occupancy differs from PyTorch in {mismatch:.3%} of cells")
            path.unlink()
            print(f"{spec.name}: fp16 dropped")
            continue
        validation[variant] = {"triplane_relative_error": round(error, 6), "occupancy_mismatch": round(mismatch, 6)}
    return {
        "inputs": [{"name": "image", "shape": [1, 3, TRIPOSR_SIZE, TRIPOSR_SIZE]}],
        "outputs": [{"name": "triplane", "shape": list(expected.shape)}],
        "preprocess": {
            "size": TRIPOSR_SIZE,
            "value_range": [0.0, 1.0],
            "background": TRIPOSR_BACKGROUND,
            "foreground_ratio": TRIPOSR_FOREGROUND_RATIO,
        },
        "params": {"radius": TRIPOSR_RADIUS, "density_threshold": TRIPOSR_THRESHOLD},
        "validation": validation,
    }


def _ensure_decoder(spec: Spec, weights: Path, out: Path) -> Path:
    path = out / "triposr-decoder" / "fp32.onnx"
    if not path.is_file():
        (out / "triposr-decoder").mkdir(parents=True, exist_ok=True)
        _export_decoder_file(_triposr(weights), path)
    return path


def _export_decoder_file(model: TripoSR, target: Path) -> None:
    triplane = torch.randn(1, 3, 40, 64, 64)
    points = torch.rand(1000, 3) * 2 * TRIPOSR_RADIUS - TRIPOSR_RADIUS
    torch.onnx.export(
        Decoder(model),
        (triplane, points),
        str(target),
        input_names=["triplane", "points"],
        output_names=["density", "color"],
        opset_version=OPSET,
        dynamo=False,
        dynamic_axes={"points": {0: "n"}, "density": {0: "n"}, "color": {0: "n"}},
    )
    onnx.checker.check_model(str(target), full_check=True)


def export_decoder(spec: Spec, weights: Path, folder: Path, fp16: bool) -> dict:
    model = _triposr(weights)
    target = folder / "fp32.onnx"
    _export_decoder_file(model, target)
    with torch.no_grad():
        triplane = model.scene_codes(torch.from_numpy(_chair(weights)))
        points = torch.from_numpy(grid_points(64))
        density, color = model.query(triplane, points)
    got_density, got_color = _decode(_session(target), triplane.numpy(), points.numpy())
    density_error = float(np.max(np.abs(got_density - density.numpy()) / (1 + np.abs(density.numpy()))))
    color_error = float(np.max(np.abs(got_color - color.numpy())))
    mismatch = _occupancy_mismatch(got_density, density.numpy())
    print(f"{spec.name} fp32: density error {density_error:.2e} (relative), colour {color_error:.2e}, occupancy differs in {mismatch:.3%}")
    if density_error > 1e-3 or color_error > 1e-3:
        raise ExportError(f"{spec.name}: differs from PyTorch (density {density_error:.2e}, colour {color_error:.2e})")
    return {
        "inputs": [{"name": "triplane", "shape": list(triplane.shape)}, {"name": "points", "shape": ["n", 3]}],
        "outputs": [{"name": "density", "shape": ["n"]}, {"name": "color", "shape": ["n", 3]}],
        "preprocess": {"value_range": [-TRIPOSR_RADIUS, TRIPOSR_RADIUS]},
        "params": {"radius": TRIPOSR_RADIUS, "density_threshold": TRIPOSR_THRESHOLD, "chunk": DECODER_CHUNK},
        "validation": {"image": "chair.png", "grid": 64, "fp32": {"density_error": round(density_error, 6), "color_error": round(color_error, 6)}},
    }


# --- Depth Anything V2 -------------------------------------------------------------------------


class Depth(nn.Module):
    """The model with its ImageNet normalisation inside, so the app feeds RGB in [0, 1]."""

    def __init__(self, net: nn.Module) -> None:
        super().__init__()
        self.net = net
        self.register_buffer("mean", torch.tensor(IMAGENET_MEAN).view(1, 3, 1, 1))
        self.register_buffer("std", torch.tensor(IMAGENET_STD).view(1, 3, 1, 1))

    def forward(self, image: torch.Tensor) -> torch.Tensor:
        return self.net(pixel_values=(image - self.mean) / self.std).predicted_depth


def _depth_model(weights: Path) -> Depth:
    from transformers import DepthAnythingForDepthEstimation

    folder = weights / "depth-anything-v2-small"
    for needed in ("model.safetensors", "config.json"):
        if not (folder / needed).is_file():
            raise ExportError(f"{folder / needed} is missing; run scripts/download_models.py first")
    # from_pretrained, not load_state_dict: it renames the checkpoint's layers to this
    # transformers' names, and reports any weight left unused.
    net, info = DepthAnythingForDepthEstimation.from_pretrained(str(folder), local_files_only=True, output_loading_info=True)
    if info["missing_keys"] or info["unexpected_keys"]:
        raise ExportError(f"depth-anything-v2-small: weights do not fit the model: {info}")
    return Depth(net).eval()


def depth_input(rgb: np.ndarray, side: int = DEPTH_SIZE) -> np.ndarray:
    """The image stretched to side x side, bicubic, as RGB in [0, 1]: 1 x 3 x side x side."""
    resized = np.asarray(Image.fromarray(rgb).resize((side, side), Image.BICUBIC), dtype=np.float32) / 255.0
    return resized.transpose(2, 0, 1)[None].copy()


def _normalised(depth: np.ndarray) -> np.ndarray:
    low, high = float(depth.min()), float(depth.max())
    return (depth - low) / max(1e-6, high - low)


def export_depth(spec: Spec, weights: Path, folder: Path, fp16: bool) -> dict:
    # A fixed square: the head resizes to the input's size with Python ints, which both the
    # tracer and torch.export bake into the graph, so a dynamic export answers every size with
    # the traced one. The app stretches the photo to the square and the depth back.
    model = _depth_model(weights)
    target = folder / "fp32.onnx"
    torch.onnx.export(
        model,
        (torch.rand(1, 3, DEPTH_SIZE, DEPTH_SIZE),),
        str(target),
        input_names=["image"],
        output_names=["depth"],
        opset_version=OPSET,
        dynamo=False,
    )
    onnx.checker.check_model(str(target), full_check=True)
    validation: dict = {"image": "captured.jpeg"}
    x = depth_input(np.asarray(Image.open(weights / "test-images" / "captured.jpeg").convert("RGB")))
    with torch.no_grad():
        expected = _normalised(model(torch.from_numpy(x)).numpy())
    for variant in ("fp32", "fp16") if fp16 else ("fp32",):
        path = folder / f"{variant}.onnx"
        if variant == "fp16":
            _to_fp16(target, path)
        got = _normalised(_session(path).run(None, {"image": x})[0])
        error = float(np.max(np.abs(got - expected)))
        psnr = float(10 * np.log10(1.0 / max(1e-12, float(np.mean((got - expected) ** 2)))))
        print(f"{spec.name} {variant}: max error {error:.2e}, PSNR {psnr:.1f} dB")
        if variant == "fp32" and error > DEPTH_FP32_MAX:
            raise ExportError(f"{spec.name}: fp32 differs from PyTorch by {error:.2e}")
        if variant == "fp16" and psnr < DEPTH_FP16_MIN_PSNR:
            path.unlink()
            print(f"{spec.name}: fp16 dropped, {psnr:.1f} dB")
            continue
        validation[variant] = {"max_error": round(error, 6), "psnr": round(psnr, 2)}
    return {
        "inputs": [{"name": "image", "shape": [1, 3, DEPTH_SIZE, DEPTH_SIZE]}],
        "outputs": [{"name": "depth", "shape": [1, DEPTH_SIZE, DEPTH_SIZE]}],
        "preprocess": {"size": DEPTH_SIZE, "value_range": [0.0, 1.0]},
        "params": {},
        "validation": validation,
    }


# --- U-2-Net -----------------------------------------------------------------------------------


def u2net_input(rgb: np.ndarray, size: int = 320) -> np.ndarray:
    """rembg's normalize(): LANCZOS to 320, divided by the image's maximum, then ImageNet's mean and std."""
    resized = np.asarray(Image.fromarray(rgb).resize((size, size), Image.LANCZOS), dtype=np.float32)
    resized = resized / max(1.0, float(resized.max()))
    mean = np.array(IMAGENET_MEAN, dtype=np.float32)
    std = np.array(IMAGENET_STD, dtype=np.float32)
    return ((resized - mean) / std).transpose(2, 0, 1)[None].astype(np.float32)


def u2net_mask(session: ort.InferenceSession, rgb: np.ndarray) -> np.ndarray:
    """rembg's U2netSession.predict: the first output, min-max scaled, resized back."""
    name = session.get_inputs()[0].name
    pred = session.run(None, {name: u2net_input(rgb)})[0][0, 0]
    pred = (pred - pred.min()) / max(1e-6, float(pred.max() - pred.min()))
    mask = Image.fromarray((pred * 255).astype(np.uint8)).resize((rgb.shape[1], rgb.shape[0]), Image.LANCZOS)
    return np.asarray(mask, dtype=np.float32) / 255.0


def export_u2net(spec: Spec, weights: Path, folder: Path, fp16: bool) -> dict:
    source = weights / "u2net.onnx"
    if not source.is_file():
        raise ExportError(f"{source} is missing; run scripts/download_models.py first")
    target = folder / "fp32.onnx"
    shutil.copyfile(source, target)
    onnx.checker.check_model(str(target), full_check=True)
    session = _session(target)
    chair = _rgba(weights / "test-images" / "chair.png").astype(np.float32) / 255.0
    on_white = (chair[..., :3] * chair[..., 3:4] + (1 - chair[..., 3:4])) * 255
    mask = u2net_mask(session, on_white.astype(np.uint8)) > 0.5
    truth = chair[..., 3] > 0.5
    iou = float(np.sum(mask & truth) / max(1, np.sum(mask | truth)))
    print(f"{spec.name} fp32: IoU with the chair's own alpha {iou:.3f} (floor {U2NET_MIN_IOU})")
    if iou < U2NET_MIN_IOU:
        raise ExportError(f"{spec.name}: finds the chair at IoU {iou:.3f}, below {U2NET_MIN_IOU}")
    graph = onnx.load(str(target), load_external_data=False).graph
    return {
        "inputs": [{"name": graph.input[0].name, "shape": [1, 3, 320, 320]}],
        "outputs": [{"name": graph.output[0].name, "shape": [1, 1, 320, 320]}],
        "preprocess": {"size": 320, "value_range": [0.0, 1.0], "scale_by_max": True, "mean": list(IMAGENET_MEAN), "std": list(IMAGENET_STD)},
        "params": {},
        "validation": {"image": "chair.png", "fp32": {"iou": round(iou, 4)}},
    }


# --- Real-ESRGAN ------------------------------------------------------------------------------

UPSCALE_TILE = 256
UPSCALE_FP32_MAX = 1e-3
UPSCALE_FP16_MIN_PSNR = 40.0


def _rrdbnet(weights: Path) -> nn.Module:
    sys.path.insert(0, str(SCRIPTS / "archs"))
    from rrdbnet import RRDBNet

    path = weights / "RealESRGAN_x4plus.pth"
    if not path.is_file():
        raise ExportError(f"{path} is missing; run scripts/download_models.py first")
    state = torch.load(path, map_location="cpu", weights_only=True)
    model = RRDBNet(scale=4)
    model.load_state_dict(state.get("params_ema", state), strict=True)
    return model.eval()


def export_upscaler(spec: Spec, weights: Path, folder: Path, fp16: bool) -> dict:
    model = _rrdbnet(weights)
    target = folder / "fp32.onnx"
    torch.onnx.export(
        model,
        (torch.rand(1, 3, 64, 64),),
        str(target),
        input_names=["image"],
        output_names=["upscaled"],
        opset_version=OPSET,
        dynamo=False,
        dynamic_axes={"image": {2: "height", 3: "width"}, "upscaled": {2: "out_height", 3: "out_width"}},
    )
    onnx.checker.check_model(str(target), full_check=True)
    photo = np.asarray(Image.open(weights / "test-images" / "0014.jpg").convert("RGB"), dtype=np.float32) / 255.0
    # A non-square crop, so the dynamic height and width are both exercised.
    x = photo[:96, :128].transpose(2, 0, 1)[None].copy()
    with torch.no_grad():
        expected = model(torch.from_numpy(x)).clamp(0, 1).numpy()
    validation: dict = {"image": "0014.jpg"}
    for variant in ("fp32", "fp16") if fp16 else ("fp32",):
        path = folder / f"{variant}.onnx"
        if variant == "fp16":
            _to_fp16(target, path)
        got = np.clip(_session(path).run(None, {"image": x})[0], 0, 1)
        error = float(np.max(np.abs(got - expected)))
        psnr = float(10 * np.log10(1.0 / max(1e-12, float(np.mean((got - expected) ** 2)))))
        print(f"{spec.name} {variant}: {got.shape[2]}x{got.shape[3]}, max error {error:.2e}, PSNR {psnr:.1f} dB")
        if variant == "fp32" and error > UPSCALE_FP32_MAX:
            raise ExportError(f"{spec.name}: fp32 differs from PyTorch by {error:.2e}")
        if variant == "fp16" and psnr < UPSCALE_FP16_MIN_PSNR:
            path.unlink()
            print(f"{spec.name}: fp16 dropped, {psnr:.1f} dB")
            continue
        validation[variant] = {"max_error": round(error, 6), "psnr": round(psnr, 2)}
    return {
        "inputs": [{"name": "image", "shape": [1, 3, "height", "width"]}],
        "outputs": [{"name": "upscaled", "shape": [1, 3, "out_height", "out_width"]}],
        "preprocess": {"value_range": [0.0, 1.0]},
        "params": {"scale": 4, "tile": UPSCALE_TILE},
        "validation": validation,
    }


# --- Common ------------------------------------------------------------------------------------


def _rgba(path: Path) -> np.ndarray:
    if not path.is_file():
        raise ExportError(f"{path} is missing; run scripts/download_models.py first")
    return np.asarray(Image.open(path).convert("RGBA"))


def _to_fp16(fp32: Path, target: Path) -> None:
    # The exporter computes attention's scale from the tensor's shape, through Casts to float the
    # converter leaves in place; ONNX Runtime's basic optimisations fold that chain to constants
    # first (standard ONNX ops only, nothing provider-specific).
    folded = target.with_suffix(".folded.onnx")
    options = ort.SessionOptions()
    options.graph_optimization_level = ort.GraphOptimizationLevel.ORT_ENABLE_BASIC
    options.optimized_model_filepath = str(folded)
    ort.InferenceSession(str(fp32), options, providers=["CPUExecutionProvider"])
    try:
        # Inputs and outputs stay float32, so the app feeds every variant the same tensors.
        converted = float16.convert_float_to_float16(onnx.load(str(folded)), keep_io_types=True)
    finally:
        folded.unlink(missing_ok=True)
    # The converter keeps the fp32 graph's value_info, which contradicts its own Casts; the
    # checker's shape inference rebuilds it.
    del converted.graph.value_info[:]
    onnx.save(converted, str(target))
    onnx.checker.check_model(str(target), full_check=True)


def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as f:
        while chunk := f.read(1 << 20):
            digest.update(chunk)
    return digest.hexdigest()


SPECS = [
    Spec("triposr-decoder", "triplane-decoder", "triposr", False, export_decoder),
    Spec("triposr-encoder", "image-to-triplane", "triposr", True, export_encoder),
    Spec("depth-anything-v2-small", "depth", "depth-anything-v2-small", True, export_depth),
    Spec("u2net", "background-removal", "u2net", False, export_u2net),
    Spec("realesrgan-x4plus", "upscale", "realesrgan-x4plus", True, export_upscaler),
]


def write_meta(spec: Spec, folder: Path, described: dict) -> dict:
    """meta.json, the contract scripts/meta.schema.json states and the app reads."""
    sources = {m["name"]: m for m in json.loads(SOURCES.read_text(encoding="utf-8"))["models"]}
    upstream = sources[spec.source]
    variants = {}
    for variant in ("fp32", "fp16"):
        path = folder / f"{variant}.onnx"
        if path.is_file():
            variants[variant] = {"file": path.name, "bytes": path.stat().st_size, "sha256": sha256_of(path)}
    meta = {
        "schema": META_SCHEMA,
        "name": spec.name,
        "task": spec.task,
        **described,
        "variants": variants,
        "upstream": {"url": upstream["url"], "license": upstream["license"], "license_url": upstream["license_url"]},
    }
    (folder / "meta.json").write_text(json.dumps(meta, indent=2) + "\n", encoding="utf-8", newline="\n")
    return meta


def export(spec: Spec, weights: Path, out: Path, fp16: bool = True) -> dict:
    folder = out / spec.name
    if folder.exists():
        shutil.rmtree(folder)
    folder.mkdir(parents=True)
    try:
        described = spec.export(spec, weights, folder, fp16 and spec.fp16)
    except Exception:
        shutil.rmtree(folder, ignore_errors=True)
        raise
    return write_meta(spec, folder, described)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Convert the upstream weights to Modelado's ONNX models.")
    parser.add_argument("--weights", type=Path, default=WEIGHTS, help="download_models.py's folder (default: .cache/weights)")
    parser.add_argument("--out", type=Path, default=OUT, help="where the models go (default: .cache/models)")
    parser.add_argument("--only", action="append", default=[], metavar="NAME", help="export just this model; repeatable")
    parser.add_argument("--no-fp16", action="store_true", help="write fp32 only")
    args = parser.parse_args(argv)
    known = [s.name for s in SPECS]
    unknown = [n for n in args.only if n not in known]
    if unknown:
        print(f"ERROR unknown model(s): {', '.join(unknown)}; known: {', '.join(known)}", file=sys.stderr)
        return 2
    failed = []
    for spec in SPECS:
        if args.only and spec.name not in args.only:
            continue
        try:
            meta = export(spec, args.weights, args.out, fp16=not args.no_fp16)
            print(f"{spec.name}: {', '.join(meta['variants'])} -> {args.out / spec.name}")
        except ExportError as error:
            print(f"ERROR {error}", file=sys.stderr)
            failed.append(spec.name)
    if failed:
        print(f"{len(failed)} failed: {', '.join(failed)}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
