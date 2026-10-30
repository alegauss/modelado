# Modelado

Modelado is a Windows app that turns **photos of objects into 3D models** with AI models running
on your own computer. It is a sibling of [PhotoBoost](https://github.com/Japode/photo-boost):
the same architecture (models converted to ONNX by Python scripts, published on Hugging Face
with pinned SHA-256 digests, downloaded by the app on first use and run with ONNX Runtime on the
GPU or the CPU), but instead of improving the image it makes geometry. No photo leaves the PC.

| Operation | Models | Result |
| --- | --- | --- |
| **3D Object** | U²-Net (cut-out) → TripoSR (encoder + decoder) → marching cubes | A closed mesh with front, sides and back, coloured per vertex |
| **Relief** | Depth Anything V2 Small (+ optional U²-Net) | A bas-relief textured with the photo; with a solid base, ready to print |
| **Edit and improve** | Real-ESRGAN x4plus + geometry tools | Any generated or opened model (GLB, OBJ, PLY, STL): simplify, subdivide, smooth, fill holes, flat base, turn, mirror, 4× sharper texture, photo colours |

The result appears in a 3D viewer (drag to turn, mouse wheel to zoom, double-click to recentre;
colour, clay or edges) and is saved as **GLB** (binary glTF: Windows 3D Viewer, Blender, the
web), **OBJ** (with .mtl and texture, or vertex colours), **STL** (3D printing, in millimetres)
or **PLY** (with colours: MeshLab, CloudCompare).

A GLB carries a photo texture as JPEG (PNG where it has transparency) and, under 65,535 vertices,
16-bit indices. The Export card's **Compact** choice (`--compact` on the command line) also
quantizes the geometry (KHR_mesh_quantization), about half the size: the web and Blender read it,
while older viewers that lack the extension refuse the file, so **Standard** stays the default.

![The robot from TripoSR's examples, reconstructed by Modelado](docs/robot.png)

## Edit and improve

The **Edit and improve** card works on the model shown: the one just generated, or a 3D model
opened with **Open…** (GLB with textures, colours and node hierarchy; OBJ with .mtl and texture
or vertex colours; ASCII or binary PLY with colours; ASCII or binary STL). Every step is a new
version: **Undo** (Ctrl+Z) and **Redo** (Ctrl+Y) move between them.

| Tool | What it does |
| --- | --- |
| Simplify | Reduces the triangles to the chosen share by edge collapse with quadric error metrics (Garland-Heckbert), keeping borders, never flipping a triangle and keeping a closed mesh closed. TripoSR's robot goes from 151 thousand to 22 thousand triangles in 0.8 s, still closed |
| Subdivide | 4× the triangles, rounded off: a smoother surface for printing large |
| Smooth | Taubin λ\|μ: removes jagged edges and noise without shrinking the model |
| Fill holes | Caps each hole, leaving the mesh watertight; on a relief without a base the outer border stays open |
| Remove loose pieces | Deletes fragments smaller than 5 % of the largest part |
| Flat base | Flattens the bottom 3 %, so the model stands on the printer bed |
| Turn and mirror | 90° about X, Y or Z (for imported models lying down) and a left-right mirror |
| **4× sharper texture (AI)** | Real-ESRGAN x4plus (BSD-3-Clause) upscales and cleans the texture of reliefs and opened models, in overlapping tiles (no seams), up to 4096 px |
| **Enhance a small photo (AI)** | In 3D Object: a photo under 1024 px goes through Real-ESRGAN before the cut-out and the reconstruction |
| **Sharp photo colours** | TripoSR colours from a 64 × 64 triplane, which blurs eyes, lettering and edges. Modelado projects the photo itself back onto the object's front (fitting the mesh's outline to the object's outline in the photo, only where the surface is visible and faces the camera, blending smoothly into the model's colours on the sides). On by default; **Reapply the photo's colours** does it again after editing |

On AI that edits meshes from an instruction ("make it taller", "change the material"): the models
that do this today are large diffusion models that do not run locally in ONNX and mostly have no
open licence for commercial use. So editing here is geometric, and AI comes in where it truly
improves the result: the texture, the input photo and the colours.

From the command line:

```
modelado-cli edit robot.glb -o robot.stl --simplify 0.15 --fill-holes --flatten 0.03 --size 100
modelado-cli edit relief.glb -o relief-sharp.glb --upscale-texture
modelado-cli object photo.jpg -o object.glb --enhance
modelado-cli cloud image-to-3d photo.jpg --estimate
modelado-cli cloud image-to-3d photo.jpg -o meshy.glb
modelado-cli cloud texture "weathered oak planks" -o oak.png
modelado-cli cloud remesh model.glb -o clean.glb --polycount 20000
modelado-cli cloud retexture model.glb --prompt "rusted iron" -o rust.glb --keep-uv
```

`cloud` uses the Meshy AI key stored in the window's About, or one stored with
`modelado-cli cloud key meshy`, which reads it from standard input. `--estimate` prints the
window's consent line (what is sent, to whom, for how much, and what the month's budget leaves)
and sends nothing. A job sent this way is checked against the same budgets and recorded in the
same ledger, so it shows in About. `texture` draws a seamless tile with Ideogram (an Ideogram
key); `remesh` and `retexture` send a model to Meshy AI. The Blender add-on uses these commands
for "3D Object with Meshy AI" and, in its AI group, for a new texture from a prompt, Remesh and
Retexture, each shown only when About holds that service's key.

