# Roadmap (active backlog)

## Priority

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

- 📋 **MOD94** (deps: —) **There is no list of recently opened photos and models, so a file opened yesterday has to be found again by hand** — Every Windows editor offers recent files, and without them a generated photo or an edited model is easy to lose. → §MOD94
- 📋 **MOD98** (deps: —) **The Operations panel stacks all seven cards in one long scroll, so the one in use is buried among the others** — Every setting is read past every other, and the Generate button of the card in use is often below the fold. → §MOD98
- 📋 **MOD99** (deps: —) **The services the app drives (Meshy, Ideogram, Blender) appear only as text, with no logo anywhere in the window** — A logo tells which service an action or a charge belongs to at a glance, where a name has to be read. → §MOD99
- 📋 **MOD100** (deps: MOD99) **About and models is one long page of eleven text sections, mixing settings, accounts, downloads and licences** — Changing a key or freeing disk space means reading past licences and diagnostics; each job deserves its own simple, visual page. → §MOD100

## Block C — Existing code wired into the window

## Block D — Model downloads

- 📋 **MOD97** (deps: —) **About lists only the models already downloaded, so a missing or removed model vanishes instead of showing as missing** — Download everything gives no clue what it would fetch, and after a Remove the user cannot see what is gone or get it back alone. → §MOD97
- 📋 **MOD124** (deps: —) **A model over 2 GB cannot be published or downloaded: the manifest and downloader ignore ONNX external data** — TripoSG's transformer alone exceeds protobuf's 2 GB limit, so MOD80 cannot start until large models can be stored in parts. → §MOD124
- 📋 **MOD122** (deps: —) **Only freely downloadable models can be offered; one behind a licence click, login or registration cannot be fetched** — The Stability models SF3D and SPAR3D, accepted on 12/11/2026, are gated on Hugging Face; a spike settles how a wizard can guide that. → §MOD122
- 📋 **MOD123** (deps: MOD122) **There is no download wizard that shows a model's licence, asks for consent and walks through login or registration** — A model with conditions needs informed consent and a guided login, which a Download button that just fetches files cannot give. → §MOD123
- 📋 **MOD125** (deps: MOD122) **The licence gate accepts only permissive licences, so a model under the Stability licence cannot be published** — The SF3D and SPAR3D decision of 12/11/2026 needs a licence class with conditions, recorded per model, instead of a yes-or-no list. → §MOD125
- 📋 **MOD126** (deps: MOD125) **Nothing shows the attribution a licence requires, such as Powered by Stability AI, when its model is used** — The Stability licence requires that notice wherever its model runs, and the app has no place that says which model made a result. → §MOD126

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

- ⏳ **MOD92** (deps: MOD81 ✅) **The Store listing is in English only, with no page in Portuguese, German, French, Spanish or Chinese** — The five new languages wait to be added in Partner Center, their CSV imported and screenshots uploaded in the next submission. → §MOD92

## Block G — Public site for 3D modelling

- 📋 **MOD101** (deps: —) **Someone without an NVIDIA GPU can't tell from the README, site or Store page that Modelado runs on AMD, Intel or CPU** — ChatGPT ranks Modelado first only for non-NVIDIA PCs, yet marks its CPU support Check build, since no first screen states it. → §MOD101
- 📋 **MOD132** (deps: —) **The README sends GLB files to Windows 3D Viewer, which reports say left the Store on 01/07/2026** — Pointing users at an app that may be gone makes the first open of a result fail outside Modelado. → §MOD132
- 📋 **MOD133** (deps: —) **Nothing on the site or Store says Modelado is unlimited, with no credits, no account and no moderation** — MakerLab now charges credits at generation and needs an account, and its users complain; Modelado's free local runs are the contrast. → §MOD133

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

- 📋 **MOD93** (deps: —) **With the Ideogram panel open, its opening button stays on the drop zone and pressing it again seems to do nothing** — A button that appears dead next to the panel it opened makes the user think the app hung and click again. → §MOD93
- 📋 **MOD95** (deps: —) **Paid Ideogram images are lost: unchosen candidates are discarded, the chosen one lands in a folder the app hides** — Every candidate is paid for, so a photo used once and never found again is money spent twice when it is redrawn. → §MOD95
- 📋 **MOD96** (deps: —) **About says a cloud key is stored yet shows an empty key box, without saying which key or where it came from** — An empty box beside "A key is stored" reads as a missing key, and a key set in the environment is not read at all. → §MOD96

## Block I — Blender integration: AI tools for existing Blender models

## Block J — Local model quality: past TripoSR

