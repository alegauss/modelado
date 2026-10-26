"""Add an ONNX model export_models.py did not build, so build_manifest.py can publish it.

Writes <out>/<name>/fp32.onnx, fp16.onnx when given, and meta.json (default out: .cache/models),
the folder shape export_models.py leaves, so build_manifest.py and publish_models.py take the
model with no change.

    .venv\\Scripts\\python scripts\\add_model.py --name my-depth --task depth --onnx depth.onnx ^
        --license MIT --license-url https://example.org/LICENSE --source https://example.org/weights

The task's contract is the built-in model's own meta.json for that task, never retyped here: its
preprocessing and params are copied, and the model's inputs and outputs must have the same count,
rank and fixed dimensions, since the app's pipeline feeds it what it feeds the built-in. The
built-in's meta.json is read from --models when it was exported there, else downloaded through
the embedded manifest and checked by SHA-256. The names and shapes in the new meta.json are the
graph's own, which the app reads (MOD24).

Before anything is written, every variant passes onnx.checker and one ONNX Runtime run on random
inputs of the declared shapes, whose outputs must have the shapes the contract names.

The app takes each model by the name its pipeline ships (ReliefReconstruction.Depth, ...), so
today a custom model is used only under that name, in a repository given with --manifest.

Requirements: onnx, onnxruntime and numpy (scripts/requirements.txt).
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import shutil
import sys
import urllib.request
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
MODELS = REPO / ".cache" / "models"
EMBEDDED = REPO / "src" / "Modelado.Inference" / "models-manifest.json"
META_SCHEMA = 1
# What the app accepts as a model name: one folder, nothing that reads as a path (MOD57).
NAME = re.compile(r"^[a-z0-9][a-z0-9._-]*$")
# A dynamic dimension in the smoke run: small enough for the decoder's points and the upscaler.
DYNAMIC_SIZE = 64
# onnx.TensorProto.FLOAT: the app feeds every variant float32 tensors.
FLOAT = 1


class AddError(Exception):
    """The model cannot be added: a refusal naming what does not fit."""


def sha256_of(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as f:
        while chunk := f.read(1 << 20):
            digest.update(chunk)
    return digest.hexdigest()


def tasks(manifest: dict) -> dict[str, dict]:
    """The embedded manifest's entry for each task, by task."""
    return {model["task"]: model for model in manifest["models"]}


def template_for(task: str, manifest: dict, models: Path, fetch=urllib.request.urlopen) -> dict:
    """The built-in model's meta.json for `task`: exported locally, or downloaded and verified."""
    known = tasks(manifest)
    if task not in known:
        raise AddError(f"the app knows no task {task!r}; it knows {', '.join(sorted(known))}")
    entry = known[task]
    local = models / entry["name"] / "meta.json"
    if local.is_file():
        return json.loads(local.read_text(encoding="utf-8"))
    with fetch(entry["meta"]["url"]) as response:
        body = response.read()
    if hashlib.sha256(body).hexdigest() != entry["meta"]["sha256"]:
        raise AddError(f"{entry['meta']['url']} differs from the embedded manifest's SHA-256")
    return json.loads(body)


def check_contract(template: dict, graph: dict, label: str, ran: bool = False) -> None:
    """Refuse a graph whose tensors the task's pipeline could not feed or read.

    `ran` holds the shapes a run produced, where every axis is a number, so an axis the
    contract leaves dynamic is not refused for being one.
    """
    for kind in ("inputs", "outputs"):
        expected, got = template[kind], graph[kind]
        if len(expected) != len(got):
            raise AddError(f"{label} has {len(got)} {kind}; {template['task']} takes {len(expected)}")
        for index, (want, have) in enumerate(zip(expected, got)):
            where = f"{label} {kind[:-1]} {index} ({have['name']})"
            if len(want["shape"]) != len(have["shape"]):
                raise AddError(f"{where} has rank {len(have['shape'])}; {template['task']} expects {_shape(want['shape'])}")
            for axis, (w, h) in enumerate(zip(want["shape"], have["shape"])):
                if isinstance(w, int) and isinstance(h, int) and w != h:
                    raise AddError(f"{where} is {_shape(have['shape'])}; {template['task']} expects {_shape(want['shape'])}")
                if not ran and not isinstance(w, int) and isinstance(h, int):
                    raise AddError(f"{where} fixes axis {axis} at {h}; {template['task']} feeds it any size")
            if have.get("elem", FLOAT) != FLOAT:
                raise AddError(f"{where} is not float32, which is what the app feeds and reads")


def _shape(shape: list) -> str:
    return " x ".join(str(d) for d in shape)


def describe(path: Path) -> dict:
    """The graph's inputs and outputs: names, shapes (a dynamic axis as its name) and types."""
    import onnx

    try:
        graph = onnx.load(str(path), load_external_data=False).graph
    except Exception as error:  # protobuf's DecodeError and onnx's own, all of them a refusal here
        raise AddError(f"{path} is not an ONNX model onnx can read: {error}") from error
    initialisers = {i.name for i in graph.initializer}

    def tensors(values):
        found = []
        for value in values:
            if value.name in initialisers:
                continue
            kind = value.type.tensor_type
            shape = [d.dim_value if d.HasField("dim_value") else (d.dim_param or f"d{i}") for i, d in enumerate(kind.shape.dim)]
            found.append({"name": value.name, "shape": shape, "elem": kind.elem_type})
        return found

    return {"inputs": tensors(graph.input), "outputs": tensors(graph.output)}


