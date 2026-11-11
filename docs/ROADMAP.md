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

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

- ⏳ **MOD81** (deps: —) **At the Store's thumbnail size the screenshots look alike and nothing on them says what each one shows** — The four composed screenshots wait to be uploaded in a new submission, by the Partner Center page or the API. → §MOD81
- ⏳ **MOD82** (deps: —) **The Store listing has no 16:9 Super hero art, without which it shows no trailer** — The hero art waits to be uploaded with the screenshots in the next submission. → §MOD82
- ⏳ **MOD84** (deps: MOD81 ⏳, MOD82 ⏳) **The listing shows a 3D modeller only in still pictures, with no model seen turning** — The six trailers and thumbnails wait to be uploaded with the hero art in the next submission. → §MOD84
- ⏳ **MOD92** (deps: MOD81 ⏳) **The Store listing is in English only, with no page in Portuguese, German, French, Spanish or Chinese** — The five new languages wait to be added in Partner Center, their CSV imported and screenshots uploaded in the next submission. → §MOD92

## Block G — Public site for 3D modelling

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

- 📋 **MOD93** (deps: —) **With the Ideogram panel open, its opening button stays on the drop zone and pressing it again seems to do nothing** — A button that appears dead next to the panel it opened makes the user think the app hung and click again. → §MOD93
- 📋 **MOD95** (deps: —) **Paid Ideogram images are lost: unchosen candidates are discarded, the chosen one lands in a folder the app hides** — Every candidate is paid for, so a photo used once and never found again is money spent twice when it is redrawn. → §MOD95
- 📋 **MOD96** (deps: —) **About says a cloud key is stored yet shows an empty key box, without saying which key or where it came from** — An empty box beside "A key is stored" reads as a missing key, and a key set in the environment is not read at all. → §MOD96

## Block I — Blender integration: AI tools for existing Blender models

## Block J — Local model quality: past TripoSR

## Block K — Translated interface

- 📋 **MOD85** (deps: —) **Interface text is hard-coded in XAML and code-behind, with no English resource file for translations to fill** — Each translation needs one neutral English source of truth; without it every language edits the same strings in many places. → §MOD85
- 📋 **MOD86** (deps: MOD85) **The app forces en-US on every thread and has no way to show its interface in another language** — Translations are useless until the app follows the Windows display language and lets the user choose one in settings. → §MOD86
- 📋 **MOD87** (deps: MOD85, MOD86) **The interface has no Brazilian Portuguese (pt-BR) translation** — Brazilian Portuguese is the author's own language and a large Store market. → §MOD87
- 📋 **MOD88** (deps: MOD85, MOD86, MOD87) **The interface has no German (de) translation** — German is a large Store market, and its long compound words are the first test of whether the layout holds longer text. → §MOD88
- 📋 **MOD89** (deps: MOD85, MOD86, MOD87) **The interface has no French (fr) translation** — French covers France, Canada and much of Africa in the Store. → §MOD89
- 📋 **MOD90** (deps: MOD85, MOD86, MOD87) **The interface has no Spanish (es) translation** — Spanish reaches Spain and Latin America with one neutral resource file. → §MOD90
- 📋 **MOD91** (deps: MOD85, MOD86, MOD87) **The interface has no Simplified Chinese (zh-Hans) translation** — Simplified Chinese is the largest language market left, and the only one here that tests a non-Latin script and font fallback. → §MOD91

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

## Non-goals
