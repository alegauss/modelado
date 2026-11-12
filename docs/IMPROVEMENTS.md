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

### §MOD98 MOD98 — An icon rail with one operation shown at a time

MainWindow.xaml stacks 3D Object, Relief, Cloud 3D, Edit and improve, Export, History
and Quality in one ScrollViewer. Add a narrow vertical rail of icons, about 48 px wide,
on the panel's edge next to the viewport, with one Segoe Fluent icon per card, a
tooltip, an AutomationId (Rail-Object, Rail-Relief, …) and a selected state. The panel
shows only the selected card, so the viewport stays visible and usable, unlike a modal,
which the user weighed and set aside because these settings are tuned while watching the
model. The choice persists in SettingsFile. A card's essentials (its main control and
its Generate button) stay on top, and the rest folds into Advanced, as on 3D Object
today. A dot on an icon means its model still needs a download, so the "One-time
download" note leaves the card. Cloud 3D hides with its rail icon when no key is held.
The rail is keyboard reachable (arrows move, Enter selects). Existing winwright cases
first select the rail icon of the card they drive. A new case checks that pressing each
icon shows that card alone, and that the choice survives a restart.

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

### §MOD97 MOD97 — Every model About knows of, downloaded or not

FillAbout in MainWindow.About.cs builds both the Models list and the Downloads list from
library.Installed(), so a model that was never fetched, or was removed, is simply
absent. Meanwhile, OnAboutDownloadAll fetches ObjectReconstruction.Models,
ReliefReconstruction.Models(true) and Upscaling.Model without saying which ones are
missing. Build the Downloads list from that same set, together with any model from an
added source, and give each row its state: "Downloaded · 800 MB" with Remove, or "Not
downloaded · 800 MB, used by Make 3D" with Download. Rows flip in place while a download
runs. The Download everything button names what it will fetch and its total size
("Download 2 missing, 847 MB") and disables itself when nothing is missing. The Models
list above keeps describing licences, and also shows a missing model as missing. A case
removes one fixture model, finds its row marked as not downloaded with a Download
button, downloads it, and finds the row back to downloaded.

### §MOD124 ONNX external data in the manifest

ONNX files above 2 GB must keep their weights in external data files beside the graph.
The manifest, the downloader, meta.json and the published repository handle one file per
model, so TripoSG (7.5 GB in fp32, about 3.8 GB in fp16, MOD80) cannot be shipped, nor
any later large model.

The manifest entry lists every file of a model with its size and SHA-256; the downloader
fetches and resumes each; ModelLibrary loads the graph with its data beside it;
publish_models.py and verify_published.py handle the set.

Done when a test model split into external data publishes, downloads, verifies and runs,
and MOD80 can move out of deferred.

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

### §MOD101 Running without NVIDIA is why Modelado is found

Modelado's audience arrived on its own from 03/11/2026, mostly without a referrer; the
site's main named source is chatgpt.com. A test on 12/11/2026 found the question behind
it: asked for a "free offline app photo to 3D model Windows without NVIDIA GPU / AMD /
CPU only", ChatGPT puts Modelado first as the "best fit", for TripoSR and Depth Anything
V2 over DirectML. Asked the same without the GPU clause, it recommends Modly first,
which needs an NVIDIA card with 8 GB or more, and never names Modelado.

So running without NVIDIA is the reason Modelado is found, and the public text hides it.
The same answer marks Modelado "Check build" in its CPU-only column, while Presidium
Shape Studio, which states CPU-only operation in plain words, gets "Yes" and is the one
ChatGPT tells a CPU-only machine to test first. The facts are already measured, but
buried: line 96 of the README, and mid-page on the site.

What changes is where it is said: the first screen of the README, the site's title,
description and hero, the Store listing's first lines, and an llms.txt for the site,
each stating that no NVIDIA GPU is needed, that AMD and Intel graphics run through
DirectML, and that a CPU-only PC works, more slowly, with the measured time. Done when
ChatGPT, asked the CPU-only question again, can answer "Yes" in that column from the
page itself.

### §MOD132 Windows 3D Viewer may be gone

README line 27 names Windows 3D Viewer first among the viewers that open GLB. A
secondary source reports that Microsoft removed 3D Viewer from the Store on 01/07/2026;
this was not confirmed with Microsoft on 12/11/2026.

