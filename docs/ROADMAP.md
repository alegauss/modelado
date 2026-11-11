# Roadmap (active backlog)

## Priority

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

- ⏳ **MOD81** (deps: —) **At the Store's thumbnail size the screenshots look alike and nothing on them says what each one shows** — The four composed screenshots wait to be uploaded in a new submission, by the Partner Center page or the API. → §MOD81
- 📋 **MOD82** (deps: —) **The Store listing has no 16:9 Super hero art, without which it shows no trailer** — The Store plays trailers only on a listing that has the hero art, so MOD84 waits on this. → §MOD82
- 📋 **MOD84** (deps: MOD81 ⏳, MOD82) **The listing shows a 3D modeller only in still pictures, with no model seen turning** — A trailer at the top of the listing proves the models are 3D in a way a screenshot cannot. → §MOD84

## Block G — Public site for 3D modelling

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

## Block I — Blender integration: AI tools for existing Blender models

## Block J — Local model quality: past TripoSR

## Block K — Translated interface

- 📋 **MOD85** (deps: —) **Interface text is hard-coded in XAML and code-behind, with no English resource file for translations to fill** — Each translation needs one neutral English source of truth; without it every language edits the same strings in many places. → §MOD85
- 📋 **MOD86** (deps: MOD85) **The app forces en-US on every thread and has no way to show its interface in another language** — Translations are useless until the app follows the Windows display language and lets the user choose one in settings. → §MOD86

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

## Non-goals
