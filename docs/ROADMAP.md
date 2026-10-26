# Roadmap (active backlog)

## Priority

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

- 📋 **MOD66** (deps: —) **A relief's GLB carries its geometry at full width: float positions, normals and UVs and 32-bit indices** — The canyon relief is still 3.3 MB as GLB after its texture became a 195 kB JPEG, 3.1 MB of it geometry the site and every share download. → §MOD66

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

## Block G — Public site for 3D modelling

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

## Block I — Blender integration: AI tools for existing Blender models

- ⏳ **MOD45** (deps: MOD38 ✅, MOD40 ✅, MOD43 ✅, MOD65 ✅) **From Blender, a texture cannot be sharpened with AI or remade from a prompt, nor sent to Meshy to remesh or retexture** — From Blender, a texture cannot be drawn by Ideogram nor a model sent to Meshy: the CLI has no cloud door yet. → §MOD45
- 📋 **MOD67** (deps: winwright WW582) **No UI case shows a job sent from modelado-cli cloud in the window's About** — MOD65's evidence stops at the ledger file the CLI writes; the window never opens on it, since a fixture cannot run the CLI first. → §MOD67

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

## Done when — MOD45

- **Blender's cloud actions ask before sending, and a decline sends nothing** On the
  guest with Ideogram and Meshy stood in by the fixture server: a textured model in
  Blender gets an Ideogram texture and a Meshy retexture, each priced before it is sent
  and undoable, and a declined action makes no request to the stand-in.

## Non-goals

- **A Portuguese or multilingual interface** All code, UI text, CLI output, comments,
  tests and docs are written in English, as PhotoBoost's are; a translation layer would
  double every string the cases assert on.
