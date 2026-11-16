# Modelado

For anyone who wants to work in 3D with AI and save time: Modelado is a Windows app that turns
**photos of objects into 3D models**, and edits and improves any 3D model, with AI models running
on your own computer. No photo leaves the PC.

**No NVIDIA GPU needed.** Modelado runs its models through DirectML, so any DirectX 12 graphics
card works: AMD and Intel as well as NVIDIA, with no CUDA. A PC with no graphics card runs it on
the CPU alone: a 3D object from a photo in about 20 seconds on 8 cores of an Intel Core i7-14700,
38 seconds on 4 cores, and a relief in about a second (see [How it works](#how-it-works)).

**Unlimited and free.** Because everything runs on your PC, there is no account, no credits to buy
and no moderation of your photos or what you make: run it as often as you like, for printing,
games, Blender or the web.

**Get it from the [Microsoft Store](https://apps.microsoft.com/detail/9PB86WW5NRR2)**: signed by
the Store, updated by it, and installed without a SmartScreen warning. The
[latest release](https://github.com/alegauss/modelado/releases/latest) also holds
`Modelado-Setup-<version>.exe`, which installs per user without administrator rights; it is not
code-signed, so SmartScreen warns the first time (**More info**, then **Run anyway**), and the
release's `SHA256SUMS` is what to check it against. The site,
[alegauss.com/modelado](https://alegauss.com/modelado/), shows what it makes.

Modelado is a sibling of [Revelei](https://github.com/alegauss/revelei), which improves photos:
AI on your PC, nothing leaves it. Both share one architecture (models converted to ONNX by
Python scripts, published on Hugging Face with pinned SHA-256 digests, downloaded by the app on
first use and run with ONNX Runtime on the GPU or the CPU); Modelado makes geometry.

| Operation | Models | Result |
| --- | --- | --- |
| **3D Object** | U²-Net (cut-out) → TripoSR (encoder + decoder) → marching cubes | A closed mesh with front, sides and back, coloured per vertex |
| **Relief** | Depth Anything V2 Small (+ optional U²-Net) | A bas-relief textured with the photo; with a solid base, ready to print |
| **Edit and improve** | Real-ESRGAN x4plus + geometry tools | Any generated or opened model (GLB, OBJ, PLY, STL): simplify, subdivide, smooth, fill holes, flat base, turn, mirror, 4× sharper texture, photo colours |

The result appears in a 3D viewer (drag to turn, mouse wheel to zoom, double-click to recentre;
colour, clay or edges) and is saved as **GLB** (binary glTF: Blender, the Babylon.js
Sandbox in a browser, the web; Windows 3D Viewer left the Microsoft Store on 1 July 2026), **OBJ** (with .mtl and texture, or vertex colours), **STL** (3D printing, in millimetres),
**3MF** (3D printing with the millimetres recorded in the file and the colours kept: Bambu Studio,
OrcaSlicer, PrusaSlicer) or **PLY** (with colours: MeshLab, CloudCompare). The command line writes
whichever the output's extension names, `.3mf` included. STL and 3MF are laid on the slicer's
plate, centred with their lowest point on Z = 0, so a relief opens face up with its flat back
down, the way it prints without supports. Where Bambu Studio, OrcaSlicer, PrusaSlicer or UltiMaker
Cura is installed, the Export card's **Open in** button hands it the model as a 3MF at the print
size, and with several, the one picked is remembered.

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
modelado-cli edit robot.glb -o robot.3mf --make-printable 0.4 --size 100
modelado-cli edit relief.glb -o relief-sharp.glb --upscale-texture
modelado-cli object photo.jpg -o object.glb --enhance
modelado-cli cloud image-to-3d photo.jpg --estimate
modelado-cli cloud image-to-3d photo.jpg -o meshy.glb
modelado-cli cloud texture "weathered oak planks" -o oak.png
modelado-cli cloud remesh model.glb -o clean.glb --polycount 20000
modelado-cli cloud retexture model.glb --prompt "rusted iron" -o rust.glb --keep-uv
```

`--make-printable` rebuilds the model on a voxel grid that many millimetres wide at the `--size`
it is saved at, so whatever holes or crossing pieces it had, it comes out closed and manifold
for a slicer, its colours kept. `--check-thickness 1.2` colours red every part whose wall is
thinner than 1.2 mm at that size (an ear or a tail a slicer would drop) and says how much of the
surface that is; the Edit card's **Check thickness** does the same at the size Export prints at.
`--thicken 1.2` grows those walls to 1.2 mm on `--make-printable`'s voxel and leaves the rest of
the model's shape alone; the Edit card's **Thicken** does the same.
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
- **Memory:** 3D Object is the heavy one. Measured peaks (below): about 4.2 GB of RAM on the CPU
  (4.5 GB at high quality); on a GPU, 1.8 GB of RAM and 2.5 GB of video memory at standard
  quality, and 4.6 GB of RAM with 3.2 GB of video memory at high. Relief stays under 1 GB.
- **Disk:** 3D Object downloads about 1.0 GB of models on a GPU at standard quality and 1.85 GB
  on the CPU or at high quality, since those always take the full-precision encoder. Relief takes
  50 MB on a GPU or 99 MB on the CPU (176 MB more for Object only), and Real-ESRGAN 34 or 67 MB,
  only when an AI enhancement is used. The first-use prompt says the size for this PC.

## Using it

Install it from the [Microsoft Store](https://apps.microsoft.com/detail/9PB86WW5NRR2) or the
[latest release](https://github.com/alegauss/modelado/releases/latest) and start it from the
Start menu. Open a photo (or drop it on the window) and choose
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
by itself, and it asks first when the model shown has not been saved. A copy installed from the
Microsoft Store never checks: About says its updates come from the Store.

## Building from source

For contributors. Needs the **.NET 10 SDK** (`global.json`):

```
dotnet build Modelado.slnx
.\run.cmd
.\run.cmd --open photo.jpg --provider cpu
```

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

### The Store package

```
build\build-msix.cmd
powershell -NoProfile -File build\check-msix-vm.ps1
```

`build-msix.cmd` packs the same publish folder (`build\publish.cmd`) with
`build\msix\AppxManifest.xml`, the identity reserved in Partner Center, into
`artifacts\msix\Modelado.msix`, unsigned: the Store signs it on upload. `-Register` installs the
layout on this machine, and `check-msix.ps1` registers it, starts Modelado from its Start menu
entry and removes it again; all need Developer Mode. The guest has it off, so the check runs on
the desk that has it:

```
powershell -NoProfile -File build\check-msix.ps1 -Layout artifacts\msix\layout -Cases
```

`-Cases` also checks Open with, the `modelado-cli.exe` execution alias and the window
`modelado-cli window` opens through it: the package aliases the CLI alone, since the Store refuses
a second application without a Start entry. `-RunCases` adds the UI cases in `cases-packaged\`
(`MODELADO_PACKAGED=1`, after a `dotnet build Modelado.slnx`), which wait on winwright following a
window a launched process's child draws (WW620). They stage `LOCALAPPDATA` wherever they write, so
the desk's own Modelado data is never touched.

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
dotnet run --project tools\Modelado.Cli -c Release -- relief portrait.jpg -o plaque.stl --width-mm 60 --depth-mm 3 --base-mm 2
dotnet run --project tools\Modelado.Cli -c Release -- relief pet.jpg -o keychain.3mf --width-mm 45 --keychain circle --hole-mm 4
dotnet run --project tools\Modelado.Cli -c Release -- download all
```

`object` takes `--resolution 128..384`, `--threshold`, `--keep-background`, `--step`,
`--smooth`; `relief` takes `--grid`, `--depth`, `--base`, `--remove-background`, or, sized for
printing, `--width-mm`, `--depth-mm` and `--base-mm` in place of `--depth` and `--base`: the
relief (the cut-out, with `--remove-background`) is built that many millimetres wide and saved at
that size, so the STL measures what was asked for. The Relief card's **Size it in millimetres**
does the same. With `--width-mm`, `--keychain circle` or `--keychain tag` cuts the relief into a
keychain, a disc or a rounded tag with a hole near the top (`--hole-mm`, 4 by default), written as
one closed solid with the hole through it. `--shape rounded|circle|oval` crops a relief to a plaque
or a medallion, closed the same way, and `--border-mm` with `--border-height-mm` raises a frame
along its outline; the back stays flat, for a magnet or glue. The Relief card has the same shapes,
and the border with the other millimetres. `--text "ANA 2026"` raises a name or a date along the
bottom, or with `--text-on arc` around a medallion (`--text-mm` sets the letters' height), in a
built-in font of capitals and digits, or with `--font` any TrueType (.ttf) font's own outlines, in
lower case and accents too. `--text-engrave-mm 1` cuts the letters 1 mm into the surface instead of
raising them, always leaving 0.6 mm under them. The Relief card takes the text, its height, where it
runs, an installed font and raised or engraved letters with the other millimetres, and says before
a run when the text will not fit, when the font lacks a character, or when the relief is too thin
for the cut. `--stand` also
writes `<output>-stand`, a block that prints flat with a slot cut to the relief's thickness, so
the plaque stands on a shelf leaning back `--stand-tilt` degrees (15 by default); on the Relief
card, **Also save a stand** makes Save as write it beside an STL or 3MF. `lithophane` needs no
model: the panel is thin where the photo is bright and thick where it is dark (`--min-mm` 0.8 and
`--max-mm` 3 by default, `--width-mm` 100), so the photo shows when it is lit from behind.
`--shape curve` bends it through `--arc` degrees (120 by default) so it stands on its own, and
`--shape cylinder` bends it all the way round into a shade for a tea light; `--frame-mm 3` adds a
rim at the thickest so a thin panel keeps its shape. The Relief card's **Make lithophane** does the
same, with the shape and the frame beside the thicknesses. Common to all: `--provider`, `--high`, `--models`, `--manifest`, `--size` (longest
side when saving).

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
   reads the repository's latest manifest. A graph over protobuf's 2 GB limit keeps its weights
   in ONNX external data files beside it: `meta.json` lists them under the variant's `data`, they
   are published as `<model>-<file>` and listed in the manifest with the name the graph uses, and
   the app fetches and verifies them before the graph, into the same folder.

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

On the CPU, **Light** quality (About, or `--light` on the command line; MOD116) runs a 433 MB
INT8 encoder instead of the 1.6 GB FP32 one, so 3D Object downloads about 0.6 GB rather than
1.85 GB and needs about 1 GB less memory. It is no faster: 11.5 to 15.7 s against about 15 s on
an 8-core i7-14700 (docs/local-3d-models.md). Its shapes drift visibly from FP32's, so it stays a
choice and Standard stays the default.

Peak memory, measured with `build\measure-memory.ps1` (MOD118) on the robot photo: the CLI's peak
working set, and the GPU memory Windows' counters gave its process (dedicated is video memory,
shared is system memory the GPU maps), on an Intel Core i7-14700 with an NVIDIA GeForce RTX 4060
(8 GB):

| Operation | CPU: RAM | DirectML: RAM | DirectML: video memory | DirectML: shared |
| --- | --: | --: | --: | --: |
| 3D Object, grid 256 | 4.2 GB | 1.8 GB | 2.5 GB | 1.0 GB |
| 3D Object, High, grid 384 | 4.5 GB | 4.6 GB | 3.2 GB | 3.3 GB |
| Relief, grid 256 | 0.3 GB | 0.4 GB | not sampled (under 1 s) | not sampled |
| Relief, Object only, grid 512 | 0.8 GB | 0.6 GB | 0.6 GB | 0.3 GB |

On the same processor's integrated **Intel UHD Graphics 770** through DirectML (MOD121,
`build\measure-memory.ps1 -Gpu 1`, which runs `modelado-cli --gpu 1`), every model runs, but
3D Object is slower there than on the CPU. So Automatic (MOD147) asks DXCore which GPUs are
integrated. On a PC whose only GPU is integrated it runs on the CPU and says why on the provider
chip; DirectML chosen in About, or `--provider dml`, still forces the GPU. On a hybrid PC it runs
on the discrete GPU even when Windows lists the integrated one first:

| Operation | Time | RAM | GPU dedicated | GPU shared |
| --- | --: | --: | --: | --: |
| 3D Object, grid 256 (FP16) | 65 s | 2.6 GB | 2.0 GB | 2.0 GB |
| 3D Object, High, grid 384 (FP32) | 35 s | 6.4 GB | 1.2 GB | 5.4 GB |
| Relief, grid 256 (first run, shaders compiled) | 8.9 s | 0.4 GB | 0.2 GB | 0.1 GB |
| Relief, Object only, grid 512 | 2.3 s | 0.5 GB | 0.2 GB | 0.1 GB |

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

None of these carries conditions. A model whose licence does carry them is optional, and it is
handled like this:
- The download asks you to agree to its terms first, and records that you did (MOD123).
- A notice its licence requires appears beside each result made with it, in About, and in the
  GLB or 3MF metadata of what is saved or opened in a slicer, with the model named (MOD126). An
  example is "Powered by Stability AI" for SF3D and SPAR3D, which are under the Stability AI
  Community License (docs/model-downloads.md).