Check Microsoft's own pages and a clean Windows 11 install. If 3D Viewer is gone, name
viewers that still exist (Blender, a browser glTF viewer, Modelado itself), and check
the site and Store listing for the same mention.

Done when the README, the site and the listing name only viewers that can be installed
today, with the source of the check recorded.

### §MOD133 Unlimited, no credits, nothing uploaded

The site says nothing is uploaded. It does not say what that means against the tools
people compare with: MakerLab's image to 3D charges credits at generation since
mid-2026, with complaints about paying for unprintable results, needs an account and
moderates photos; Meshy and 3D AI Studio sell credits; Microsoft's Copilot 3D left with
Copilot Labs on 29/09/2026.

The site, the README's first screen and the Store listing say Modelado makes unlimited
results for free, with no account, no credits and no moderation, because it runs on the
PC. Printing is one of its uses beside games, Blender and the web, not its only one. The
wording joins MOD101's no-NVIDIA claim.

Done when the three places carry the message in the first screen, and MOD92's
translations include it.

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

### §MOD93 MOD93 — The Ideogram button once its panel is open

OnDropGenerate in MainWindow.Generate.cs only toggles GeneratePanel's visibility, and
the button keeps the same text, "Generate a photo with Ideogram…", in both states. With
the panel open, the button sits above the fields it already showed, and a second press
either collapses them with no visible cue or, as the user saw, appears to do nothing.
Reproduce this first on the guest, because the report says nothing happens, while a
collapse should still be visible. While the panel is open, the button either hides or
becomes an explicit "Hide" or "Cancel" toggle, and the panel takes focus on its first
field ("The object"). A winwright case presses the button, finds the panel and the
prompt field focused, presses the toggle, and finds the panel collapsed and the button
restored.

### §MOD95 MOD95 — Keep every generated photo where the user can find it

ShowCandidatesAsync keeps the candidates in memory only. OpenCandidateAsync writes only
the pressed candidate, to %LOCALAPPDATA%\Modelado\generated, which the MSIX package
virtualises under the package's LocalCache, and nothing in the window names that folder.
Write every candidate as soon as the job returns, carrying its prompt and seed in the
PNG as today, to Pictures\Modelado (the user's own library, visible outside the
package), and name the files after the prompt and the seed. The drop zone gets a
"Generated photos" strip of the most recent ones, which open as if dropped, plus a link
that opens the folder in Explorer. A job that fails to save says where it tried to
write, and the images stay in memory until the user saves or discards them. Cases check
that all four candidates of a fixture job land on disk before any is pressed, and that
the strip lists them.

### §MOD96 MOD96 — Where each cloud key comes from

FillCloudServices in MainWindow.Cloud.cs prints "A key is stored." whenever
CloudKeys.Has is true, then always shows an empty PasswordBox with Save and test. The
stored key is the DPAPI file under %LOCALAPPDATA%\Modelado, written by About or by the
CLI. Nothing reads MESHY_API_KEY or IDEOGRAM_API_KEY, although a user with those
variables set expects them to be used. CloudKeys resolves a key from the environment
variable first (MESHY_API_KEY, IDEOGRAM_API_KEY), then from the stored file, in the app
and in the CLI alike, and reports which source answered. About states the source and the
key's last four characters: "Using IDEOGRAM_API_KEY from the environment (…a1b2)" or
"Stored by Modelado (…a1b2)". When the key comes from the environment, the box and
Remove are hidden, and a line says to change the variable instead. When it is stored,
the box is collapsed behind a "Replace key…" button. Tests cover the order of
precedence, and a case with an environment key finds the source line and no box.

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

### §MOD127 A model comparison harness

MOD71 judged models from their papers; nothing ran. The decisions ahead (an INT8
encoder, Depth Anything 3, MoGe-2, SF3D, SPAR3D and TripoSG in MOD80) all need the same
measurement.

A script, using modelado-cli, runs a fixed set of test photos (objects with backs, thin
parts, portraits and landscapes for relief, licensed for publication) through each model
and variant, and records time, peak memory, provider, triangle count, closedness and,
against a reference mesh or the fp32 result, Chamfer distance, plus turntable renders
side by side. Results go to docs/local-3d-models.md.

Done when one command produces the table and renders for TripoSR fp32 and fp16 and Depth
Anything V2 Small, ready for each new candidate.

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

