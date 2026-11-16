# Roadmap (active backlog)

## Priority

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

- 📋 **MOD100** (deps: MOD99 ⏸) **About and models is one long page of eleven text sections, mixing settings, accounts, downloads and licences** — Changing a key or freeing disk space means reading past licences and diagnostics; each job deserves its own simple, visual page. → §MOD100

## Block C — Existing code wired into the window

## Block D — Model downloads

- 🛠 **MOD122** (deps: —) (requires: hf-token) **Only freely downloadable models can be offered; one behind a licence click, login or registration cannot be fetched** — The Stability models SF3D and SPAR3D, accepted on 12/11/2026, are gated on Hugging Face; a spike settles how a wizard can guide that. → §MOD122
- 📋 **MOD123** (deps: MOD122) **There is no download wizard that shows a model's licence, asks for consent and walks through login or registration** — A model with conditions needs informed consent and a guided login, which a Download button that just fetches files cannot give. → §MOD123
- 📋 **MOD125** (deps: MOD122) **The licence gate accepts only permissive licences, so a model under the Stability licence cannot be published** — The SF3D and SPAR3D decision of 12/11/2026 needs a licence class with conditions, recorded per model, instead of a yes-or-no list. → §MOD125
- 📋 **MOD126** (deps: MOD125) **Nothing shows the attribution a licence requires, such as Powered by Stability AI, when its model is used** — The Stability licence requires that notice wherever its model runs, and the app has no place that says which model made a result. → §MOD126

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

## Block G — Public site for 3D modelling

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

## Block I — Blender integration: AI tools for existing Blender models

## Block J — Local model quality: past TripoSR

- 📋 **MOD130** (deps: MOD123, MOD125, MOD126, MOD127 ✅) **3D Object has no textured option beyond TripoSR's blurred 64×64 vertex colours** — SF3D bakes a UV texture, beats TripoSR on Chamfer distance and already runs in ONNX on an iPhone; its licence was accepted on 12/11/2026. → §MOD130
- 📋 **MOD131** (deps: MOD123, MOD125, MOD126, MOD127 ✅) **SPAR3D measures clearly better than TripoSR, but its point-diffusion stage has no known ONNX export** — SPAR3D is the only candidate with a measured large gain (Chamfer 0.120 against 0.145), so its export is worth a spike. → §MOD131

## Block K — Translated interface

## Block L — Printable objects: relief keepsakes, 3MF and print-ready meshes

- ⏳ **MOD110** (deps: MOD103 ✅, winwright WW628) **Nothing opens the exported model in the slicer installed on the PC; the user finds the file by hand** — Still to come: a case proving the pick is remembered (WW628), and a check against the four real slicers. → §MOD110

## Block M — The CPU and integrated-GPU path

- ⏳ **MOD116** (deps: MOD115 ✅) (requires: hf-token) **The CPU path cannot use a smaller, faster encoder, even where the quality would allow it** — Still to come: publish the INT8 encoder to the models repository, then state its size and time in the README and the site. → §MOD116

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

## Done when — MOD116

- **A CPU-only install downloads and runs the published INT8 encoder** publish_models.py
  publishes triposr-encoder int8, the embedded manifest lists it, and Light on a
  CPU-only PC downloads 433 MB and runs it; the README and the site state its size and
  time.

## Non-goals
