# Roadmap (active backlog)

## Priority

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

- 📋 **MOD72** (deps: visibility VS58) **Every first download meets a SmartScreen warning that the README and the release notes have to explain away** — The Store signs the MSIX package itself, and claude-tray tests that path first, so this follows what that test learns. → §MOD72

## Block G — Public site for 3D modelling

- 📋 **MOD69** (deps: —) **The README teaches compiling before it mentions the release, never links the site, and calls the sibling PhotoBoost** — A visitor from GitHub concludes Modelado must be built from source, and the sibling link leads to a name that product dropped. → §MOD69

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

## Block I — Blender integration: AI tools for existing Blender models

- 📋 **MOD70** (deps: —) **Someone browsing Blender's extensions platform never finds the Modelado add-on** — Blender users install add-ons from inside Blender, and the add-on already carries a manifest in the extensions format. → §MOD70

## Block J — Local model quality: past TripoSR

- 💭 **MOD71** (deps: —) **A local 3D Object comes out well below cloud services and newer open models, with the back of the object invented** — TripoSR is from 2024, newer open models are clearly better, and only a measured comparison of licence, ONNX and memory says whether one fits. → §MOD71

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

## Non-goals

- **A Portuguese or multilingual interface** All code, UI text, CLI output, comments,
  tests and docs are written in English, as PhotoBoost's are; a translation layer would
  double every string the cases assert on.
