# Roadmap (active backlog)

## Priority

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

- 📋 **MOD100** (deps: MOD99 ⏸) **About and models is one long page of eleven text sections, mixing settings, accounts, downloads and licences** — Changing a key or freeing disk space means reading past licences and diagnostics; each job deserves its own simple, visual page. → §MOD100
- 📋 **MOD136** (deps: —) **About's source and download rows, rebuilt equal by value, can drop out of the accessibility tree while still shown** — A Remove button a screen reader or a case found once can vanish from the tree when About is opened again. → §MOD136
- 📋 **MOD138** (deps: —) **Relief, Cloud 3D, Edit and Export show every option at once, so their main control sits among rarely changed ones** — MOD98 shows one card at a time, but each card still lists all its settings; folding the extras leaves the main control and action in view. → §MOD138
- 📋 **MOD140** (deps: —) **Ticking Object only or Enhance through UI Automation leaves the card's download size and rail dot stale** — Both boxes refresh the models they need on Click, which WPF never raises for a screen reader's or a case's toggle. → §MOD140

## Block C — Existing code wired into the window

## Block D — Model downloads

- 📋 **MOD124** (deps: —) **A model over 2 GB cannot be published or downloaded: the manifest and downloader ignore ONNX external data** — TripoSG's transformer alone exceeds protobuf's 2 GB limit, so MOD80 cannot start until large models can be stored in parts. → §MOD124
- 📋 **MOD122** (deps: —) **Only freely downloadable models can be offered; one behind a licence click, login or registration cannot be fetched** — The Stability models SF3D and SPAR3D, accepted on 12/11/2026, are gated on Hugging Face; a spike settles how a wizard can guide that. → §MOD122
- 📋 **MOD123** (deps: MOD122) **There is no download wizard that shows a model's licence, asks for consent and walks through login or registration** — A model with conditions needs informed consent and a guided login, which a Download button that just fetches files cannot give. → §MOD123
- 📋 **MOD125** (deps: MOD122) **The licence gate accepts only permissive licences, so a model under the Stability licence cannot be published** — The SF3D and SPAR3D decision of 12/11/2026 needs a licence class with conditions, recorded per model, instead of a yes-or-no list. → §MOD125
- 📋 **MOD126** (deps: MOD125) **Nothing shows the attribution a licence requires, such as Powered by Stability AI, when its model is used** — The Stability licence requires that notice wherever its model runs, and the app has no place that says which model made a result. → §MOD126

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

## Block G — Public site for 3D modelling

- 📋 **MOD132** (deps: —) **The README sends GLB files to Windows 3D Viewer, which reports say left the Store on 01/07/2026** — Pointing users at an app that may be gone makes the first open of a result fail outside Modelado. → §MOD132
- 📋 **MOD133** (deps: —) **Nothing on the site or Store says Modelado is unlimited, with no credits, no account and no moderation** — MakerLab now charges credits at generation and needs an account, and its users complain; Modelado's free local runs are the contrast. → §MOD133

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

## Block I — Blender integration: AI tools for existing Blender models

## Block J — Local model quality: past TripoSR

- 📋 **MOD127** (deps: —) **There is no repeatable side-by-side of models on the same photos, so a new model or INT8 variant cannot be judged** — Every model decision ahead (INT8, Depth Anything 3, SF3D, SPAR3D, TripoSG) needs the same photos, numbers and pictures to compare. → §MOD127
- 📋 **MOD128** (deps: MOD127) **The relief uses Depth Anything V2 Small, the only V2 size with a commercial licence, far below Large quality** — Depth Anything 3 Mono Large is Apache-2.0 with a community ONNX export, so a large-class relief model is now allowed. → §MOD128
- 📋 **MOD129** (deps: MOD127) **MoGe-2 has not been compared as a relief depth model, though it has an official ONNX export and normals** — MoGe-2 is MIT with ONNX files from its authors, so it may beat Depth Anything for reliefs without any export work. → §MOD129
- 📋 **MOD130** (deps: MOD123, MOD125, MOD126, MOD127) **3D Object has no textured option beyond TripoSR's blurred 64×64 vertex colours** — SF3D bakes a UV texture, beats TripoSR on Chamfer distance and already runs in ONNX on an iPhone; its licence was accepted on 12/11/2026. → §MOD130
- 📋 **MOD131** (deps: MOD123, MOD125, MOD126, MOD127) **SPAR3D measures clearly better than TripoSR, but its point-diffusion stage has no known ONNX export** — SPAR3D is the only candidate with a measured large gain (Chamfer 0.120 against 0.145), so its export is worth a spike. → §MOD131

