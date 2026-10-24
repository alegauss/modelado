# Improvements

## Block A — English everywhere

### §MOD3 README in English

Translate README.md whole: the operations table, Edit and refine, the export formats,
how to run, prepare-models and publishing. Keep every number it quotes (triangle counts,
timings) and check each one against the current code rather than translating it blind.
Image alt text is translated too.

Any other prose a person reads outside docs\ (for example comments in .cmd files and the
$comment in model-sources.json) is checked in the same pass. Comments in .cs and .py
files are already English, so confirm that and do not rewrite them.

Done when a search for Portuguese across the repository, docs\ excluded, finds nothing.

## Block B — Windows interface built like PhotoBoost

### §MOD4 Brand, Appearance and Templates as in PhotoBoost

Model: D:\git\japode\photo-boost\src\PhotoBoost.App\Brand.cs, Appearance.cs and
Templates.cs. Brand holds only the colours Fluent has no opinion on, as bytes, each
converted at its own edge. For Modelado that is the viewport background (#FF1C1F24
today), the empty-hint greys and the clay and wireframe colours MeshRenderer uses.
Appearance is the ChromeTheme record (System, Light, Dark) handed to WPF's ThemeMode, so
following Windows means following it while running. Templates builds the Ghost button
template in code (a new one per call) for the glyph toolbar.

An architecture test, as PhotoBoost has, fails on a colour literal written anywhere
outside Brand. Styles.xaml keeps Card, Chip and the caption styles, renamed to
PhotoBoost's names (GroupCard, StatusChip, StatCaption, SectionHeader) so the later
screens copy over cleanly.

Done when the window looks the same in dark mode, follows a light/dark switch while
running, and the architecture test passes.

### §MOD5 Glyph toolbar with commands

Model: the EditorToolbar in PhotoBoost's MainWindow.xaml (the ToolBarTray at the top,
PB63, PB65, PB66). Modelado's groups: Open (Ctrl+O) and Export (Ctrl+S); Undo (Ctrl+Z)
and Redo (Ctrl+Y, Ctrl+Shift+Z); rotate X, Y and Z and Mirror (moved here from the Edit
card); then, on the right, the view choice (colours, clay, wireframe, input photo) and
Reset view, as PhotoBoost places its compare modes.

Each button is a Segoe Fluent Icons glyph with AutomationProperties.Name, an
AutomationId and a tooltip naming its shortcut. Each goes through a CommandBinding with
CanExecute: off with no model, and off while an operation runs, as Undo already is. The
ToolBar's overflow takes what does not fit at MinWidth 900.

Done when every button and shortcut does the same thing, a disabled command disables
both, and the window at 900 px wide clips nothing.

### §MOD6 Pinned job panel

Model: PhotoBoost's JobPanel (PB60), docked at the top of the Operations DockPanel,
outside its ScrollViewer: a title with Cancel on its right, a 6 px progress bar and a
status caption. Modelado's ProgressCard and its Pause and Cancel buttons move there.
Pause stays for a download, which is what it is for.

The operations panel becomes a DockPanel with an "Operations" SectionHeader, the job
panel, and then the scrolling cards, at PhotoBoost's widths and margins (300 px, 24 px).

Done when a reconstruction started from the Relief card, with the panel scrolled to the
bottom, shows its progress at the top, and Cancel stops it.

### §MOD7 App icon

Model: PhotoBoost.ico as a Resource and
Window.Icon="pack://application:,,,/PhotoBoost.ico", plus ApplicationIcon in the csproj
so the exe carries it too. Modelado.ico holds 16, 24, 32, 48 and 256 px. It should be a
simple mark that reads at 16 px (a cube or a mesh), in the family of PhotoBoost's mark,
with its colours declared in Brand if they are drawn in code.

Done when the exe in Explorer, the window title bar and the taskbar all show it.

### §MOD8 AutomationIds and winwright cases

