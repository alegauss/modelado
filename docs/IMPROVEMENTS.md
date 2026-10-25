# Improvements

## Block A — English everywhere

### §MOD49 Numbers formatted in one culture

The window's text is English (MOD1), but its numbers are formatted in the system
culture: `{n:N0}`, `{x:P0}`, `{s:0.0}` and `CultureInfo.CurrentCulture` in
MainWindow.xaml.cs, MainWindow.Operations.cs, MainWindow.Edits.cs and
MainWindow.Downloads.cs (16 sites). On a pt-BR machine a 980 000 triangle mesh shows as
"980.000 triangles", which an English reader takes for 980, and "0,8 s" and "1,1 GB" mix
decimal commas into English sentences. The CLI already formats its own parsing with
InvariantCulture but prints counts with the current culture too.

The non-goal "A Portuguese or multilingual interface" settles the choice: the app speaks
one language, so it should format in one culture. Set
`CultureInfo.DefaultThreadCurrentCulture` and `DefaultThreadCurrentUICulture` to en-US
at startup in App.OnStartup (and at the top of the CLI's Program.cs), before any window
or thread exists, rather than threading a culture through every format call. Keep file
parsing on InvariantCulture as it is.

Done when, on a machine set to pt-BR, the mesh chip reads "980,000 triangles", timings
read "0.8 s", and a test pins the startup culture.

## Block B — Windows interface built like PhotoBoost

### §MOD53 Status bar chips for a reconstruction and a download

MOD9 put the job and download chips in the status bar and saw an edit there live on the
VMware guest: "Simplify: 980.000 triangles" while the cards were scrolled to the bottom,
then the last-run chip. A reconstruction and a model download use the same SetBusy path
(JobChip for a reconstruction, DownloadChip for a download, bound to the job panel's
title and text) but neither could run: the embedded manifest lists no models and the
repository is not published yet (MOD20), so the guest has nothing to download or run.

The download half is now held by a case: MOD11 added a fixture server to
tests\Modelado.Cases and "About opens and closes during a download whose chip keeps
going" reads DownloadChip against it on the guest. What is left is the reconstruction:
once MOD20 has published the models, start a Relief on the guest with the cards scrolled
down and read JobChip, then LastRun ("Relief · <s> s · CPU"). With the models there,
also close MOD17's check: a 3D Object at step 1 with 0 smoothing passes (the card's
Advanced) gives the triangle count modelado-cli object --step 1 --smooth 0 gives on the
same photo. And MOD18's: after choosing the CPU in About, the next run's LastRun says
CPU.

## Block C — Existing code wired into the window

## Block D — Model downloads

### §MOD20 Export, publish and embed the first manifest

src\Modelado.Inference\models-manifest.json is schema 1, version "0", base_url "",
models []. The pipeline exists: prepare-models.cmd downloads the upstream weights
(download_models.py, about 2 GB, each file checked by SHA-256), converts them to ONNX
and checks each against PyTorch (export_models.py), and with --publish uploads to
alegauss/modelado-models (publish_models.py). Publishing pins every URL to the upload's
commit and copies the manifest into the app.

This task runs that end to end once. It needs a Hugging Face write token, from a file
the user names, which is never printed or committed. Run --dry-run first and read the
staged model card and manifest before uploading. scripts\published-models.json and the
new embedded manifest are committed. The weights and ONNX files are not: they live in
.cache.

Done when a clean build with no .cache downloads every model from the window on first
use, each verified by SHA-256, and the 3D Object, Relief and texture upscale operations
each run on the robot photo.

## Block E — Publishing custom models to Hugging Face

### §MOD24 Add a custom model to the publish stage

build_manifest.py reads every <models>/<name>/meta.json that export_models.py wrote, and
publish_models.py uploads the stage. A custom model needs the same folder shape without
the export step. A new scripts\add_model.py takes --name, --task (one the app knows:
object-encoder, object-decoder, depth, saliency, upscale), --onnx fp32.onnx [--fp16
fp16.onnx], --license and --license-url, and --source (where the weights came from).

It validates what the app will rely on: onnx.checker, the input and output names and
shapes the pipeline for that task expects (taken from the pipeline's own constants, not
retyped), and an ONNX Runtime run on that task's test image. Then it writes meta.json
beside the files in .cache\models\<name>, so build_manifest and publish_models take it
with no change.

Done when a re-exported copy of Depth Anything added this way, under a new name,
publishes to a test repository and the app's Relief runs with it.

### §MOD25 License gate on publish

