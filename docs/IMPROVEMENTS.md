# Improvements

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

### §MOD94 MOD94 — Recent files

Keep the last ten paths that OpenAsync loaded and that Save wrote, newest first, in the
settings file (SettingsFile) and not in the registry, so the MSIX package and the
portable build behave alike. Show them on the drop zone under Open…, each with the file
name, the folder and a thumbnail for photos, and in a Recent menu beside the Open
toolbar button. A path that no longer exists is shown greyed with "Remove from list"
rather than being dropped silently. When MOD95 lands, generated photos enter the list
through OpenAsync like any other file. When Windows can do so for the package, the same
paths also feed the taskbar Jump List. A case opens two fixture files, restarts the app,
and finds both listed in order.

Landed: RecentFiles in Modelado.Inference (RecentFilesTests), the drop zone's list in
MainWindow.Recent.cs fed by every open and save, and a case whose fixture stages a
settings.json filled with {files} (winwright's fill), which stands in for the restart:
two staged files and a gone one, read back in order. Left: the toolbar menu (a drop-down
on ToolbarOpen sharing FillRecent's rows), photo thumbnails (decoded small off the
window's thread, cached by path and time written), and the Jump List.

### §MOD99 MOD99 — The logos of the services the app drives

Ship the marks of Meshy AI, Ideogram and Blender as app resources (SVG converted to XAML
DrawingImage, or PNG at 1x, 2x and 4x), in a light and a dark variant where the brand
provides them. Each logo is taken from the owner's press or brand kit and used under
that owner's guidelines: unmodified, kept to its clear space, and never implying
endorsement. The kit's URL and terms are recorded beside each file, and the logos are
credited under Third-party software. Blender's logo follows the Blender Foundation's
logo usage page. Before shipping, ask Meshy and Ideogram whether their terms allow use
in a Store app, and drop any logo whose answer is no. ICloudProvider gains a Logo next
to DisplayName, so every place that names a service can show its mark: the Ideogram
button on the drop zone, the Cloud 3D card, the status-bar budget chip, the consent
lines and the Blender actions. A local model has no product logo, and none is invented
for one. An architecture test checks that every logo file has its source and terms
recorded.

### §MOD100 MOD100 — Settings with pages, About on its own

AboutPanel in MainWindow.xaml is one ScrollViewer with eleven sections in sequence.
Split it in two. The status-bar button "About and models" becomes a gear, Settings, with
a left navigation as in Windows Settings, and About becomes a small page of its own.
Services: one card per service, Meshy, Ideogram and Blender, with its logo from MOD99, a
state chip (Connected, No key, Key rejected), the key's source from MOD96, this month's
spend as a bar against the budget, and Get a key. The usage detail folds under
"Details". Models: one tile per model, with a state badge (Downloaded, Not downloaded,
Downloading %) and size, grouped by the operation that uses it, with Download or Remove
on each tile and one "Download missing" button (MOD97). Folders and model sources fold
under Advanced. Processing: where inference runs, the GPU found, the provider choice.
About: icon, version, update source, licence, third-party software and what is
downloaded. Text shrinks to one line per item, and the explanations move to tooltips or
an info icon. Existing AutomationIds are kept where the control survives, so the cases
only add a navigation step. Cases press each page and find its key control. MOD96 and
MOD97 land inside these pages, whichever ships first.

## Block C — Existing code wired into the window

## Block D — Model downloads

### §MOD122 Spike: downloading gated models

The downloader fetches public files from Hugging Face by pinned commit and SHA-256. SF3D
and SPAR3D, accepted on 12/11/2026 despite the Stability AI Community License, are
gated: the user logs in to Hugging Face, accepts the licence, and Stability asks for
registration. Modelado must not redistribute the weights itself unless the licence
allows it.

A spike, time-boxed, answers: can the app use the user's Hugging Face token (OAuth or a
pasted token, stored with DPAPI) to fetch a gated file; how it learns the licence is
accepted; whether the exported ONNX may be published in Modelado's own repository under
the licence or must be produced on the user's PC from the gated weights; and where
registration happens.

Done when docs/model-downloads.md records the answers and the flow a download wizard
would follow, with a throwaway prototype fetching one gated file.

### §MOD123 The model download wizard

About has Download everything and per-model downloads. Models with conditions
(registration, attribution, a revenue cap) need more: what the model does and how big it
is, its licence in plain words and the full text, a consent the app records, the Hugging
Face login or token when the file is gated, and the progress and checks the downloader
already does.

A wizard, built from MOD122's flow, does that for any model whose manifest entry carries
conditions, and stays one click for the permissive ones. It also lists models available
but not installed (overlapping MOD97).

Done when SF3D (or the gated test file) can be installed through the wizard on a clean
PC, with consent recorded, and a permissive model still installs in one click.

### §MOD125 Licences with conditions

build_manifest.py and publish_models.py accept only MIT, Apache-2.0, BSD-2-Clause,
BSD-3-Clause and CC-BY-4.0. On 12/11/2026 the author accepted the Stability AI Community
License for SF3D and SPAR3D: commercial use allowed under US$1M annual revenue,
registration, a Powered by Stability AI notice, and no training of other foundation
models on the outputs.

The manifest gains a licence class (permissive, or with conditions) and the conditions a
model carries (attribution text, registration URL, revenue cap, use restrictions), which
the download wizard shows and the attribution in the window uses. The gate accepts
listed conditional licences and still refuses non-commercial ones (CC-BY-NC) and
regional exclusions (Hunyuan3D).

Done when the manifest can carry a Stability-licensed model with its conditions and the
gate refuses a CC-BY-NC one, with tests.

### §MOD126 Attribution for conditional models

The Stability AI Community License requires a Powered by Stability AI notice. Modelado
shows model licences only in About.

When a model with an attribution condition is installed and used, the result panel and
About show its notice, the export metadata (GLB asset, 3MF metadata) names the model,
and the README and the Store listing say which optional models carry conditions.

Done when a result made with a Stability model shows the notice in the window and in its
exported file metadata, and no notice appears for permissive models.

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

### §MOD77 MOD77 — Data folders under the package's file system redirection

`ModelLibrary` downloads to `%LOCALAPPDATA%\Modelado\models` unless a folder is chosen,
and `SettingsFile`, `CloudKeys` and `CloudLedger` sit beside it. In a full trust package
Windows redirects new files under AppData into the package's private `LocalCache`.
Inside the process nothing changes, but About's "open folder" starts `explorer.exe`,
which runs outside the package and sees the real, empty path; the sizes and the
remove-all question would name a folder the person cannot find; and the files go away on
uninstall, which the consent text should say.

Under a package (MOD74's PackageIdentity), name and open the redirected path,
`%LOCALAPPDATA%\Packages\Alegauss.Modelado_d7cpypw1jc0bm\LocalCache\Local\Modelado`, and
say the downloads are removed with the app. A folder the person chose stays outside the
redirection and keeps working. Turning redirection off with `unvirtualizedResources` is
the other way, but it is a second restricted capability to justify, so it is kept only
if this one fails.

Checked 11/11/2026 on the host (MOD78's cases-packaged): About names the LocalCache
folder and says downloads go with the app, and a key the CLI alias stored is decrypted
to send a job the window then counts. Left, for a desk with no Modelado data of its own
(the host has the person's models and keys, which a download through the real
redirection would sit beside): Done when, on the registered layout, a model downloads,
About opens the folder holding it and reports its size, and the DPAPI keys still decrypt
after a restart.

### §MOD81 MOD81 — Captioned Store screenshots

Seen on the live listing (11/11/2026): at the Store's thumbnail size the four
screenshots read as one light window with the Export card, the model small in the middle
and no word saying what each shows. claude-tray's listing (T412) composes each capture
under its caption at 1920 x 1080 on a branded backdrop; Modelado follows it, with what a
3D app needs of its own.

- `tools\Modelado.Icon --store-shot <capture> <out> <headline> [<photo>]` draws the backdrop in the
  icon's blues, the brand mark (icon and name) in the top-left corner, the caption as the headline,
  the window below it with rounded corners and a shadow, and, where a photo is given, that photo as a
  card beside the window, so the shot says "this photo became this model".
- `build\store-shots.ps1` captures the window smaller (about 1440 x 900), so the interface is not
  shrunk below legibility, turns each model to a three-quarter view with a drag in the viewport, and
  brings the card the caption is about into view through UI Automation: the 3D Object card, the
  Relief card, Edit and improve with History after a Simplify and a Smooth, and Export with STL in
  millimetres.
- The captions in `build\msix\store-listing.md` become one per card shown, and the raw captures stay
  beside the composed ones.

Done when `artifacts\store-shots` holds the composed screenshots and they are in a
submission.

### §MOD82 MOD82 — Super hero art for the listing

The Store shows a listing's trailers only when the listing also has the 16:9 Super hero
art (3840 x 2160), and Modelado's `--store-art` draws the box, the poster and the tiles
but not that one. claude-tray draws it without text (`hero-16x9-3840x2160.png`), because
the Store sets the title over it. Modelado's should show what the app makes: a generated
model on the icon's blue, not the icon alone, rendered from a demo model through the
same renderer `modelado-cli --preview` uses, so it is redrawn when the renderer changes.
Keep the left third and the bottom quarter quiet, where the Store lays its title and
buttons.

Done when `--store-art` writes `hero-16x9-3840x2160.png` and it is in a submission.

### §MOD84 MOD84 — A trailer for the listing

A 3D modeller's listing shows still pictures of models that can be turned, and the Store
takes up to 15 trailers (MP4 or MOV, 1920 x 1080, under 2 GB, about 60 seconds
recommended, each with a 1920 x 1080 PNG thumbnail), shown at the top of the listing
when the Super hero art is there (MOD82).

The trailer, 30 to 45 seconds, in the screenshots' look (MOD81): a photo opened and Generate 3D
object pressed, the model turning, the View modes switched, a relief turning, a Refine step with its
History, then Export. Made so it can be remade when the interface changes:
- the turning shots are frames drawn by the app's own renderer, the camera stepped a fixed angle per
  frame, not a hand-held screen recording;
- the steps with clicks are captured from the window on the VMware guest, as the screenshots are;
- each part carries its caption in the screenshots' headline style, and ffmpeg joins the frames into
  an H.264 MP4 at 1920 x 1080, 30 fps.

Done when the trailer and its thumbnail are in a submission and play on the listing.

### §MOD92 MOD92 — Store listing in six languages

Asked by the owner on 11/11/2026: a Store page in English, Brazilian Portuguese, German,
French, Spanish and Simplified Chinese. Partner Center's "Manage additional languages"
adds listings in languages the package does not declare; each needs a title, a
description and at least one screenshot.

- `build\msix\store-listing.md` holds one section per language (`en-us`, `pt-br`, `de-de`, `fr-fr`,
  `es-es`, `zh-cn`) in claude-tray's shape: short description, description, features, screenshot
  captions, keywords, copyright. Each caption line is the screenshot's headline, stressed words in
  [brackets], then ` | ` and the line under it; the Store gets the headline without brackets.
- Until Block K translates the interface, every non-English description says the interface is in
  English, so nobody installs expecting otherwise; when K ships, that sentence goes and the window is
  captured in each language.
- `build\store-listing-lib.ps1` reads the sections; `store-shots-vm.ps1` captures the window once and
  composes it per language into `artifacts\store-shots\<language>\`, with a CJK font where the text
  needs one; `build\store-listing-csv.ps1`, after claude-tray's, fills Partner Center's exported
  listing CSV from the file, since its import takes text but refuses images named by file.

Done when the six listings are live on the Store page.

## Block G — Public site for 3D modelling

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

## Block I — Blender integration: AI tools for existing Blender models

### §MOD70 MOD70 — Publishing the add-on on extensions.blender.org

From the portfolio review of 30/10/2026 (visibility, `produtos/modelado.md`). People who
use Blender look for add-ons on Blender's extensions platform (extensions.blender.org)
and install them from inside Blender; the Modelado add-on (Block I) exists only inside
this repository and the release, so that audience never meets it. The add-on already has
a `blender_manifest.toml` in the extensions format, which makes the submission mostly a
matter of meeting the platform's rules.

Points to settle before submitting: the add-on drives `modelado-cli`, an external
Windows program, so the listing must say that Modelado has to be installed and that it
works on Windows only, and the reviewers must accept an add-on that depends on it; every
permission it uses is declared (it writes temporary files, and Meshy's cloud jobs leave
the PC through the CLI); the version and the website point at the release and at the
site; and the screenshots show the round trip (Edit in Modelado, Send to Blender). If
the platform refuses add-ons that need an external program, the refusal is recorded and
the add-on stays in the release.

Those points are settled in `blender/LISTING.md`, and the manifest passes `extension
validate` on every gate; only the upload is left. Done when the add-on is submitted to
extensions.blender.org with the listing written here, or the platform's reason for
refusing it is recorded.

## Block J — Local model quality: past TripoSR

### §MOD80 MOD80 — Exporting and measuring TripoSG

MOD71's evaluation (`docs/local-3d-models.md`) found TripoSG (VAST, MIT) the one newer
open image-to-3D model that passes the licence rule with only standard ONNX operators: a
DINOv2 encoder, a rectified-flow transformer and an SDF VAE decoder.

Sized 11/11/2026 from the repository listing: FP32 weights are 7.5 GB (transformer 5.5,
DINOv2 1.2, VAE 0.9), about 3.8 GB in FP16, so 8 GB is plausible but unmeasured. The
transformer is past protobuf's 2 GB limit even in FP16, so its ONNX needs external data
files, which the manifest, downloader and `meta.json` (one file per variant) do not
handle yet: that is the first slice to design.

Then export the way `scripts\export_models.py` exports TripoSR: one ONNX per part,
checked against PyTorch on the model's test image, FP32 and FP16. The sampling loop and
marching cubes stay in C#. Measure on the host's RTX 4060 with DirectML and on the CPU,
on TripoSR's test photos: peak video memory and RAM, time per object, and the mesh
beside TripoSR's. TripoSG makes no colour; the photo projection colours the front.

Done when the measurements and side-by-side are in `docs/local-3d-models.md` with a
verdict: offer TripoSG as a "high quality" 3D Object (its own line), or record why not
and try Step1X-3D's geometry stage.

### §MOD128 Depth Anything 3 Mono Large for reliefs

Depth Anything V2 Base and Large are CC-BY-NC, so Modelado uses Small (README line 404).
Depth Anything 3 (ByteDance, 14/11/2025) published DA3MONO-LARGE and DA3METRIC-LARGE
under Apache-2.0 per its official table; the multi-view LARGE and GIANT are CC-BY-NC and
stay out. Community exports exist (Jens-Duttke/Depth-Anything-3-MONO-ONNX;
Heliosoph/da3-base-onnx in fp16 within 5e-4 of fp32, fixed 504 px input).

Export DA3 Mono Large from the official weights with a parity check, handle its fixed
input size in the relief pipeline, run it on DirectML and the CPU, compare with V2 Small
in the harness, and offer it as the High quality relief model (about 700 MB in fp16) if
it wins.

Done when the harness shows the comparison and, if adopted, the Relief card's High
quality uses it on both paths.

### §MOD129 Evaluate MoGe-2 for reliefs

Microsoft's MoGe-2 (35M, 104M and 331M) has MIT code, MIT weights at least for
moge-2-vitb-normal (the other variants' weights to check), and an official ONNX export
(docs/onnx.md) with post-processing outside the graph. It predicts geometry and normals,
which could sharpen relief detail.

Check each variant's weight licence, run the official ONNX on DirectML and the CPU, port
its post-processing to C#, and compare with V2 Small and DA3 Mono Large in the harness.

Done when the harness table includes MoGe-2 with a verdict, and its licence per variant
is recorded.

### §MOD130 Stable Fast 3D as a textured 3D Object

TripoSR colours vertices from a 64 by 64 grid, so detail blurs. Stable Fast 3D
(Stability AI, 1B) outputs a UV-unwrapped textured mesh, measures Chamfer 0.137 against
TripoSR's 0.145, and has been exported to ONNX and run on iPhone (jc-builds/sf3d-ios,
fp16 encoder within 4.4e-4). Its UV unwrapper and texture baker are custom C++ (CPU and
Metal backends), which Modelado must port to C# or replace.

Export SF3D's networks with parity checks, write the pipeline (marching cubes, unwrap,
bake) in C#, deliver the weights through the wizard under the Stability licence with
attribution, and compare it in the harness.

Done when SF3D runs on DirectML and the CPU as a 3D Object option, installed through the
wizard, with its comparison and notice.

### §MOD131 Spike: SPAR3D in ONNX

SPAR3D (Stability AI, 08/01/2025) adds a point-diffusion stage before an SF3D-like mesh
stage and measures Chamfer 0.120 and F-score 0.584 on GSO, against TripoSR's 0.145 and
0.501. It has an official CPU path (very slow) and an experimental Apple MPS one; no
ONNX export is known. It uses the same Stability licence as SF3D.

A time-boxed spike tries exporting the point-diffusion model and its sampler loop, with
the loop in C#, and measures steps, time and memory on DirectML and the CPU. If it
works, the mesh stage reuses MOD130's.

Done when docs/local-3d-models.md records whether SPAR3D exports and runs, at what cost,
and files the follow-up or the reason not to.

## Block K — Translated interface

### §MOD87 MOD87 — Brazilian Portuguese (pt-BR)

Add Strings.pt-BR.resx with every key from the English file. Terms of the trade (mesh,
GLB, OBJ, STL, texture, prompt) follow the usage of Blender's pt-BR translation, and
product names are never translated. A test checks that the file has the same keys and
the same {n} placeholders as the English one, a check that every language task reuses.
One winwright case launches in pt-BR and confirms that the window title and the main
buttons render without clipping. A native speaker reviews the text before the task
ships.

Landed: the file, the parity test (StringResourceTests), pt-br in the MSIX manifest, and
a case reading the Portuguese names. GenerateTemplate stays English, being the prompt
the image service reads. For the review, the choices worth a second look: View as
"Visualização" (or "Vista"); Remesh/Retexture as "Remalhar"/"Retexturizar"; passes as
"passadas" (or "iterações"); watertight as "fechada (estanque)"; Candidate as "Opção";
"Salvo: {0}"; built-in as "embutido" (or "integrado"); budget "quase
esgotado"/"esgotado"; release as "versão" (or "lançamento"); job(s) as "tarefa(s)". The
caption-fit check waits on winwright (WW624): "within" holds only that a button stays
inside its panel.

### §MOD88 MOD88 — German (de)

Add Strings.de.resx, held by the key and placeholder parity test from MOD87. German
strings run 30 to 40 percent longer than English, so buttons, the side panel and the
status bar may need to grow or wrap rather than clip. Fix any width that MOD87 did not
expose in the shared XAML, not in a German-only style. A winwright case launches in
German and checks the main window for clipped text. A native speaker reviews the text
before the task ships.

Landed: the file, de in the MSIX manifest, and a case reading the German captions with
the 3D Object button held inside the side panel; no width had to change. For the review:
mesh as "Netz" and vertex as "Vertex"; Remesh/Retexture as "Neu vernetzen"/"Neu
texturieren"; clay view as "Modell in Ton"; flat base "Flacher Boden" against solid base
"Massiver Sockel"; watertight as "wasserdicht"; Candidate as "Vorschlag"; Pause as
"Anhalten"; Best as "Beste"; LanguageNow as "Sprache des Fensters: {0}."; no "Sie"
address anywhere. The caption-fit check waits on winwright (WW624).

### §MOD89 MOD89 — French (fr)

Add Strings.fr.resx, held by the key and placeholder parity test from MOD87. French puts
a non-breaking space before colons, question marks and exclamation marks, so strings
that end in those characters carry the space in the resource rather than relying on the
layout. A winwright case launches in French and checks the main window for clipped text.
A native speaker reviews the text before the task ships.

Landed: the file (non-breaking spaces before : ? ! ; and %, « » quotes, typographic
apostrophes, "vous"), fr in the MSIX manifest, and a case reading the French captions
with the 3D Object button inside the side panel. For the review: quads as "quadrangles";
maillage/sommet/arête and "étanche"; "Remplir les trous"; "Remailler"/"Retexturer";
Mirror as "Miroir" (or "Symétrie"); Best as "Optimal"; Candidate as "Proposition";
"cloud" against "en ligne"; "slicers" left in English; and "{0} : annulé" assuming a
masculine step name. The symbol-only values "{0}: {1}" keep no space before the colon.
The caption-fit check waits on winwright (WW624).

### §MOD90 MOD90 — Spanish (es)

Add Strings.es.resx as neutral Spanish, held by the key and placeholder parity test from
MOD87. Avoid regional terms, so that es-ES and es-MX both fall back to it without a
regional file. A winwright case launches in Spanish and checks the main window for
clipped text. A native speaker reviews the text before the task ships.

Landed: the file ("usted" imperatives, "este equipo", no "ordenador" or "vosotros"), es
in the MSIX manifest, LanguageSettingTests holding es-ES and es-MX to it, and a case
reading the Spanish captions with the 3D Object button inside the side panel. For the
review: Mirror as "Reflejar"; quads as "cuadriláteros"; watertight as "hermética" and
"Rellenar huecos"; "Remallar"/"Retexturizar"; passes as "pasadas"; slicers as
"laminadores"; model sources as "Orígenes de modelos"; WorkingOffline as "Trabajo sin
conexión"; "300 000" with a space. The caption-fit check waits on winwright (WW624).

### §MOD91 MOD91 — Simplified Chinese (zh-Hans)

Add Strings.zh-Hans.resx, held by the key and placeholder parity test from MOD87. zh-CN
and zh-SG must resolve to it. The font set in Styles.xaml needs a CJK fallback
(Microsoft YaHei UI), or WPF falls back to a glyph set that renders unevenly. Check that
every custom font in the styles has that fallback. Chinese text is short but tall, so
the case checks vertical clipping in rows of fixed height. A native speaker reviews the
text before the task ships.

Landed: the file (mainland usage, full-width punctuation in sentences, "、" as the list
comma), zh-hans in the MSIX manifest, Microsoft YaHei UI ending every font family (held
by StringResourceTests), zh-CN and zh-SG held by LanguageSettingTests, and a case
reading the Chinese captions with the 3D Object button inside the side panel. For the
review: ListAnd as " 和 " with spaces; clay view as 黏土模型 (or 白模); Remesh/Retexture as
重构网格/重做纹理; open mesh as 非封闭 and watertight as 水密; the sampling grid as 采样网格; 秒 and 分钟
for s and min; budget states 即将用完/已用完/未超出. The row-height check waits on winwright
(WW624).

## Block L — Printable objects: relief keepsakes, 3MF and print-ready meshes

### §MOD110 Open in slicer

Modelado has Open in Blender, but a print still ends with saving a file, opening the
slicer and finding it. Bambu Studio embeds MakerLab's keychain, lithophane and relief
tools, so its users never leave the slicer; Modly's print epic lists an OrcaSlicer
hand-off.

Modelado finds the slicers installed on the PC (Bambu Studio, OrcaSlicer, PrusaSlicer,
UltiMaker Cura, from their registry entries or the Store) and offers Open in <slicer>
beside Open in Blender, saving a 3MF to a temporary file and passing it on the command
line. The choice is remembered.

Done when Open in slicer opens the current model in each of the four slicers present on
a test PC, and the button is absent when none is installed.

### §MOD143 Engraved text on reliefs

`ReliefTextStamp` in `src/Modelado.Geometry/ReliefText.cs` (MOD106) only raises letters
above the relief's highest point. A plaque with a smooth band, or a keychain's back,
often wants the name cut in instead.

Add an engraved mode to `ReliefText`: the covered vertices are set to the surface there
minus a depth in mm, clamped so the cut never reaches the base (at least 0.6 mm of
material left under it). Placing it is the same layout as raised text. The CLI takes it
as `--text-engrave-mm D` in place of `--text-raise-mm`, the Relief card as a Raised /
Engraved choice beside the letter height.

Done when a plaque with an engraved date exports as one closed mesh whose letters sit
below the surface around them, and a cut deeper than the relief allows is refused in
words.

### §MOD145 Curved and framed lithophanes

`Lithophane.Build` in `src/Modelado.Geometry/Lithophane.cs` (MOD108) makes a flat panel
only: the relief mesher's height grid over a flat solid base. Curved lithophanes, bent
around a vertical axis so they stand on their own, and cylinders that go over a tea
light, are the shapes most lithophane tools offer next to flat. Neither has a frame, and
a thin panel with no rim warps and has fragile edges.

Bend the finished flat panel rather than build a second mesher: map each vertex's x to
an angle around a vertical axis (x / radius), and push it out along that angle by its z,
so the back becomes the inner wall and the thickness stays radial. A curve takes an arc
in degrees (default 120); a cylinder is 360, which needs the panel's left and right
edges welded into one seam, so the grid's first and last columns must share vertices
before solidifying. A frame is a raised rim at the maximum thickness, a few mm wide,
stamped into the height grid the way `Relief`'s border (MOD105) is, which also gives a
curved panel a flat bottom edge to stand on.

CLI: `--shape flat|curve|cylinder`, `--arc DEG`, `--frame-mm W`.

Done when a curved and a cylindrical lithophane with a frame export as one closed mesh,
and the curve stands on a table on its own.

### §MOD146 Thickness check

After Make printable (MOD111) and Lay flat (MOD112, `src/Modelado.Geometry/LayFlat.cs`),
the problem left is parts thinner than the nozzle can print: ears, tails, handles. They
break off or vanish in the slicer, and nothing in Modelado says where they are. Modly's
print epic lists a printability check for the same reason.

A thickness check measures, at each vertex, how far a ray cast inward along the reversed
vertex normal travels before it leaves the model again (the local wall thickness), in mm
at the print size Export uses (the STL size, as Make printable reads its voxel).
Vertices under a chosen minimum (default 1.2 mm, three 0.4 mm lines) are coloured red in
the view, and the Edit card says how many and how much of the surface that is. A
brute-force cast is vertices × triangles, far too slow for a 100k-triangle model, so the
triangles go into a uniform grid or a small BVH first.

An option to thicken them can follow: Make printable with a dilated inside field
(MOD111's voxel votes, grown by half the missing thickness before marching cubes) is the
cheapest way.

CLI: `edit --check-thickness MM` prints the count and writes the colours into the
output.

Done when a test model with a 0.5 mm fin shows the fin red and nothing else at 1.2 mm,
in the window and the CLI.

### §MOD149 A bundled font for relief text

Relief text can be drawn in an installed TrueType font (MOD142): `--font` on the CLI,
and the Relief card's Font list, which reads the system and per-user font folders
through `TrueTypeFont.FullName`. A machine whose fonts lack a script, or a print the
user wants to reproduce on another machine, still has no font the app itself carries.

Bundle one OFL font (a sans with Latin Extended, for example Noto Sans or Inter) under
`src/Modelado.App/Fonts/` with its `OFL.txt` beside it, copied to the output and the
MSIX. List it first on the Relief card's Font list, after the built-in capitals, and let
the CLI take its name as well as a path for `--font`. Mention the licence in the About
box's notices.

This adds a third-party file the app redistributes, so it waits for the user's go-ahead,
as the third-party model downloads (MOD128, MOD129) do.

Done when the font and its licence ship in the build and the MSIX, the Relief card lists
it first, and a case writes a lower-case name in it on a medallion with no font
installed.

## Block M — The CPU and integrated-GPU path

### §MOD116 Ship the light encoder for the CPU

If MOD115 finds an INT8 variant close enough to fp32, the CPU path should use it. The
manifest gains the variant, ModelLibrary picks it for the CPU in Standard quality and
keeps fp32 for High, and About shows which one is installed and lets the user fetch the
other.

If the loss is visible, it ships as a Fast quality instead, and the CPU keeps fp32 as
the default.

Done when a CPU-only install downloads and runs the chosen variant, the README and the
site state the new size and time, and the speed table shows both.

### §MOD147 Automatic and integrated GPUs

MOD121 measured the i7-14700's integrated Intel UHD Graphics 770 through DirectML
(`modelado-cli --gpu 1`, README's integrated table): 3D Object took 65 s at Standard
(FP16) and 35 s at High (FP32), against 15 s on the same PC's CPU. Relief ran in 2 to 9
s, slower than the CPU's 1 s. FP16 is slower than FP32 there, the opposite of a discrete
GPU.

Automatic (`ExecutionProviders.Resolve`) picks DirectML whenever the build has it, and
`GpuAdapter.ForDevice(0)` is whatever DXGI lists first. On a PC whose only GPU is
integrated, that is the slow path, and the user is told only "DirectML · Intel UHD
Graphics 770" on the chip.

Automatic should weigh the adapter. Dedicated memory does not tell: `GpuAdapter` read 2
GB for the UHD 770 (the CLI named it "Intel(R) UHD Graphics 770, 2 GB"). DXCore does:
`IDXCoreAdapter` answers `DXCoreAdapterProperty::IsIntegrated`. Where the only hardware
adapter is integrated, Automatic resolves to the CPU and says why on the chip ("CPU: the
integrated GPU is slower here"), and About's DirectML choice still forces it. On a
hybrid laptop where DXGI lists the integrated GPU first, Automatic could prefer the
discrete adapter through `IDXGIFactory6::EnumAdapterByGpuPreference` (high performance)
instead of index 0.

Done when Automatic on a PC whose only GPU is integrated runs on the CPU and says why,
tested with the adapter's figures stood in, and a hybrid PC's Automatic names its
discrete GPU.
