# Roadmap (active backlog)

## Priority

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

- ⏳ **MOD81** (deps: —) **At the Store's thumbnail size the screenshots look alike and nothing on them says what each one shows** — The four composed screenshots wait to be uploaded in a new submission, by the Partner Center page or the API. → §MOD81
- ⏳ **MOD82** (deps: —) **The Store listing has no 16:9 Super hero art, without which it shows no trailer** — The hero art waits to be uploaded with the screenshots in the next submission. → §MOD82
- 📋 **MOD84** (deps: MOD81 ⏳, MOD82 ⏳) **The listing shows a 3D modeller only in still pictures, with no model seen turning** — A trailer at the top of the listing proves the models are 3D in a way a screenshot cannot. → §MOD84
- ⏳ **MOD92** (deps: MOD81 ⏳) **The Store listing is in English only, with no page in Portuguese, German, French, Spanish or Chinese** — The five new languages wait to be added in Partner Center, their CSV imported and screenshots uploaded in the next submission. → §MOD92

## Block G — Public site for 3D modelling

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

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

## Non-goals
