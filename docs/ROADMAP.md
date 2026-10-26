# Roadmap (active backlog)

## Priority

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

- 📋 **MOD59** (deps: —) **An imported GLB or OBJ whose colour is a material's own (baseColorFactor, Kd) or one of several textures opens grey** — Seen by the user on an import; MeshImport keeps only vertex colours and the first base-colour texture, so plain coloured or multi-material models lose them. → §MOD59
- 📋 **MOD62** (deps: —) **Simplify takes minutes on a relief with a base, where a TripoSR mesh of the same size takes a second** — Simplifying the Studio's 196,604-triangle bust relief to a third took 260 s on the guest's CPU; the chair's 150,000 took 0.9 s. → §MOD62
- 📋 **MOD63** (deps: —) **A GLB with a photo texture weighs megabytes: the texture is always embedded as a lossless PNG** — The Studio's canyon relief is 4.1 MB as GLB, nearly all of it the 768 px photo, which as JPEG would be about 200 kB. → §MOD63

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

## Block G — Public site for 3D modelling

- 📋 **MOD64** (deps: —) **The site's speed table has no GPU timing, only the CPU's** — Left from MOD34: the VMware guest that measured the CPU has no GPU to run DirectML on, and the page names none. → §MOD64

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

- 📋 **MOD40** (deps: MOD39 ✅) **A model's texture can only be upscaled; there is no way to make a new texture or material image from a prompt** — A relief or an imported model often needs a different surface (wood, stone, painted metal), and Ideogram can draw a seamless texture for it. → §MOD40

## Block I — Blender integration: AI tools for existing Blender models

- 📋 **MOD44** (deps: MOD20 ✅, MOD43 ✅) **A photo cannot be turned into a 3D object or relief from inside Blender** — Making a prop from a reference photo is a common Blender task, and today it means leaving Blender, generating in Modelado and importing by hand. → §MOD44
- 📋 **MOD45** (deps: MOD38 ✅, MOD40, MOD43 ✅) **From Blender, a texture cannot be sharpened with AI or remade from a prompt, nor sent to Meshy to remesh or retexture** — The AI tools are what Blender lacks natively, so they are the reason to reach for Modelado from inside it. → §MOD45

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
