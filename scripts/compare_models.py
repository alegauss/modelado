"""Compare models side by side on the same photos (MOD127).

Every model decision ahead (an INT8 encoder, Depth Anything 3, SF3D, SPAR3D, TripoSG) needs the
same measurement, so this runs a fixed set of photos, the ones the site already publishes, through
each model and variant with modelado-cli and records, per run: time, peak memory, where it ran,
triangles, whether the mesh is closed, and its Chamfer distance to the reference run of the same
photo (the fp32 model on the CPU), as a share of the reference's size. Each run also saves a
render (modelado-cli --preview), and the table is written into docs/local-3d-models.md between
its markers, so a new candidate is one more entry in RUNS and one command.

    .venv\\Scripts\\python scripts\\compare_models.py [--no-gpu] [--out .cache\\compare]

Needs the models downloaded (prepare-models.cmd or modelado-cli download) and, for the GPU rows, a
DirectX 12 GPU. numpy is the only package it uses.
"""

from __future__ import annotations

import argparse
import json
import struct
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

import numpy as np

REPO = Path(__file__).resolve().parent.parent
CLI = REPO / "tools" / "Modelado.Cli" / "bin" / "Release" / "net10.0" / "modelado-cli.exe"
DOC = REPO / "docs" / "local-3d-models.md"
START, END = "<!-- compare:start -->", "<!-- compare:end -->"

OBJECT_PHOTOS = ["docs/robot.png", "site/public/demo/chair.png", "site/public/demo/studio/object-hamburger.png"]
RELIEF_PHOTOS = ["site/public/demo/studio/relief-building.jpg", "site/public/demo/studio/relief-face.jpg", "site/public/demo/studio/relief-landscape.jpg"]


@dataclass(frozen=True)
class Run:
    """One model and variant: the first of a pipeline is the reference the others are measured against."""

    pipeline: str
    label: str
    args: tuple[str, ...]
    gpu: bool = False


RUNS = [
    Run("object", "TripoSR fp32, CPU", ("--provider", "cpu")),
    Run("object", "TripoSR fp16, GPU", ("--provider", "dml"), gpu=True),
    Run("object", "TripoSR fp32, GPU", ("--provider", "dml", "--high"), gpu=True),
    Run("relief", "Depth Anything V2 Small fp32, CPU", ("--provider", "cpu")),
    Run("relief", "Depth Anything V2 Small fp16, GPU", ("--provider", "dml"), gpu=True),
]


def read_stl(path: Path) -> np.ndarray:
    """A binary STL's triangles, shape (n, 3, 3)."""
    data = path.read_bytes()
    (count,) = struct.unpack_from("<I", data, 80)
    records = np.frombuffer(data, dtype=np.dtype([("normal", "<f4", 3), ("v", "<f4", (3, 3)), ("attr", "<u2")]), count=count, offset=84)
    return np.array(records["v"], dtype=np.float64)