- 📋 **MOD127** (deps: —) **There is no repeatable side-by-side of models on the same photos, so a new model or INT8 variant cannot be judged** — Every model decision ahead (INT8, Depth Anything 3, SF3D, SPAR3D, TripoSG) needs the same photos, numbers and pictures to compare. → §MOD127
- 📋 **MOD128** (deps: MOD127) **The relief uses Depth Anything V2 Small, the only V2 size with a commercial licence, far below Large quality** — Depth Anything 3 Mono Large is Apache-2.0 with a community ONNX export, so a large-class relief model is now allowed. → §MOD128
- 📋 **MOD129** (deps: MOD127) **MoGe-2 has not been compared as a relief depth model, though it has an official ONNX export and normals** — MoGe-2 is MIT with ONNX files from its authors, so it may beat Depth Anything for reliefs without any export work. → §MOD129
- 📋 **MOD130** (deps: MOD123, MOD125, MOD126, MOD127) **3D Object has no textured option beyond TripoSR's blurred 64×64 vertex colours** — SF3D bakes a UV texture, beats TripoSR on Chamfer distance and already runs in ONNX on an iPhone; its licence was accepted on 12/11/2026. → §MOD130
- 📋 **MOD131** (deps: MOD123, MOD125, MOD126, MOD127) **SPAR3D measures clearly better than TripoSR, but its point-diffusion stage has no known ONNX export** — SPAR3D is the only candidate with a measured large gain (Chamfer 0.120 against 0.145), so its export is worth a spike. → §MOD131

## Block K — Translated interface

- 🛠 **MOD85** (deps: —) **Interface text is hard-coded in XAML and code-behind, with no English resource file for translations to fill** — Still literal: the About, cloud, consent, first-use, export, generate, texture, sources, updates and Blender partials, then a code-side literal test. → §MOD85
- 📋 **MOD86** (deps: MOD85 🛠) **The app forces en-US on every thread and has no way to show its interface in another language** — Translations are useless until the app follows the Windows display language and lets the user choose one in settings. → §MOD86
- 📋 **MOD87** (deps: MOD85 🛠, MOD86) **The interface has no Brazilian Portuguese (pt-BR) translation** — Brazilian Portuguese is the author's own language and a large Store market. → §MOD87
- 📋 **MOD88** (deps: MOD85 🛠, MOD86, MOD87) **The interface has no German (de) translation** — German is a large Store market, and its long compound words are the first test of whether the layout holds longer text. → §MOD88
- 📋 **MOD89** (deps: MOD85 🛠, MOD86, MOD87) **The interface has no French (fr) translation** — French covers France, Canada and much of Africa in the Store. → §MOD89
- 📋 **MOD90** (deps: MOD85 🛠, MOD86, MOD87) **The interface has no Spanish (es) translation** — Spanish reaches Spain and Latin America with one neutral resource file. → §MOD90
- 📋 **MOD91** (deps: MOD85 🛠, MOD86, MOD87) **The interface has no Simplified Chinese (zh-Hans) translation** — Simplified Chinese is the largest language market left, and the only one here that tests a non-Latin script and font fallback. → §MOD91

## Block L — Printable objects: relief keepsakes, 3MF and print-ready meshes

- 📋 **MOD102** (deps: —) **Relief depth and base are set as a share of the width, while a printer thinks in millimetres** — Every printed relief starts from a size in mm, so a percentage of the width makes the user work out each thickness by hand. → §MOD102
- 📋 **MOD103** (deps: —) **There is no 3MF export, and STL carries no units, so a slicer has to guess the model's size** — 3MF is the format slicers prefer: it keeps millimetres and colour and opens straight onto Bambu, Orca and Prusa plates. → §MOD103
- 📋 **MOD104** (deps: MOD102) **A relief cannot become a keychain: there is no hole, loop or tag outline to hang it by** — A keychain is among the most asked photo prints, and today the tool people use for it, MakerLab, works only in the cloud. → §MOD104
- 📋 **MOD105** (deps: MOD102) **A relief is always the photo's rectangle, never a round or oval medallion or a plaque with a border** — Bambu's Relief Maker users ask to fit a relief in a round frame and cannot; a framed medallion is what a printed portrait gift looks like. → §MOD105
- 📋 **MOD106** (deps: MOD105) **There is no way to put text on a relief: around a medallion, under a plaque or on a keychain** — A name or a date is what turns a printed relief into a gift, and Relief Maker users ask for text around the frame. → §MOD106
- 📋 **MOD107** (deps: MOD102) **A relief has no stand, so a print meant to stand on a shelf needs a support modelled elsewhere** — A printed portrait or plaque is often displayed upright, and the only fix today is modelling a stand in another tool. → §MOD107
- 📋 **MOD108** (deps: MOD102) **There is no lithophane mode, where thickness follows brightness and the print shows the photo when lit from behind** — Lithophanes are among the most searched photo prints; tools like Cura, ItsLitho and MakerLab make them, and Modelado's height mesher nearly does. → §MOD108
- 📋 **MOD109** (deps: MOD103) **A relief is not exported in the orientation it prints best, face up and flat on the plate** — A relief printed standing or face down needs supports and loses detail, and users of Bambu's Relief Maker report exactly that. → §MOD109
- 📋 **MOD110** (deps: MOD103) **Nothing opens the exported model in the slicer installed on the PC; the user finds the file by hand** — The step after export is always the slicer, and Bambu Studio now offers MakerLab's tools inside it, one click away. → §MOD110
- 📋 **MOD111** (deps: —) **A 3D Object is not guaranteed closed and manifold, so a slicer may show holes or empty layers** — Users of MakerLab's image to 3D report unprintable files with empty layers, fixed by hand with a voxel remesh in Blender. → §MOD111
- 📋 **MOD112** (deps: MOD111) **Nothing checks a 3D Object for walls too thin to print, or lays it on its flattest face** — A thin ear or tail breaks off or vanishes in the slicer, and choosing a face to rest on is done by hand in every slicer. → §MOD112
- 📋 **MOD113** (deps: —) **A relief base below 0.5% reads as no base in the window but still solidifies the mesh** — The window and the mesh disagree, so a user who set no base exports a solid and one who wants a thin base cannot trust the label. → §MOD113
- 📋 **MOD114** (deps: —) **Watertightness is tested only for rectangular masks, not for a cut-out object's irregular outline** — Object only is the relief most keychains and medallions will use, and an irregular mask is where pinched vertices break a closed mesh. → §MOD114