The add-on's **Edit in Modelado** opens the selected mesh in the Modelado window (its path is in
the add-on's preferences), with the object's name in the caption. There, **Send to Blender** on
the toolbar puts the version shown back on the same object, keeping its transform and materials,
as one step of Blender's undo; send as often as you like, until the window is closed. The other
way, the window's Export card offers **Open in Blender** wherever Blender is found
(`MODELADO_BLENDER`, then the program .blend files open with, then the usual install folders): it
starts Blender with the model shown imported into an empty scene.

## Requirements

- **Windows 10 (1903+) or 11, x64.**
- **A DirectX 12 GPU** to run on the graphics card through DirectML (NVIDIA, AMD or Intel).
  Without one everything runs on the CPU, more slowly; if a model fails on the GPU (out of video
  memory, for example) it is run again on the CPU and the status bar says so.
- **Memory:** TripoSR's encoder is the heavy model: 0.8 GB in FP16 (the GPU default) or 1.7 GB
  in FP32. Allow about 4 GB of free RAM or video memory for 3D Object.
- **Disk:** 1.1 GB of models for 3D Object at standard quality (2 GB at high), 50 to 100 MB for
  Relief and 34 to 67 MB for Real-ESRGAN, downloaded only when an AI enhancement is used.

## Using it

```
dotnet build Modelado.slnx
.\run.cmd
.\run.cmd --open photo.jpg --provider cpu
```

Needs the **.NET 10 SDK** (`global.json`). Open a photo (or drop it on the window) and choose
**Generate 3D object** or **Generate relief**. On first use the app shows how much it will
download, from where and under which licence; the download can be paused and resumed, and each
file is checked by SHA-256 before it is used. **About and models** lists what is installed,
downloads everything for offline use or deletes what was downloaded.

Tips for a good 3D Object: a single, whole object, centred, on a plain background, seen from
slightly above. A PNG already cut out (with transparency) is used as it is. TripoSR invents the
sides the photo does not show, so the back of an asymmetric object comes out approximate.

**Cloud services** are optional. A Meshy AI key stored in **About** (kept encrypted for your
Windows user) brings a **Cloud 3D** card: image or text to 3D through Meshy, priced on its
button, sent only after the card says what goes and for how many credits, and returned as a
version in the history like a local result. Without a key nothing reaches a cloud service.

The app's command-line options: `--open <photo>`, `--models <folder>`,
`--provider auto|dml|cpu`, `--quality standard|high`, `--manifest <url>`, `--releases <url>|off`.

Once after it opens and then once a day, the window asks GitHub's release API whether a newer
release exists, sending only its name and version, and says so in the status bar. **About**
checks by hand and turns the automatic check off; `--releases off` skips it for one run. Its
**Update and restart** downloads the installer, refuses it unless it matches the release's
`SHA256SUMS`, runs it silently over the installed copy and starts Modelado again. It never runs
by itself, and it asks first when the model shown has not been saved.