### §MOD134 Library sentences in the window

MOD85 moved every string Modelado.App writes into Properties/Strings.resx, but the
window also shows sentences the libraries compose in English: CloudLedger's budget note
on every consent line (CloudSpending.Note), CloudJobs.Label for a cloud job's title and
the History step it leaves, ExecutionProviders.Label on the provider chip and the reason
Resolve gives for falling back to the CPU, ModelRunner.Notice in the last-run tooltip,
each provider's KeyCheck message, and MeshImport's notice about baked textures. Under a
translation these read in English beside translated text.

The libraries are shared with the CLI, which stays English, so they should not read the
app's resources. Each one returns what happened as data (an enum or a record: the budget
state and the amounts, the capability, the device name, the fallback cause), and the app
turns it into a sentence from Strings.resx; the CLI keeps its own English wording. Where
a service's own text arrives (a KeyCheck message from the API, an exception message), it
is shown as the service wrote it, inside a translated frame. Done when
StringResourceTests-style checks find no library sentence on the window's consent lines,
chips, History or About, and the CLI's output is unchanged.

## Block L — Printable objects: relief keepsakes, 3MF and print-ready meshes

### §MOD102 Relief dimensions in millimetres

The Relief card sets depth (5 to 60%) and base (0 to 20%) as a share of the width, and
millimetres appear only at export, where STL defaults the longest side to 100 mm.
Someone printing a keychain or a plaque thinks the other way round: 50 mm wide, 3 mm of
relief on a 2 mm base. Every later print feature in this block (keychain, medallion,
stand, lithophane) needs sizes that mean something on the plate.

The card gains a printed size (width or longest side in mm) and depth and base in mm,
derived from the same mesh, so the export no longer asks for a size it already knows.
The CLI takes the same values. The proportional controls can stay as the default for a
model meant for the screen.

Done when a relief made at 60 mm wide with 3 mm depth and 2 mm base measures exactly
that in the exported STL and in a slicer.

### §MOD103 3MF export with units

Modelado exports GLB, OBJ, PLY and STL. STL has no unit, so the size only survives when
the slicer assumes millimetres, and it drops colour. 3MF, the 3D Manufacturing Format,
records the unit, can keep vertex or texture colour, and is what Bambu Studio,
OrcaSlicer and PrusaSlicer save and prefer. Modly's print epic (lightningpixel/modly
#429, 12/11/2026) lists 3MF for the same reason, and MakerLab's lithophane tool already
exports it.

The export gains 3MF for both 3D Object and Relief, in millimetres, with the colour the
mesh carries, written by a small writer of the 3MF core spec (a zip with the model XML),
and the CLI gains the same format. Import can follow later.

Done when a 3MF from Modelado opens at the right size and colour in Bambu Studio,
OrcaSlicer and PrusaSlicer, and a test checks the file against the core spec.

### §MOD104 Keychain from a photo

MakerLab's Image to Keychain is cloud-only, needs an account and passes the photo
through moderation; a Bambu forum thread asks for an offline way to get the same result
and has no answer. The only offline route, Blender's Photo Relief extension, has no
keychain and takes height from brightness, not depth.

A keychain preset turns the relief into a tag: an outline (the cut-out object, a circle,
a rounded rectangle), a hole or a loop placed where the user drags it, a minimum
thickness that survives a pocket, and a size in mm (around 40 to 60 mm). The mesh stays
closed with the hole cut through it.

Done when a keychain made from a pet photo exports as one closed mesh with a hole that a
slicer prints without supports, and the preset is in the window and the CLI.

### §MOD105 Medallions and plaques

Today the relief covers the photo's rectangle, or the cut-out object with Object only.
Users of MakerLab's Relief Sculpture Maker ask on the Bambu forum to fit the relief in a
circular frame and cannot. A portrait gift usually is a round or oval medallion, or a
plaque with a raised border.

The Relief card gains a shape (rectangle, rounded rectangle, circle, oval) that crops
the relief, a raised border of a set width and height in mm, and an optional back that
stays flat for a magnet or glue. The border joins the relief in one closed mesh.

Done when a circular medallion and a bordered plaque export as single closed meshes at
the size asked, with the shape and border in the window and the CLI.

### §MOD106 Text on reliefs