publish_models.py's docstring says every model is under a license that allows
redistribution, and nothing checks it. Its model card writes "license: other" with a
per-model table. A gate in build_manifest.py (so --dry-run catches it too) refuses a
model whose meta.json license is not on an allow-list (MIT, Apache-2.0, BSD-2-Clause,
BSD-3-Clause, CC-BY-4.0), naming the model and its license. --allow-license <id>
--reason "<why>" accepts one more for that run, and the reason goes into the model card
beside it.

Depth Anything V2 Base and Large (CC BY-NC 4.0) are the worked example of what must be
refused. model-sources.json already notes why only Small is used.

Done when staging a model with license CC-BY-NC-4.0 fails with its name, and passes with
--allow-license and a reason that appears in README.md.

### §MOD26 Verify a publish from the repository

After commit 2 (the pinned manifest), publish_models.py downloads manifest.json from its
resolve URL, then HEADs every file the manifest lists, checking size and the
X-Linked-Etag that Hugging Face gives for LFS files (the SHA-256). It fully downloads
and hashes any file whose header lacks it. The same check is a standalone
scripts\verify_published.py --repo <id> [--revision <commit>], so a repository can be
audited at any time without republishing.

The embedded manifest is copied into src\Modelado.Inference only after the check passes.
A failure names the file and what differed, and leaves the previous embedded manifest in
place.

Done when publishing to a test repository passes verification, and a file replaced by
hand in that repository makes verify_published.py fail with its name.

### §MOD27 Custom model repositories in the window

CommandLine.cs takes --manifest <url>, and ModelManifest refuses one it cannot trust
(https, or loopback http for tests). About gains "Model sources": the built-in
alegauss/modelado-models, always first and not removable, and any added source. A source
can be given as a Hugging Face repository id (owner/name, resolving to its manifest.json
at the latest commit, then pinned to that commit) or as a manifest URL.

Adding one shows what it lists (each model, task, size and license) before it is saved.
A model whose task the built-in already covers appears as a choice on that operation's
card, for example "Upscaler: Real-ESRGAN x4plus (built-in) / <custom>". Sources persist
per user beside the provider choice. --manifest still overrides them for one run.

Done when a test repository published with MOD24's model is added by its id, its model
is chosen on the Relief card, and Relief runs with it.

## Block F — Installer, releases and auto update

### §MOD29 Release pipeline on GitHub

Model: D:\Git\alegauss\freewilly\.github\workflows\release.yml and PhotoBoost's
build\update-release.cmd and guard-publish.ps1. A local build\update-release.cmd <x.y.z>
sets the version in Directory.Build.props, runs the whole suite on the VMware guest
(MOD14), builds the installer, and tags v<x.y.z>.

The tag triggers release.yml on alegauss/modelado. It builds on windows-latest, refuses
a tag whose version disagrees with the props or the installer's file name, and publishes
a GitHub release holding Modelado-Setup-<x.y.z>.exe and SHA256SUMS. The release notes
are the commit titles since the previous tag, each naming its MOD id, written by
build\release-files.ps1; that script, the workflow and update-release.cmd are built and
checked locally. What is left is a pushed test tag: its release's SHA256SUMS must match
the installer, and a tag that disagrees with the props must fail the workflow.

No code signing for now. The release notes say SmartScreen will warn, and the sums file
is what the updater checks.

Done when tagging a test version produces a release whose sums file matches the
installer it holds, and a mismatched tag fails the workflow.

## Block G — Public site for 3D modelling

### §MOD33 Hero: photo to a model you can orbit

Model: PhotoBoost's Hero.tsx and Compare.tsx (drag anywhere, or the keyboard through a
hidden range input). Modelado's hero shows the input photo beside a GLB viewer of the
model Modelado made from it, which the reader drags to orbit and wheels to zoom. A
toggle switches colours, clay and wireframe, the app's three view styles.