def check_runs(path: Path, template: dict, graph: dict, label: str) -> None:
    """onnx.checker, then one ONNX Runtime run on random inputs, its outputs held to the contract."""
    import numpy as np
    import onnx
    import onnxruntime as ort

    try:
        onnx.checker.check_model(str(path), full_check=True)
    except onnx.checker.ValidationError as error:
        raise AddError(f"{label} fails onnx.checker: {error}") from error
    random = np.random.default_rng(0)
    feeds = {
        tensor["name"]: random.random([d if isinstance(d, int) else DYNAMIC_SIZE for d in tensor["shape"]], dtype=np.float32)
        for tensor in graph["inputs"]
    }
    try:
        session = ort.InferenceSession(str(path), providers=["CPUExecutionProvider"])
        results = session.run(None, feeds)
    except Exception as error:  # ONNX Runtime raises its own types, all of them a refusal here
        raise AddError(f"{label} does not run on inputs of its declared shapes: {error}") from error
    produced = {"inputs": graph["inputs"], "outputs": [{"name": t["name"], "shape": list(r.shape), "elem": FLOAT if r.dtype == np.float32 else 0} for t, r in zip(graph["outputs"], results)]}
    check_contract(template, produced, f"{label}'s run", ran=True)


def add(
    name: str,
    task: str,
    fp32: Path,
    fp16: Path | None,
    license_id: str,
    license_url: str,
    source: str,
    template: dict,
    out: Path = MODELS,
    replace: bool = False,
    run=check_runs,
    read=describe,
) -> dict:
    """Validate the files against the task's contract and write the model's folder and meta.json."""
    if not NAME.match(name):
        raise AddError(f"{name!r} is not a model name: lowercase letters, digits, '.', '_' and '-', with no path in it")
    folder = out / name
    if folder.exists() and not replace:
        raise AddError(f"{folder} already exists; pass --replace to write over it")
    variants = {"fp32": fp32, **({"fp16": fp16} if fp16 is not None else {})}
    graphs = {}
    for variant, path in variants.items():
        if not path.is_file():
            raise AddError(f"{path} is not there")
        label = f"{name} {variant}"
        graphs[variant] = read(path)
        check_contract(template, graphs[variant], label)
        run(path, template, graphs[variant], label)
    if fp16 is not None and [t["name"] for t in graphs["fp16"]["inputs"] + graphs["fp16"]["outputs"]] != [
        t["name"] for t in graphs["fp32"]["inputs"] + graphs["fp32"]["outputs"]
    ]:
        raise AddError(f"{name}: fp16's inputs and outputs are named differently from fp32's, and meta.json names one set")

    staging = out / f".{name}.adding"
    if staging.exists():
        shutil.rmtree(staging)
    staging.mkdir(parents=True)
    described = {}
    for variant, path in variants.items():
        target = staging / f"{variant}.onnx"
        shutil.copyfile(path, target)
        described[variant] = {"file": target.name, "bytes": target.stat().st_size, "sha256": sha256_of(target)}
    meta = {
        "schema": META_SCHEMA,
        "name": name,
        "task": task,
        "inputs": [{"name": t["name"], "shape": t["shape"]} for t in graphs["fp32"]["inputs"]],
        "outputs": [{"name": t["name"], "shape": t["shape"]} for t in graphs["fp32"]["outputs"]],
        "preprocess": template["preprocess"],
        "params": template["params"],
        "validation": {"added_by": "scripts/add_model.py", "contract": template["name"], "checked": "onnx.checker and one ONNX Runtime run on random inputs"},
        "variants": described,
        "upstream": {"url": source, "license": license_id, "license_url": license_url},
    }
    (staging / "meta.json").write_text(json.dumps(meta, indent=2) + "\n", encoding="utf-8", newline="\n")
    if folder.exists():
        shutil.rmtree(folder)
    staging.rename(folder)
    return meta


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Add a custom ONNX model beside the exported ones, ready to publish.")
    parser.add_argument("--name", required=True, help="the model's name, which is its folder and its file names when published")
    parser.add_argument("--task", required=True, help="the task it does, one the app knows (depth, background-removal, upscale, ...)")
    parser.add_argument("--onnx", type=Path, required=True, help="the fp32 ONNX file")
    parser.add_argument("--fp16", type=Path, help="an fp16 ONNX file, with float32 inputs and outputs")
    parser.add_argument("--license", required=True, help="the weights' license, as About shows it")
    parser.add_argument("--license-url", required=True, help="where the license text is")
    parser.add_argument("--source", required=True, help="where the weights came from")
    parser.add_argument("--models", type=Path, default=MODELS, help="where the model goes, and the exported built-ins are (default: .cache/models)")
    parser.add_argument("--replace", action="store_true", help="write over a model already there under this name")
    args = parser.parse_args(argv)
    try:
        manifest = json.loads(EMBEDDED.read_text(encoding="utf-8"))
        template = template_for(args.task, manifest, args.models)
        meta = add(args.name, args.task, args.onnx, args.fp16, args.license, args.license_url, args.source, template, args.models, args.replace)
    except AddError as error:
        print(f"ERROR {error}", file=sys.stderr)
        return 1
    print(f"{meta['name']}: {meta['task']} like {template['name']}, {', '.join(meta['variants'])} -> {args.models / meta['name']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