Users of Bambu's Relief Sculpture Maker ask for text around the frame. Today adding a
name or a date to a Modelado relief means another program.

Text is raised or engraved on the base or the border: along an arc around a medallion,
on a straight line under a plaque, or on a keychain tag. It needs glyph outlines from a
font turned into a closed extruded mesh and merged with the relief, with a bundled open
font (OFL) and the option to pick an installed one. Height and depth are in mm.

Done when a medallion with a name along its arc and a plaque with a date underneath
export as one closed mesh that prints legibly at 1 mm of text height.

### §MOD107 A stand for reliefs

A plaque or medallion made in Modelado lies flat. To display it upright, the user models
a stand in Blender or prints an easel found elsewhere.

The Relief card gains an optional stand: a foot joined to the base at a chosen tilt
(around 10 to 20 degrees back), sized from the relief's width and thickness so it does
not tip, and a slot variant that prints separately and holds the plaque. Either way the
export is printable without supports.

Done when a plaque with a stand prints upright without supports and stands on a table
without falling.

### §MOD108 Lithophane mode

A lithophane is a thin panel whose thickness follows the photo's brightness, so the
photo appears when light passes through it. Cura, ItsLitho, 3dp.rocks, MakerLab and
Blender's Photo Relief already make them, so it does not set Modelado apart, but it is
one of the largest photo-print searches and the relief mesher already builds a height
grid with a solid base.

The Relief card gains a Lithophane mode: height from inverted brightness instead of
depth, minimum and maximum thickness in mm (around 0.8 and 3 mm), flat, curved or
cylinder shapes, an optional frame, and a resolution suited to a 0.4 mm nozzle.

Done when a flat and a curved lithophane print and show the photo against a light, with
the mode in the window and the CLI.

### §MOD109 Print orientation for reliefs

A user of Bambu's Relief Maker reported prints coming out standing, which needs supports
and costs detail. A relief prints best face up, its flat back on the plate.

On STL and 3MF export, a relief (and the keychain, medallion, plaque and lithophane
variants) is placed with its back on Z = 0 and its face up, centred on the origin,
unless the user asks otherwise. GLB for the screen keeps today's Y-up orientation.

Done when every relief variant opens in Bambu Studio, OrcaSlicer and PrusaSlicer lying
face up on the plate with no rotation needed.

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

### §MOD111 Make printable: a closed, manifold mesh

Marching cubes usually closes TripoSR's mesh, and a test asserts it for one model, but
nothing guarantees it after edits or for every photo. On the Bambu forum (05/11/2026),
files from MakerLab's image to 3D printed with empty layers, and the fix suggested was a
voxel remesh at 0.3 to 0.5 mm in Blender.

A Make printable action rebuilds the surface on a voxel grid in mm (default 0.4 mm),
keeps the largest shell or unions them, guarantees a closed two-manifold mesh, and
reprojects the colours. It reports what it fixed. The CLI gains the same.

Done when every mesh in the test set comes out of Make printable with each edge used
twice and no self-intersection, and slices with no empty layer.

### §MOD112 Thickness check and lay flat

After Make printable, two problems remain: parts thinner than the nozzle can print
(ears, tails, handles) and an orientation that needs supports. Modly's print epic lists
a printability check and lay flat for the same reason.

A thickness check marks, on the model, the regions thinner than a chosen minimum in mm
(default 1.2 mm), with an option to thicken them. Lay flat picks the largest flat face
as the base, or lets the user click one, and the flat base action can then cut the model
there.

Done when thin regions show in colour on a test model and lay flat puts each test model
on a face that slices without supports under its base.

### §MOD113 A base below 0.5% is not no base

MainWindow.Operations.cs shows no base for a value below 0.5% but passes the value
through, and Relief.Solidify adds walls and a back anyway (lines 64 and 172, found on
12/11/2026). Either the threshold applies to both, or the label shows the real value.

Done when a base below the threshold produces an open relief and says so, or the label
shows the thin base it builds, with a test for each side of the threshold.

### §MOD114 Closed reliefs for irregular masks

ReliefTests assert every edge used twice and a positive volume for an unmasked relief
and a rectangular mask. Object only cuts the relief along U-2-Net's outline, where two
triangles can meet at one vertex and leave the mesh non-manifold after Solidify. The
keychain and medallion work in this block relies on that outline.

