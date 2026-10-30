# Roadmap (active backlog)

## Priority

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

## Block G — Public site for 3D modelling

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

## Block I — Blender integration: AI tools for existing Blender models

- ⏳ **MOD46** (deps: MOD12 ✅, MOD43 ✅, winwright WW586) **There is no round trip: a Blender object cannot be opened in the Modelado window and sent back to the same object** — The standalone window's Export card has no Open in Blender, which would find Blender and start it importing the model. → §MOD46

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

## Done when — MOD46

- **Open in Blender starts Blender importing the shown model** On the guest with Blender
  installed, a UI case presses Open in Blender on the Export card and a Blender process
  starts with the model's GLB; with no Blender found, the button is absent.

## Non-goals

- **A Portuguese or multilingual interface** All code, UI text, CLI output, comments,
  tests and docs are written in English, as PhotoBoost's are; a translation layer would
  double every string the cases assert on.