## Block K — Translated interface

- 📋 **MOD134** (deps: MOD85 ✅) **The window shows sentences the libraries compose in English, which no translation of Strings.resx reaches** — Budget notes, cloud job titles, the provider chip and its fallback reason stay English beside translated text. → §MOD134

## Block L — Printable objects: relief keepsakes, 3MF and print-ready meshes

- ⏳ **MOD110** (deps: MOD103 ✅, winwright WW628) **Nothing opens the exported model in the slicer installed on the PC; the user finds the file by hand** — Still to come: a case proving the pick is remembered (WW628), and a check against the four real slicers. → §MOD110
- 📋 **MOD112** (deps: MOD111 ✅) **Nothing checks a 3D Object for walls too thin to print, or lays it on its flattest face** — A thin ear or tail breaks off or vanishes in the slicer, and choosing a face to rest on is done by hand in every slicer. → §MOD112
- 📋 **MOD113** (deps: —) **A relief base below 0.5% reads as no base in the window but still solidifies the mesh** — The window and the mesh disagree, so a user who set no base exports a solid and one who wants a thin base cannot trust the label. → §MOD113
- 📋 **MOD114** (deps: —) **Watertightness is tested only for rectangular masks, not for a cut-out object's irregular outline** — Object only is the relief most keychains and medallions will use, and an irregular mask is where pinched vertices break a closed mesh. → §MOD114
- 📋 **MOD141** (deps: MOD104 ✅) **A keychain cannot follow the cut-out object's outline, and its hole cannot be dragged into place** — Pet keychains are shaped like the pet, and a slider along the top cannot put the hole where the object has room for it. → §MOD141
- 📋 **MOD142** (deps: MOD106 ✅) **Relief text is limited to a built-in font of capitals and digits; no bundled OFL font or installed font** — MOD106 raises text in a stroke font only, so a lower-case name or a script face still needs another program. → §MOD142
- 📋 **MOD143** (deps: MOD106 ✅) **Text on a relief can only be raised, never engraved into the surface** — MOD106 stamps letters above the relief, while a plaque band or a keychain back often wants the name cut in. → §MOD143
- 📋 **MOD145** (deps: MOD108 ✅) **A lithophane can only be a flat panel with no frame: no curved one that stands, and no cylinder for a lamp** — Curved and cylinder lithophanes are what the other tools offer beside flat, and a thin unframed panel warps. → §MOD145

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

## Done when — MOD87

- **A native speaker has reviewed the pt-BR interface text** The author reads
  Strings.pt-BR.resx in the running window and settles the terminology questions the
  design section lists; the translation was written without a native reading.

## Done when — MOD88

- **A native speaker has reviewed the German interface text** A German reader goes
  through Strings.de.resx in the running window and settles the terminology questions
  the design section lists; the translation was written without a native reading.

## Done when — MOD89

- **A native speaker has reviewed the French interface text** A French reader goes
  through Strings.fr.resx in the running window and settles the terminology questions
  the design section lists; the translation was written without a native reading.

## Done when — MOD90

- **A native speaker has reviewed the Spanish interface text** A Spanish reader goes
  through Strings.es.resx in the running window, checks it reads neutral to Spain and
  Latin America, and settles the terminology questions the design section lists.

## Done when — MOD91

- **A native speaker has reviewed the Simplified Chinese interface text** A Chinese
  reader goes through Strings.zh-Hans.resx in the running window and settles the
  terminology and punctuation questions the design section lists.

## Done when — MOD94

- **Recent files reach the toolbar, show photo thumbnails and feed the Jump List** A
  case opens the toolbar's Recent menu and finds the drop zone's list in it; photo
  entries carry a thumbnail; the Jump List lists the same paths where Windows allows it
  for the package.

## Done when — MOD110

- **A case proves the slicer picked is kept for the next session** Once WW628 lets a
  file step read text, the slicer case reads settings.json for the pick, and each of the
  four slicers on a test PC opens the 3MF.

## Non-goals