### The installer

```
build\build-installer.cmd
powershell -NoProfile -File build\check-installer-vm.ps1
```

`build-installer.cmd` publishes the app and `modelado-cli` self-contained for win-x64, collects
the packages' licences and compiles `build\installer.iss` with Inno Setup 6 into
`artifacts\installer\Modelado-Setup-<version>.exe`. It installs per user, without administrator
rights, into `%LOCALAPPDATA%\Programs\Modelado`, with a Start menu entry and **Open with
Modelado** for .glb, .obj, .ply and .stl. Models are not bundled. A newer installer upgrades in
place and keeps the downloaded models; uninstalling asks whether to delete them.
`check-installer-vm.ps1` installs, upgrades, opens a model and uninstalls on the VMware guest.

### A release

```
build\update-release 0.2.0          sets the version, runs the suite on the guest, builds and tags
build\update-release 0.2.0 push     and pushes the branch and the tag
```

Pushing the tag `v0.2.0` runs `.github\workflows\release.yml`, which builds again on a clean
Windows runner and publishes a GitHub release holding `Modelado-Setup-0.2.0.exe` and
`SHA256SUMS`. `build\release-files.ps1`, which both paths run, refuses a tag whose version
disagrees with `Directory.Build.props`, the built exe or the installer's name. The installer is not
code-signed yet, so SmartScreen warns; the sums file is what to check it against.

### Where the models live

The app looks for each model in order:

1. the `models` folder beside the executable;
2. `%LOCALAPPDATA%\Modelado\models`, where downloads go.

`--models <folder>` replaces both. `run.cmd` uses `.cache\models` when it exists, which is where
`prepare-models.cmd` writes. No model is kept in the repository.

### Command line

`tools\Modelado.Cli` runs the same pipelines without a window, for batches and scripts (and on
Linux or macOS, on the CPU):

```
dotnet run --project tools\Modelado.Cli -c Release -- object chair.png -o chair.glb --preview view.png
dotnet run --project tools\Modelado.Cli -c Release -- relief portrait.jpg -o portrait.stl --base 0.05 --size 100
dotnet run --project tools\Modelado.Cli -c Release -- download all
```

`object` takes `--resolution 128..384`, `--threshold`, `--keep-background`, `--step`,
`--smooth`; `relief` takes `--grid`, `--depth`, `--base`, `--remove-background`. Common to all:
`--provider`, `--high`, `--models`, `--manifest`, `--size` (longest side when saving).

## Preparing the models (Windows, Python) and publishing them on Hugging Face

The models are downloaded from their original sources, converted to ONNX and checked against
PyTorch by Python scripts. Tested with **Python 3.13**; CPU PyTorch is enough, no CUDA needed.
One command does it all:

```
prepare-models.cmd
prepare-models.cmd --publish D:\Keys\hf.txt
```

What it does, step by step (each step can be run on its own):

1. **Environment:** creates `.venv` and installs `scripts\requirements.txt`.

   ```
   python -m venv .venv
   .venv\Scripts\python -m pip install torch==2.14.1 --index-url https://download.pytorch.org/whl/cpu
   .venv\Scripts\python -m pip install -r scripts\requirements.txt
   ```

2. **Download the weights** (about 2 GB, into `.cache\weights`): standard library only. Each
   file is pinned to a commit in `scripts\model-sources.json`, with its size and SHA-256; an
   interrupted download resumes where it stopped, and a file already correct is not fetched
   again.

   ```
   .venv\Scripts\python scripts\download_models.py
   ```