Model: D:\git\japode\photo-boost\winwright.json, cases\window.cases.json,
download.cases.json and upscale.cases.json, with fixtures in cases\fixtures. Every
interactive control and every text a case reads gets an AutomationId (PascalCase, the
same as x:Name), and icon-only buttons get AutomationProperties.Name too.

The first cases: open a photo and see the operations panel; open a model and see its
counts; run an edit and Undo it; open About and go Back; a file that fails to open. The
fixtures are a small photo and a small OBJ, committed. Cases that need the real models
skip when .cache\models is absent, as the tests do, and run-tests.cmd runs the cases.

Done when the cases pass in run-tests.cmd. Every later UI task adds its own case.

### §MOD9 Status bar with chips

Model: the StatusBar in PhotoBoost's MainWindow.xaml (PB14, PB24, PB58, PB60). Chips,
left to right: the provider ("DirectML · <GPU name>" or "CPU"); the last run's wall time
and provider; the run in progress as "<title>: <status>", bound to the job panel; a
model download in progress (megabytes, speed, time left); and the mesh chip Modelado
already has (vertices, triangles, texture). About sits on the right as a button.

The free-text StatusText line goes. What it says today becomes either a chip or a note
on the card that caused it, as PhotoBoost does with OperationNote.

Done when a reconstruction, a download and an edit each show in the status bar while the
operations panel is scrolled away from them.

### §MOD10 Drop zone and open errors

Model: PhotoBoost's DropZone, OpenError and ViewerOpenError (PB23, PB64). The card reads
"Drop a photo or a 3D model here", lists the formats (PNG, JPEG, WebP photos; GLB, OBJ,
PLY, STL models) and has an "Open…" button. Below that, the advice the hint carries
today: a single whole object, centred, on a plain background, and nothing leaves this
computer.

A file that cannot be opened is said in OpenError on the drop zone, or in
ViewerOpenError above the viewport once a model is shown, which stays. No MessageBox.
The operations panel and the toolbar's model commands appear with the first file, as
PhotoBoost's Operations is Collapsed until then.

Done when dropping a .txt file on each screen says why in place, and dropping a photo
then a model switches cleanly.

### §MOD11 About as an in-window panel

Model: PhotoBoost's AboutPanel in MainWindow.xaml and MainWindow.About.cs (PB46).
AboutWindow.cs goes. Its content moves into XAML as a GroupCard at MaxWidth 760 with:
the name and version; where inference runs and that no photo is uploaded; Models, each
installed one with the license its meta.json carries; Downloads (folder, disk used,
Download everything, Cancel, progress); and Third-party software.

The status bar's About button toggles it and reads "Back" while it is open, and the
stage collapses meanwhile. Per-model removal is a block D task, not this one: this one
moves what exists today.

Done when About opens and closes from the status bar during a running download whose
chip keeps updating, and AboutWindow.cs is deleted.

### §MOD12 History card