The viewer is <model-viewer> or three.js, loaded lazily so the first paint is the poster
(a --preview render from the CLI, MOD19's function). It is keyboard operable and
labelled, and respects prefers-reduced-motion with no auto-rotate.

Every example in public\demo is a real Modelado result made with the published models,
with the photo's source and license credited. Each GLB is simplified to keep the page
light (under 2 MB each), and the triangle count is shown beside it.

Done when the hero orbits on desktop and touch, and a reduced-motion reader gets the
poster with no spin.

### §MOD34 Studio, journey, details and download

Model: PhotoBoost's Studio.tsx, Journey.tsx and Sections.tsx. The Studio is a dark stage
with a tab per operation, each showing a real input and its orbitable result: 3D Object
(TripoSR), Relief (Depth Anything V2: a face, a building, a landscape), Edit and refine
(before and after simplify, smooth and fill holes, with triangle counts), and AI texture
(Real-ESRGAN, before and after). The Journey is the four moves: drop a photo, generate,
refine, export (GLB, OBJ, PLY, STL, and print-ready STL in millimetres).

Then privacy (inference on this PC, only models are downloaded, the opt-in cloud
services of block H named as the one exception), speed (measured timings on a named GPU
and on CPU), and the models and their licenses.

The download button points at the latest GitHub release's installer (MOD29), read at
build time, with its size and version. Every number the page quotes comes from a real
run, written once in content.ts.

Done when every tab shows a real result, and the download link resolves to the current
release.

### §MOD35 Site deploy workflow

Model: D:\Git\alegauss\freewilly\.github\workflows\site.yml. On a push to main that
touches site\**, and on demand, it runs npm ci and npm run build in site\ and publishes
dist\ to GitHub Pages at alegauss.github.io/modelado/, matching Vite's base. A pull
request builds without deploying, so a broken prerender fails the check before merge.

The workflow pins its action versions and Node version, as freewilly's does.

Done when a push changing a word in content.ts is live on Pages, and a pull request with
a template missing an anchor fails its check.

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

### §MOD37 Meshy AI: image and text to 3D

The first ICloudProvider is MeshyProvider, over Meshy's REST API with the user's Bearer
key. Check the current API docs when this is built: the endpoints, model versions and
options move. A "Meshy AI" card appears only once a key is stored. It has two modes:
Image to 3D (the open photo, sent as is or after Modelado's own background removal) and
Text to 3D (a prompt). It offers the options Meshy takes (topology, target polycount,
PBR textures on or off, art style), each defaulted to Meshy's own.

Before the upload, the consent line names what is sent ("This photo, 2.1 MB, goes to
Meshy AI") and the projected cost (MOD42). The job runs in the JobPanel with Meshy's own
progress, can be cancelled, and survives a restart by its task id. The GLB it returns is
downloaded, imported through MeshImport with its textures, and pushed as a version in
the history.

It also brings About's "Cloud services", which MOD36 left for the first provider: each
of CloudProviders.All with a key field, Test (ValidateKeyAsync), Remove and a link to
its KeyPage, storing through CloudKeys. Run jobs with CloudJobs.RunAsync, and label the
version with CloudJobs.Label.

Done when the same photo goes through TripoSR and through Meshy (with a real key, on the
guest), and both results can be refined and exported.

### §MOD38 Meshy AI: remesh and retexture the open model

The Edit and refine card gains a "With Meshy AI" group, shown only with a key: Remesh
(target polycount, quad or triangle topology) and Retexture (a text prompt or a style
image, keeping the model's UVs or making new ones). The open model is exported to GLB,
uploaded, processed, and the result comes back as a new version, so Undo returns to the
local one.

The consent line names what goes ("This model, 151k triangles, 6.4 MB GLB, goes to Meshy
AI") with the projected cost, as for generation. A model over Meshy's upload limit is
refused before upload, offering the local Simplify first.

Done when a TripoSR result is remeshed and then retextured through Meshy, both steps are
undone, and the original returns byte for byte.

### §MOD39 Ideogram: a reference image from a prompt

IdeogramProvider is the second ICloudProvider, with the TextToImage capability, over
Ideogram's REST API and the user's key (check its current API docs when built). The drop
zone gains "Generate a photo with Ideogram…", shown only with a key. It takes a prompt
and wraps it in a template that asks for what reconstruction needs: one whole object,
centred, three-quarter view, even light, plain light background, no text. The user sees
the template and can edit it.

It offers Ideogram's aspect ratio, style and seed, and up to four candidates side by
side. The chosen one opens as if it had been dropped, so 3D Object, Relief and Meshy all
take it unchanged. The image is saved beside the session with its prompt and seed in the
PNG's text chunk, so the result can be traced and redone.

Done when a prompt gives four candidates, one becomes a TripoSR model, and its PNG
carries the prompt.

### §MOD40 Ideogram: textures from a prompt

The Edit and refine card's "With AI" group gains "New texture with Ideogram…", shown
only with a key and only for a model with UVs (reliefs, OBJ and GLB opened with
textures). It takes a prompt wrapped in a template for a flat, evenly lit, seamless
square texture with no perspective. The result replaces the model's texture as a new
version, so Undo returns to the old one, and Real-ESRGAN can then be applied to it as
today.

A model with no UVs (TripoSR's vertex colours) gets a box or triplanar projection in
Modelado.Geometry, which a test checks for seams. For tiling, Ideogram's result is made
seamless locally (offset and blend at the edges) when the service's own output tiles
badly.

Done when a relief gets a stone texture from a prompt, the seam test passes, and Undo
restores the photo texture.

### §MOD55 Cloud usage panel and chip

MOD41 and MOD42 built the figures in Modelado.Cloud and left their window here, since it
shows only once a key is stored, which needs MOD37's first provider. CloudLedger keeps
each job's provider, action, estimate, actual, time and outcome. CloudBudget and
BudgetState give Ok, Warning past WarnAt and Exceeded at the limit. CloudPrices
estimates an action from a dated price table, and CloudJobs.RunAsync, given a
CloudSpending, records each job and refuses with BudgetExceededException, before any
upload, one the budget cannot cover.

Build the window over them. A "Cloud usage" panel, from About and from a status-bar chip
that appears once any key is stored, has a row per provider: the key's state
(ValidateKeyAsync), the balance where UsageAsync reports one, and this app's spend
today, this month and in total, said to be its own where no balance is reported. It also
has the monthly budget and warning share the user sets, estimate against actual per
action, and a flag on a price table CloudPrices.IsStale calls old.

The action's button carries its estimate ("Generate with Meshy · ~30 credits") and
updates as options change. The consent line repeats it beside what the budget leaves.
The chip turns amber at Warning, and an action the budget cannot cover says why before
it runs. Rate-limit errors show on the row with when they clear. A winwright case drives
it with a stand-in provider on the cases' fixture server.

## Block I — Blender integration: AI tools for existing Blender models

### §MOD43 Blender add-on with Modelado's edits

A Blender extension (Blender 4.2+ format, blender_manifest.toml) lives in
blender\modelado and puts a "Modelado" panel in the 3D Viewport's sidebar. It finds
modelado-cli from a preference, defaulting to the installed path (MOD28 bundles the
extension zip). For the selected mesh object it offers the edit tools with their
amounts: Simplify, Subdivide, Smooth, Fill holes, Remove loose pieces, Flat base.

Each runs as: export the object to a temp GLB (applying modifiers if the user asks),
modelado-cli edit, then import the result. The result replaces the mesh data of the same
object, keeping its name, transform, parent, collections and material slots, so
Blender's own Ctrl+Z undoes it. The CLI gains --json, printing a machine-readable result
(counts, timings, warnings) that the add-on reads instead of parsing text.

The CLI runs in a background thread with a progress bar and Cancel, so Blender never
freezes. Tests run Blender headless (blender --background --python) on the VMware guest
with a fixture .blend.

Done when Simplify to 25% on Suzanne subdivided twice keeps its transform and material,
and Ctrl+Z restores it.

### §MOD44 Generate from a photo into Blender

The add-on's panel gains "Generate from photo". It picks an image file, or an image
already loaded in the .blend, and offers 3D Object (TripoSR, local), Relief (Depth
Anything, local) or, with a key, Meshy image to 3D. It takes the same main options as
the window (resolution, thickness, relief height and base).

The result is imported at the 3D cursor as a new object named after the photo, scaled to
a size the user sets in the panel (default 0.2 m on the longest side), with its vertex
colours or texture in a new material. Models download on first use through the CLI's
download, with progress in Blender's status bar, after the same consent the window asks
for.

Done when the robot photo becomes an object at the cursor at 0.2 m with its colours
visible in Material Preview, from a Blender with no models downloaded yet.

### §MOD45 AI actions in the Blender add-on

The add-on's panel gains an "AI" group for the selected object. It has Upscale texture
4× (Real-ESRGAN, local); New texture from prompt (Ideogram); and Remesh and Retexture
(Meshy). The cloud actions show only when the app has the key: the CLI reads the same
DPAPI store as the window, so the add-on never handles a key itself.

Each cloud action shows the same consent line and cost estimate as the window (MOD42's
estimate, via modelado-cli estimate --json), and asks before anything is sent. Its spend
goes into the same ledger, so the usage monitor counts it.

The CLI gains the subcommands these need (upscale-texture already exists; texture
--prompt, meshy remesh, meshy retexture). Textures come back as images packed into the
.blend and wired into the object's existing Principled BSDF Base Color, replacing only
that input, so the rest of the material is kept.

Done when, on the guest, a textured model in Blender gets an upscaled texture and an
Ideogram texture, each undoable, and the cloud action is refused when declined, with no
request made.

### §MOD46 Round trip between Blender and the window

"Edit in Modelado" in the add-on exports the object to a session folder and launches
Modelado.exe --blender-session <folder>, which opens it with its name in the caption. In
that mode the toolbar gains "Send to Blender". It writes the current version to the
session folder and signals the add-on, which polls the folder with a Blender timer and
replaces the object's mesh data as MOD43 does: transform, materials and collections
kept, and one Blender undo step.

Several sends are fine, each a new undo step in Blender. Closing the window ends the
session. In the other direction, the standalone window's Export card gains "Open in
Blender", which finds Blender (registry, then the user's path preference) and starts it
importing the model.

Done when an object goes to the window, gets three edits, is sent back twice, and both
sends undo in Blender in order.