3. **Convert to ONNX** (into `.cache\models`):

   ```
   .venv\Scripts\python scripts\export_models.py
   .venv\Scripts\python scripts\export_models.py --only depth-anything-v2-small
   ```

   For each model it writes `fp32.onnx`, `fp16.onnx` when that holds up, and a `meta.json` the
   app reads. TripoSR is split in two: the **encoder** (512 × 512 photo → 3 × 40 × 64 × 64
   triplane) and the **decoder** (triplane + points → density and colour), and the app extracts
   the mesh with marching cubes. Each export runs in ONNX Runtime against PyTorch on the model's
   own test image; one that diverges beyond the tolerance fails the script instead of writing a
   wrong model. Measured here:

   | Model | Variant | Compared with PyTorch |
   | --- | --- | --- |
   | triposr-encoder | FP32 | triplane relative error 2·10⁻⁶; identical grid occupancy |
   | triposr-encoder | FP16 | relative error 1·10⁻³; identical grid occupancy |
   | triposr-decoder | FP32 | density 9·10⁻⁵ (relative), colour 3·10⁻⁵ |
   | depth-anything-v2-small | FP32 / FP16 | 120 dB / 71.6 dB PSNR on the normalised depth |
   | u2net | FP32 (rembg's export) | IoU 0.979 with the original chair cut-out |
   | realesrgan-x4plus | FP32 / FP16 | 120 dB / 75.9 dB PSNR, on a rectangular input (dynamic height and width) |

   **A model of your own** (a fine-tune, another upscaler, something you converted) goes in
   beside them with `add_model.py`, which `build_manifest.py` and `publish_models.py` then take
   like any other:

   ```
   .venv\Scripts\python scripts\add_model.py --name my-depth --task depth --onnx depth.onnx ^
       --license MIT --license-url https://example.org/LICENSE --source https://example.org/weights
   ```

   The task's contract is the built-in model's own `meta.json` for that task: its preprocessing
   and params are copied, and the ONNX inputs and outputs must match it in count, rank and
   fixed sizes. Each file passes `onnx.checker` and one ONNX Runtime run before anything is
   written. The app takes each model by the name its operation ships, so today a custom model is
   used under that name (`depth-anything-v2-small` for Relief), from a repository given with
   `--manifest`.

4. **Publish** to [alegauss/modelado-models](https://huggingface.co/alegauss/modelado-models)
   with a Hugging Face write token (huggingface.co/settings/tokens), read from a file or from
   `HF_TOKEN` and never printed:

   ```
   .venv\Scripts\python scripts\publish_models.py --dry-run
   .venv\Scripts\python scripts\publish_models.py --token-file D:\Keys\hf.txt
   .venv\Scripts\python scripts\publish_models.py --token-file D:\Keys\hf.txt --repo user/other-repo
   ```

   It makes two commits: first the files (`<model>-<variant>.onnx`, `<model>-meta.json`) and a
   model card with each one's licence; then `manifest.json`, with URLs pinned to
   `resolve/<commit 1>/`, so no later upload can change a file the app checks by SHA-256. The
   pinned manifest is copied to `src\Modelado.Inference\models-manifest.json`: rebuild the app
   and it knows each file's size and hash without the network. While that file is empty, the app
   reads the repository's latest manifest.

   Before that copy, the script reads the repository back: every file the pinned manifest lists
   is checked by size and SHA-256 (Hugging Face's `X-Linked-Etag` for an LFS file, a download
   for the rest), and a difference names the file and leaves the embedded manifest as it was.
   The same check runs on its own at any time, with nothing to upload:

   ```
   python scripts\verify_published.py
   python scripts\verify_published.py --repo user/other-repo --revision <commit>
   ```

   A model whose licence is not MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause or CC-BY-4.0 is
   refused before anything is staged, dry runs included, naming the model and its licence;
   `--allow-license "<licence>" --reason "<why>"` lets it through for that run and writes the
   reason into the model card.

   `--model <name>` (repeatable, with `--repo` of your own) publishes those models instead of
   the built-ins, and prints the manifest URL to give the app with `--manifest`; it leaves the
   embedded manifest and `scripts\published-models.json` alone.

## The site

`site\` is the public site, a Vite + React workspace of its own that the .NET build and the tests
never touch; [site/README.md](site/README.md) is its map, and `npm run build` there is its gate.

## Tests

```
run-tests-vm.cmd
```

The tests run on a VMware Workstation guest, not on the desk you work at: the UI cases open real
windows and take the foreground. `run-tests-vm.cmd` carries the working tree, uncommitted files
included, to the guest through winwright's runner (`D:\Git\alegauss\winwright`, or `WINWRIGHT_HOME`), runs
`run-tests.cmd` there and brings the verdict back; `-Screenshot` also brings a picture of the
guest's desk. The guest's credentials are winwright's, in `d:\tmp\winwright-vm.env`.

`run-tests.cmd` is what runs on the guest. It builds the solution with warnings as errors and the
performance analysers on, then runs the tests of each project in `tests\`: the libraries' tests,
an architecture test that keeps every colour in `Brand.cs`, and the winwright UI cases in
`cases\*.cases.json`, which drive the window through its accessibility tree and skip, saying why,
on a desk that cannot be observed. A test that hangs is ended after ten minutes and named, with a
dump in `TestResults\reports`. Then it runs the Python tests in `scripts\tests`. The tests with the
real models (the chair becomes a closed mesh facing forward; a photo becomes a solid relief) run
when `.cache\models` exists and say they skipped when it does not. On Linux or macOS,
`./run-tests.sh` runs everything but the WPF app and its cases, which are only built there.

```
run-live-vm.cmd
```

The UI cases on the real models live in `cases-live\*.cases.json` and are not part of that gate,
since the tree carries no models. `run-live-vm.cmd` runs `run-live.cmd` on the guest instead,
which downloads every model there with `modelado-cli download all` and runs those cases under
`winwright.live.json`, whose waits outlast a reconstruction on the CPU. Run it when a change
touches a reconstruction, where inference runs, or what the window shares with the CLI.

## How it works

```
src/
  Modelado.Imaging    decoding (with EXIF orientation), resizing, cut-out, PNG
  Modelado.Geometry   mesh, marching cubes, sparse sampling, relief, cleanup,
                      GLB/OBJ/PLY/STL export, the viewer's software renderer
  Modelado.Inference  meta.json, model folders, verified downloads, ONNX Runtime sessions
                      (DirectML → CPU), the 3D Object and Relief pipelines
  Modelado.App        the WPF app
tools/Modelado.Cli    the pipelines on the command line
scripts/              download, ONNX export, manifest, publishing to Hugging Face
```

- **3D Object:** a U²-Net cut-out (the same as TripoSR's `run.py`, through rembg), framed at
  85 % of a square over 0.5 grey, encoder → triplane. The decoder's density is sampled on a grid
  (256³ by default): first every 2 points, then at full resolution only in the cells the surface
  crosses and their neighbours. In the tests with TripoSR this gives **the same mesh, byte for
  byte**, as evaluating all 16.7 million points, 5 times faster. Marching cubes draws the
  surface at TripoSR's threshold of 25, loose pieces are removed, the mesh is smoothed (Taubin)
  and the decoder gives each vertex its colour.
- **Relief:** Depth Anything V2's relative depth (518 × 518) becomes the height of a grid shaped
  like the photo, between the 2nd and 98th percentiles so one stray pixel does not flatten the
  rest. With a base, the surface is closed with a flat bottom and walls at the edges.
- **Viewer:** a software rasteriser (z-buffer, Gouraud, bands in parallel): the same image in
  the app, on the command line and in the tests, with no GPU needed to draw.

Times measured on the CPU (4 cores, no GPU), with the FP32 models:

| Operation | Steps | Time |
| --- | --- | --: |
| 3D Object, grid 256 | cut-out 2.4 s · encoder 30.6 s (including loading 1.7 GB) · density 4.4 s · surface 0.6 s | 38 s |
| Relief, grid 256 | depth 1.3 s · mesh 0.2 s | 2.2 s |

## Model licences

| Model | Licence |
| --- | --- |
| TripoSR (Stability AI and Tripo AI) | MIT |
| DINO ViT-B/16 (TripoSR's encoder configuration) | Apache-2.0 |
| Depth Anything V2 **Small** | Apache-2.0 (Base and Large are CC BY-NC 4.0, so they are not used) |
| U²-Net | Apache-2.0 (weights), MIT (rembg's ONNX export) |
| Real-ESRGAN x4plus | BSD-3-Clause |

All of them allow redistribution and commercial use, which is why they are all hosted in the
Hugging Face model repository. The TripoSR code used for the export is in
`scripts\archs\triposr` (MIT, with the original licence). The app's About window lists each
installed model with a link to its licence.
