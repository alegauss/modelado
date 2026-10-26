# Roadmap (active backlog)

## Priority

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

- ⏳ **MOD66** (deps: —) **A relief's GLB carries its geometry at full width: float positions, normals and UVs and 32-bit indices** — Quantized positions, normals and UVs (KHR_mesh_quantization, Windows 3D Viewer checked) to bring the canyon under 1.5 MB, and the site's Studio GLBs re-saved. → §MOD66

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

## Block G — Public site for 3D modelling

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

## Block I — Blender integration: AI tools for existing Blender models

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

## Done when — MOD66

- **The canyon relief saves under 1.5 MB as GLB** MeshExportTests' canyon test, on
  site/public/demo/studio/relief-landscape.glb, asserts the saved file under 1.5 MB and
  its texture and shape unchanged on reading back.
- **Quantized GLBs open in Windows 3D Viewer, Blender and the site** A quantized GLB is
  opened in each on the guest before it becomes the default; where one fails, the float
  writer stays the default behind an Export card choice.
- **The site's Studio GLBs are re-saved with the lighter writer** Each file under
  site/public/demo/studio is written again by modelado-cli and the site's build serves
  the smaller files.

## Non-goals

- **A Portuguese or multilingual interface** All code, UI text, CLI output, comments,
  tests and docs are written in English, as PhotoBoost's are; a translation layer would
  double every string the cases assert on.