## Block M — The CPU and integrated-GPU path

- 📋 **MOD117** (deps: —) **The README promises a 1.1 GB download, which is wrong for a CPU-only PC, where it is about 1.85 GB** — The CPU always loads the fp32 encoder, so the people told the app runs without a GPU are the ones the stated size misleads. → §MOD117
- 📋 **MOD118** (deps: —) **No run has ever measured peak RAM or video memory, so the about 4 GB requirement is a guess** — A requirement nobody measured cannot tell a user with an 8 GB laptop whether the app will run, nor give a memory check a figure. → §MOD118
- 📋 **MOD120** (deps: —) **Choosing DirectML is documented as failing on a GPU error, yet ModelRunner silently falls back to the CPU** — The code and its own comment disagree, so a user who forces the GPU to compare times may be measuring the CPU. → §MOD120
- 📋 **MOD121** (deps: —) **The speed table has no Intel or integrated-GPU timing, so the claim that Intel graphics work is untested** — Since October Modly runs on recent Radeons, so Intel and integrated GPUs are where Modelado alone runs, and that needs a measured row. → §MOD121
- 📋 **MOD115** (deps: MOD127) **Nobody has measured an INT8 TripoSR encoder against fp32 on Modelado's own photos** — An INT8 encoder is about 430 MB against 1.7 GB, but its quality loss is only described as slight, never measured. → §MOD115
- 📋 **MOD116** (deps: MOD115) **The CPU path cannot use a smaller, faster encoder, even where the quality would allow it** — A CPU-only PC downloads and loads the largest variant today, so the people Modelado is best for wait the longest. → §MOD116
- 📋 **MOD119** (deps: MOD118) **Nothing checks free memory before a run, so a PC short of RAM fails mid-run instead of being told first** — The light hardware Modelado is chosen for is where memory runs out, and a failure after a long CPU run is the worst way to learn it. → §MOD119

## Done when — Block H

- **Nothing leaves the PC without the user's explicit consent to that action** Cloud
  services are off until a key is entered, each upload names the service and what is
  sent before it happens, and the local operations never touch the network beyond model
  downloads. Checked by a case per service that declines and sees no request made.
- **A new service is one class behind the provider interface, not a new screen** Meshy
  and Ideogram are the first two; others like them will follow. Each implements the same
  capability interface and appears in the same settings, usage and cost views without UI
  changes. Checked by a fake provider in the tests that lights up every view.

## Done when — Block B

- **Every UI task ships with a winwright case that passed on the VMware guest** A screen
  is proven by a case in cases\*.cases.json driving the real window, run through
  winwright's run-tests-vm.cmd, never on the operator's desk. Friction with winwright is
  filed in its own roadmap (D:\Git\alegauss\winwright, prefix WW) rather than worked
  around here.

## Done when — MOD81

- **The listing shows the four captioned screenshots** Checked on
  apps.microsoft.com/detail/9PB86WW5NRR2 after the submission that carries
  artifacts\store-shots\screenshot-1..4.png passes certification.

## Done when — MOD82

- **The listing carries the 16:9 Super hero art** Checked in Partner Center's Store
  listing images after the next submission, which uploads
  artifacts\store-art\hero-16x9-3840x2160.png.

## Done when — MOD92

- **The Store page reads in all six listing languages** Checked on
  apps.microsoft.com/detail/9PB86WW5NRR2 with hl=pt-br, de-de, fr-fr, es-es and zh-cn
  after the submission carrying them passes certification.

## Done when — MOD84

- **The trailer plays at the top of the Store page** Checked on
  apps.microsoft.com/detail/9PB86WW5NRR2 after the submission carrying
  artifacts\store-trailer\<language>\trailer.mp4, its thumbnail and the hero art passes
  certification.

## Done when — MOD85

- **No interface string is a literal in the app's code-behind** An architecture test,
  beside StringResourceTests' XAML check, refuses a string literal assigned to Text,
  Content, Header, ToolTip or Title in src/Modelado.App's .cs files; every such string
  comes from Strings.

## Non-goals