Tests generate reliefs from several real cut-out masks (thin parts, holes, diagonal
touches) and assert closed two-manifold meshes; Solidify is fixed where they fail.

Done when those tests pass and a cut-out relief of each test photo slices without
errors.

## Block M — The CPU and integrated-GPU path

### §MOD117 State the real download size for the CPU

README lines 101 and 102 say 3D Object needs 1.1 GB (2 GB at high quality). That holds
for a GPU in Standard quality, which loads fp16; the CPU always loads the fp32 encoder
(ExecutionProviders.cs lines 60 and 61), so a CPU-only PC downloads about 1.85 GB. About
and the first-use download prompt should give the size for this PC, not the GPU's.

Done when the README, the site and the download prompt state the size per path (GPU,
CPU, High), and the prompt shows the size it is about to download.

### §MOD118 Measure memory

The README gives about 4 GB of free RAM or video memory, from the encoder sizes; no peak
has been measured, for 3D Object, Relief, or the editing steps, on the CPU or through
DirectML.

Measure peak working set and dedicated and shared GPU memory for each pipeline at each
grid and quality, on the CPU and on DirectML, and record them in the README and the
speed table.

Done when every pipeline at its default and highest setting has a measured peak on both
paths, written beside its time.

### §MOD120 DirectML choice and the CPU fallback

ExecutionProviders.cs line 11 says that choosing DirectML makes a GPU failure an error,
not a fallback. ModelRunner sees only the provider, not the user's choice, and falls
back to the CPU on load or run failure (lines 69 to 80 and 132 to 136). Auto should fall
back; an explicit DirectML should fail and say why.

Done when ModelRunner knows whether the provider was chosen or automatic, an explicit
DirectML failure is reported as an error, and a test covers both.

### §MOD121 Measure Intel and integrated GPUs

The site's speed table has two columns, a CPU (8 cores of an i7-14700 in a VM) and an
RTX 4060. Modly added AMD support through ROCm in v0.4.3 (04/10/2026), but only for
RDNA2 or newer discrete Radeons on Windows; Intel Arc, Intel and AMD integrated graphics
and older Radeons remain Modelado's own ground through DirectML.

Time and memory for 3D Object and Relief on an Intel integrated GPU, an Intel Arc if
available, and an AMD integrated GPU, through DirectML, added to the table, with any
failure fixed or documented. This needs access to that hardware.

Done when the table and README have at least one Intel and one integrated-GPU row,
measured.

### §MOD115 Measure an INT8 encoder

A public ONNX export of TripoSR (fernandotonon/QtMeshEditor-triposr-onnx) ships an INT8
encoder of about 430 MB against 1.7 GB in fp32, with a slight quality loss and no
numbers. Arm's mobile write-up kept the encoder in fp32 as quality-sensitive and
quantized only the decoder. Modelado's CPU users, the ones ChatGPT sends here, get the
1.85 GB fp32 download today.

Export the encoder with ONNX Runtime's dynamic and static INT8 quantization from the
official MIT weights, then run the comparison harness: time and peak memory on the CPU,
and the mesh difference against fp32 on the same photos. Also try INT8 for the decoder
alone.

Done when docs/local-3d-models.md records size, time, memory and the visible difference
of each variant, with a verdict on shipping it.

### §MOD116 Ship the light encoder for the CPU

If MOD115 finds an INT8 variant close enough to fp32, the CPU path should use it. The
manifest gains the variant, ModelLibrary picks it for the CPU in Standard quality and
keeps fp32 for High, and About shows which one is installed and lets the user fetch the
other.

If the loss is visible, it ships as a Fast quality instead, and the CPU keeps fp32 as
the default.

Done when a CPU-only install downloads and runs the chosen variant, the README and the
site state the new size and time, and the speed table shows both.

### §MOD119 Check memory before a run

Sessions stay loaded until the provider or quality changes, and nothing compares free
memory with what a run needs. On an 8 GB laptop, a 3D Object at a high grid can exhaust
memory after a long run.

With MOD118's figures, the app checks available RAM (and video memory on DirectML)
before a run and, when short, says so and offers a lower grid, Standard quality or the
CPU, or releases loaded sessions it does not need.

Done when a run that would not fit is stopped with that advice before it starts, tested
by forcing a low limit.