def weld(triangles: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """Vertices shared by position (to a millionth of the size) and triangles indexing them."""
    points = triangles.reshape(-1, 3)
    size = float(np.linalg.norm(points.max(axis=0) - points.min(axis=0))) or 1.0
    keys = np.round(points / (size * 1e-6)).astype(np.int64)
    unique, index = np.unique(keys, axis=0, return_inverse=True)
    vertices = np.zeros((len(unique), 3))
    vertices[index.ravel()] = points
    return vertices, index.reshape(-1, 3)


def closed(triangles: np.ndarray) -> bool:
    """Whether every edge is used by exactly two triangles."""
    _, faces = weld(triangles)
    edges = np.sort(np.concatenate([faces[:, [0, 1]], faces[:, [1, 2]], faces[:, [2, 0]]]), axis=1)
    _, uses = np.unique(edges, axis=0, return_counts=True)
    return bool(len(uses)) and bool(np.all(uses == 2))


def sample(triangles: np.ndarray, count: int = 4000, seed: int = 7) -> np.ndarray:
    """Points spread over the surface by area, the same for the same mesh."""
    a, b, c = triangles[:, 0], triangles[:, 1], triangles[:, 2]
    area = np.linalg.norm(np.cross(b - a, c - a), axis=1)
    rng = np.random.default_rng(seed)
    chosen = rng.choice(len(triangles), size=count, p=area / area.sum())
    u, v = rng.random(count), rng.random(count)
    flip = u + v > 1
    u[flip], v[flip] = 1 - u[flip], 1 - v[flip]
    return a[chosen] + u[:, None] * (b[chosen] - a[chosen]) + v[:, None] * (c[chosen] - a[chosen])


def nearest(points: np.ndarray, to: np.ndarray, chunk: int = 512) -> np.ndarray:
    """Each point's distance to the nearest of `to`."""
    out = np.empty(len(points))
    for i in range(0, len(points), chunk):
        block = points[i : i + chunk]
        out[i : i + chunk] = np.sqrt(((block[:, None, :] - to[None, :, :]) ** 2).sum(axis=2).min(axis=1))
    return out


def chamfer(mesh: np.ndarray, reference: np.ndarray) -> float:
    """The symmetric mean nearest distance between the two surfaces, as a percentage of the reference's size."""
    p, q = sample(mesh), sample(reference)
    size = float(np.linalg.norm(q.max(axis=0) - q.min(axis=0))) or 1.0
    return 100 * (nearest(p, q).mean() + nearest(q, p).mean()) / 2 / size


def run_cli(run: Run, photo: Path, out: Path) -> dict:
    """One modelado-cli run: its JSON report, where it ran, and the STL and render it wrote."""
    stem = f"{photo.stem}-{run.label.lower().replace(' ', '-').replace(',', '')}"
    stl, preview = out / f"{stem}.stl", out / f"{stem}.png"
    done = subprocess.run(
        [str(CLI), run.pipeline, str(photo), "-o", str(stl), "--preview", str(preview), "--json", *run.args],
        capture_output=True, text=True, encoding="utf-8", errors="replace", check=False,
    )
    if done.returncode != 0:
        raise RuntimeError(f"{run.label} on {photo.name} failed: {done.stderr.strip() or done.stdout.strip()}")
    report = json.loads(done.stdout)
    where = next((line[len("Running on "):] for line in done.stderr.splitlines() if line.startswith("Running on ")), "?")
    if report.get("warnings"):
        where += " (fell back to the CPU)"
    return {"report": report, "where": where, "stl": stl, "preview": preview}


def measure(runs: list[Run], photos: dict[str, list[str]], out: Path, gpu: bool) -> list[dict]:
    """Every run on every photo of its pipeline, each compared with that photo's reference run."""
    out.mkdir(parents=True, exist_ok=True)
    rows = []
    for pipeline, names in photos.items():
        mine = [r for r in runs if r.pipeline == pipeline and (gpu or not r.gpu)]
        for name in names:
            photo = REPO / name
            reference = None
            for run in mine:
                result = run_cli(run, photo, out)
                triangles = read_stl(result["stl"])
                reference = triangles if reference is None else reference
                rows.append(
                    {
                        "photo": photo.name,
                        "model": run.label,
                        "where": result["where"],
                        "seconds": result["report"]["seconds"],
                        "peak_mb": result["report"].get("peakWorkingSetMb"),
                        "triangles": len(triangles),
                        "closed": closed(triangles),
                        "chamfer": None if triangles is reference else chamfer(triangles, reference),
                        "render": result["preview"].name,
                    }
                )
                print(f"  {photo.name:28} {run.label:36} {result['report']['seconds']:6.1f} s", flush=True)
    return rows


def table(rows: list[dict]) -> str:
    """The rows as the Markdown table docs/local-3d-models.md carries."""
    lines = [
        "| Photo | Model | Ran on | Time | Peak RAM | Triangles | Closed | Chamfer to the reference | Render |",
        "| --- | --- | --- | --: | --: | --: | --- | --: | --- |",
    ]
    for r in rows:
        peak = f"{r['peak_mb'] / 1024:.1f} GB" if r["peak_mb"] else "?"
        distance = "reference" if r["chamfer"] is None else f"{r['chamfer']:.2f}%"
        lines.append(
            f"| {r['photo']} | {r['model']} | {r['where']} | {r['seconds']:.1f} s | {peak} | {r['triangles']:,} | "
            f"{'yes' if r['closed'] else 'no'} | {distance} | {r['render']} |"
        )
    return "\n".join(lines)


def write_doc(doc: Path, markdown: str) -> None:
    """Puts the table between the document's markers, adding them at the end the first time."""
    text = doc.read_text(encoding="utf-8")
    block = f"{START}\n{markdown}\n{END}"
    if START in text and END in text:
        text = text[: text.index(START)] + block + text[text.index(END) + len(END) :]
    else:
        text = text.rstrip("\n") + "\n\n" + block + "\n"
    doc.write_text(text, encoding="utf-8", newline="\n")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Run each model on the same photos and compare them.")
    parser.add_argument("--no-gpu", action="store_true", help="the CPU runs only")
    parser.add_argument("--out", type=Path, default=REPO / ".cache" / "compare", help="where the meshes and renders go")
    parser.add_argument("--doc", type=Path, default=DOC, help="the document whose table is replaced")
    args = parser.parse_args(argv)
    # Built each time, so a table never measures an older CLI than the tree holds.
    subprocess.run(["dotnet", "build", str(REPO / "tools" / "Modelado.Cli" / "Modelado.Cli.csproj"), "-c", "Release", "--nologo", "-v", "quiet"], check=True)
    rows = measure(RUNS, {"object": OBJECT_PHOTOS, "relief": RELIEF_PHOTOS}, args.out, gpu=not args.no_gpu)
    write_doc(args.doc, table(rows))
    print(f"{len(rows)} runs -> {args.doc}, renders in {args.out}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