Model: PhotoBoost's History card and MainWindow.History.cs (PB28): a ListBox of every
version's label, the current one selected, not clickable, at MaxHeight 160. MeshHistory
already keeps a label per version (Generate object, Simplify to 25%, Smooth...), so this
only shows it. The labels must say the amount used where there is one ("Simplify to
25%", "Rotate 90° on X").

Done when generating, simplifying, smoothing and then undoing twice shows three entries
with the first selected, and a new edit after that drops the two redone ones from the
list.

### §MOD13 Export card

Model: PhotoBoost's Export card and MainWindow.Export.cs (PB25). The format is a
ComboBox: GLB, OBJ, PLY, STL. Only the options that format has are shown: OBJ with
texture or vertex colours; PLY binary or ASCII; STL with its size in millimetres, which
is the block C task for the CLI's --size. Then "Save as…" bound to the SaveAs command,
and an ExportStatus caption that names the file written.

What the mesh lacks is said on the card. For example, STL drops colour, and a model with
holes is not watertight for printing.

Done when each format is written from the card and opened again by MeshImport with the
same vertex and triangle counts.

### §MOD14 Every test runs on the VMware guest through winwright

Model: PhotoBoost's README "Tests" section and its run-tests.cmd, which loops over
tests\*.Tests and tests\*.Cases (the winwright cases as a test application, which skip
with the reason on a desk that cannot be observed), then runs scripts\tests. Modelado
gets the same shape: a tests\Modelado.Cases project driven by winwright.json at the
repository root (the executable under bin\{configuration}, timeouts), and run-tests.cmd
running it with --results-directory and --hangdump like PhotoBoost's.

Every test run, by hand or by a task's gate, goes through
D:\Git\alegauss\winwright\run-tests-vm.cmd -Tree D:\Git\alegauss\modelado, which carries
the working tree to the VMware Workstation guest, runs run-tests.cmd there with a desk,
and copies the verdict and an optional screenshot back. The guest has no DXGI video
memory budget (PhotoBoost's PB56), so DirectML may be refused and inference falls back
to the CPU. The tests must still pass there, and slow model tests get a bound, not a
skip.

The README and this project's roadmap skill both say that the VM run is the gate. Done
when the whole suite is green on the guest.

## Block C — Existing code wired into the window

### §MOD15 Export size in the window

MainWindow.xaml.cs saves through mesh.Placed(stl ? 100 : 1): STL always at 100 mm, every
other format at 1 unit. The CLI's --size takes any value. The size goes on the Export
card as a "Longest side" number box with the unit shown per format (mm for STL, units
otherwise), defaulting to today's values, plus the three dimensions it gives, read live
as the box changes.

Scaling happens at export only and is not a version in the history, as in the CLI. Both
go through Mesh.Placed, with no second scaler.

Done when an STL exported at 80 mm opens in a slicer at 80 mm on its longest side, and
the CLI and the window write identical files for the same model and size.

### §MOD16 Edit amounts in the window

MainWindow.Edits.cs calls MeshCleanup.Smooth(m, 3), MeshCleanup.RemoveSmallPieces(m,
0.05) and MeshEdit.FlattenBase(m, 0.03f). The CLI's --smooth N, --remove-pieces F and
--flatten F take each value. Simplify already has its slider, which is the pattern to
follow: a slider per amount on the Edit card, its value in the label, and the amount
written into the history label ("Smooth ×5", "Remove pieces under 2%", "Flat base at
6%").

So the card stays short, each amount can sit in a compact row beside its button instead
of a separate field block, as PhotoBoost's Restore faces card puts Strength beside its
slider.

Done when each amount set in the window gives the same mesh as the CLI with the same
value.

### §MOD17 Object sampling and smoothing on the card

MainWindow.Operations.cs builds ObjectOptions with Resolution, DensityThreshold,
RemoveBackground, EnhancePhoto and ProjectPhoto, and leaves CoarseStep (2) and
SmoothingIterations (2) at their defaults. KeepPieces (0.05) is also fixed, and the CLI
has no flag for it either.

These go in an "Advanced" expander on the 3D Object card, closed by default, so the card
stays as short as PhotoBoost's: "Sample every grid point (slower, catches thin parts)"
as a checkbox for step 1 or 2, "Smoothing passes" 0 to 6, and "Drop loose pieces under"
0 to 20%. Their tooltips say what each costs in time at 256.

Done when step 1 with no smoothing in the window gives the same triangle count as
modelado-cli object --step 1 --smooth 0 on the same photo.

### §MOD18 Provider choice in the window

MainWindow resolves options.Provider once, in the constructor, through
ExecutionProviders.Resolve, and the status-bar chip shows the result. The choice goes in
the About panel, under where inference runs, as Automatic, GPU (DirectML) or CPU, with
the reason the last resolve gave (for example why DirectML was refused). A change
applies to the next operation: the runner's sessions are dropped and rebuilt, and
nothing restarts the app.

The choice persists per user (a small settings file under LocalAppData, the same place
PhotoBoost keeps its Appearance), and --provider on the command line still wins for that
run.

Done when switching to CPU makes the next reconstruction run on the CPU, the chip says
so, and the choice is still there after a restart.

### §MOD19 Save the view as an image

The CLI's --preview renders the model through MeshRenderer into a PNG. The window
renders through the same MeshRenderer into ViewImage every frame. A toolbar button,
"Save view as image…" (a camera glyph), writes the current camera and view style
(colours, clay or wireframe) at a chosen size, 1024 to 4096 px on the long side,
rendered offscreen at that size rather than scaled up from the viewport. The background
is transparent for PNG.

The CLI's --preview and the button share one function, so their output for the same
camera matches byte for byte.

Done when a saved 2048 px PNG matches the view on screen, and a case in MOD8's suite
saves one.

### §MOD48 CLI refuses bad arguments cleanly

`tools\Modelado.Cli\Program.cs` parses its options before the `try` that turns an
`ArgumentException` into `ERROR <message>` and exit 1. So `modelado-cli edit x.glb
--smooth` (a value-taking option last) throws `--smooth needs a value.` as an unhandled
exception: a stack trace and exit -1 (0xE0434352), which a script or the Blender add-on
(MOD43) cannot read as a usage error. Any unknown `--option` is also taken silently,
eating the next argument as its value, so a misspelt `--simplfy 0.25` leaves the model
untouched and reports success.

Move the parse inside the `try` (or catch around it) so a missing value prints `ERROR
--x needs a value.` and exits 1. Keep a set of the options each command accepts and
refuse any other with `ERROR Unknown option: --x.` and exit 2, the code the
unknown-command branch already uses. Cover both with a test that runs the CLI's parse,
which may mean lifting it into a small class the tests can call, as
`src\Modelado.App\CommandLine.cs` already is for the app.

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

### §MOD21 Download consent panel

Model: PhotoBoost's ConsentPanel (PB58) and MainWindow.FirstUse.cs.
MainWindow.Downloads.cs builds a text and asks through MessageBox ("Só os modelos são
baixados... Continuar?"). That becomes a panel that takes the stage's place as About
does. It has a title naming the operation, "What is downloaded" (each file with its size
and variant, FP16 or FP32), the total, "Good to know" (from Hugging Face's
alegauss/modelado-models, into the models folder, verified by SHA-256, resumable, and
the photo stays on this computer), "License" (each model's, from the manifest), then
Download and Not now.

Not now leaves the operation's card saying what it needs, and nothing else is blocked.
DownloadText, PhotoBoost's one formatter for sizes, speed and time left, is ported so
the panel, the cards and the status-bar chip say the same thing.

Done when declining leaves every other operation usable, and accepting starts the
download with its chip in the status bar.

### §MOD22 First-use cards

Model: PhotoBoost's FaceFirstUse and BackgroundFirstUse blocks (PB58) and their
FirstUseCard state (Offered, Waiting, Downloading, Paused, Failed, Ready). The 3D
Object, Relief and AI texture cards each get one, fed from the manifest and the
ModelLibrary. The badge reads "One-time download · 1.4 GB", a detail line names the
models, and while downloading it shows a progress bar and "120 of 1400 MB · 8 MB/s ·
about 3 min left". Its action button reads Download, Pause, Resume or Retry.

ObjectModelsText and ReliefModelsText, the captions doing a little of this today, go.
Once the models are verified, the operation the person pressed starts by itself, as
PhotoBoost's pendingRun does. One download runs at a time, through one gate shared with
About's Download everything.

Done when a fresh models folder shows each card's badge with its real size, and a
download paused, resumed and finished starts its operation.

### §MOD23 Per-model removal in About

Model: PhotoBoost's DownloadedModels ItemsControl (PB13, PB58): one row per downloaded
model, "<name> · <variant> · <size>" with a Remove button whose AutomationId names the
model. AboutWindow.cs today has "Remover modelos baixados", which deletes the whole
DownloadRoot after a MessageBox. The rows replace it, and a Remove all with an in-panel
confirmation (no MessageBox) stays for the whole folder. The total disk used heads the
list.

Removing a model a running operation is using is refused with the reason, and the
operation's first-use card offers the download again afterwards.

Done when removing Depth Anything leaves TripoSR working and the Relief card offering
its download, and the folder size matches what the list said.

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

### §MOD28 Inno Setup installer

Model: D:\git\japode\photo-boost\build\installer.iss, build-installer.cmd and
collect-notices.ps1. It is a per-user install (no admin) with a fixed AppId that every
later version keeps, so a new installer upgrades in place, which the updater relies on.
It publishes Modelado.App self-contained for win-x64, plus modelado-cli beside it.

It adds a Start menu entry and offers "Open with Modelado" for .glb, .obj, .ply and .stl
(the bare-path argument CommandLine.cs already takes). The third-party notices are
collected from the NuGet packages and the models' licenses, and the uninstaller asks
whether to keep the downloaded models.

Models are not bundled: they download on first use (block D), so the installer stays
small. The output is Modelado-Setup-<x.y.z>.exe, the version-in-the-name pattern
freewilly uses.

Done when the installer is built, installed on the VMware guest, opens a model from
Explorer, upgrades over an older build keeping the models, and uninstalls cleanly.

### §MOD29 Release pipeline on GitHub

Model: D:\Git\alegauss\freewilly\.github\workflows\release.yml and PhotoBoost's
build\update-release.cmd and guard-publish.ps1. A local build\update-release.cmd <x.y.z>
sets the version in Directory.Build.props, runs the whole suite on the VMware guest
(MOD14), builds the installer, and tags v<x.y.z>.

The tag triggers release.yml on alegauss/modelado. It builds on windows-latest, refuses
a tag whose version disagrees with the props or the installer's file name, and publishes
a GitHub release holding Modelado-Setup-<x.y.z>.exe and SHA256SUMS. The release notes
are the CHANGELOG entries since the previous tag.

No code signing for now. The release notes say SmartScreen will warn, and the sums file
is what the updater checks.

Done when tagging a test version produces a release whose sums file matches the
installer it holds, and a mismatched tag fails the workflow.

### §MOD30 Release check

Model: D:\Git\alegauss\freewilly\src\FreeWilly.Core\Releases\ReleaseCheck.cs, with
claude-tray's src\Tray\Updater.cs as the simpler sibling. It reads
https://api.github.com/repos/alegauss/modelado/releases/latest behind an IReleaseFeed
seam, so the parsing is tested without the network. It compares the tag to the running
assembly version, and finds Modelado-Setup-<x.y.z>.exe and SHA256SUMS among the assets.
A release with two installers answering the pattern is refused rather than resolved by
taking the first.

It is silent on every failure (offline, proxy, rate limit, no releases yet): each reads
as "nothing newer". It runs once at startup after the window shows, then every 24 hours,
never during an operation, and sends a User-Agent.

A newer release shows as a status-bar chip, "Update 1.2.0 available", which opens About
at its update section. About also has "Check for updates" and a setting to turn the
automatic check off.

Done when the tests cover newer, same, older, malformed and two-installer feeds, and the
chip appears on the guest against a test release.

### §MOD31 Apply an update

Model: freewilly's ReleaseUpdate.cs and ReleaseSums.cs, and claude-tray's Updater, which
downloads to %TEMP% and runs the installer silently. "Update and restart" in About (and
on the chip) downloads the installer with progress in the status bar, resumable like a
model download. It checks the file's SHA-256 against its line in SHA256SUMS and refuses
on any mismatch or a missing line, deleting the file.

It waits for a running operation to finish, or offers to cancel it. It asks to export an
unsaved model first, since MeshHistory is lost on exit. Then it starts the installer
with /VERYSILENT /SUPPRESSMSGBOXES /NORESTART, which upgrades in place through the
shared AppId and relaunches Modelado, and the app exits.

Never automatic: the check may run by itself, the install only on a click.

Done when, on the VMware guest, an installed older build updates to a test release and
relaunches at the new version, and a tampered installer is refused.

## Block G — Public site for 3D modelling

### §MOD32 Site workspace like PhotoBoost's

Model: D:\git\japode\photo-boost\site (README.md is the map). It is a self-contained
Vite + React 19 + TypeScript + Tailwind v4 workspace in site\, which dotnet build and
run-tests.cmd neither build nor need. The chrome (nav, footer, buttons, theme toggle)
comes from @viglet/viglet-design-system/site, with the colours from its tokens. The
prerender and the social card come from its /vite helpers, as on PhotoBoost and
claude-tray.

English only, at /modelado/, per this roadmap's non-goal: one content.ts holds every
word, without PhotoBoost's i18n.tsx or pt route. The public/icon.svg and favicon.svg are
drawn from the app icon (MOD7). The prerender writes the title, description, canonical,
og.png and apple-touch-icon, by replace-or-throw, so a template that lost an anchor
fails the build.

npm run build is the gate for site tasks. Done when it builds, npm run preview shows the
page with nav, footer and theme toggle, and Lighthouse has no accessibility errors.

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

### §MOD36 Cloud provider interface and API keys

A new Modelado.Cloud assembly holds ICloudProvider: Id, DisplayName, Capabilities
(ImageTo3D, TextTo3D, Remesh, Retexture, TextToImage, Texture), a key validator, a usage
reader, and a cost estimator per action (the next three tasks build on these). Jobs are
submitted, polled and cancelled through one shape, since every such service runs
asynchronously, with progress reported through the same JobPanel the local operations
use.

About gains "Cloud services". Each provider shows a key field with Test, Remove and a
link to where the key is made. Keys are stored with Windows DPAPI (ProtectedData,
CurrentUser) in the settings folder, never in plain text, never logged, and never in a
crash report.

With no key, nothing in the app mentions the network. Results come back as a GLB or an
image and enter MeshHistory as a version labelled with the service ("Meshy: image to
3D"), so Undo, the edit tools and export apply to them unchanged.

Done when a fake provider in the tests drives a job from submit to a version in the
history, and a stored key is unreadable as another Windows user.

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

### §MOD41 Usage and status monitor per service

A "Cloud usage" panel (from About, and from a status-bar chip that appears once any key
is stored) shows a row per provider. Each row has the key's state (valid, invalid,
expired), the service's reachability, the balance or remaining credits where its API
reports them, and the limit if the user sets one. It also shows what this app spent
through it today, this month and in total, from a local ledger: every job's provider,
action, cost, time and result, kept in the settings folder. Where the API reports no
balance, the row says that its figures are this app's own spend, not the account's.

The user sets a monthly budget per service and a warning threshold (default 80%).
Crossing it turns the chip amber and says so on the next consent line. At 100%, jobs to
that service are refused until the budget is raised. Rate-limit and quota errors from a
service show on its row with the time they clear.

Done when, with a fake provider in the tests, spending past 80% turns the chip amber,
past 100% refuses the next job, and the ledger survives a restart.

### §MOD42 Cost projection before each action

Each ICloudProvider implements Estimate(action, options), returning the credits (and the
currency amount where the plan has one) the action will take. It comes from a price
table per provider, kept in a versioned JSON beside the provider with the date it was
read from the service's pricing page, and refreshed from the API where the service
exposes prices.

The estimate shows on the button ("Generate with Meshy · ~30 credits") and updates live
as the options change. The consent line repeats it beside the balance and budget left
("~30 of 470 credits left this month"). An action that would cross the budget says so
before it runs.

After each job, the actual cost (from the API where reported, otherwise the estimate)
goes into the usage ledger. The panel shows estimate against actual per action, so a
stale price table shows up as a drift. A table older than 90 days is flagged.

Done when, with a fake provider, each option change moves the estimate, an over-budget
action is refused before upload, and the ledger records estimate and actual.

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
