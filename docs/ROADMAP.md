# Roadmap (active backlog)

## Priority

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

- 📋 **MOD59** (deps: —) **An imported GLB or OBJ whose colour is a material's own (baseColorFactor, Kd) or one of several textures opens grey** — Seen by the user on an import; MeshImport keeps only vertex colours and the first base-colour texture, so plain coloured or multi-material models lose them. → §MOD59

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

## Block G — Public site for 3D modelling

- ⏳ **MOD34** (deps: MOD29 ✅, MOD33 ✅) **The site has no sections on each operation, privacy, formats or the download** — No Studio shows a real result per operation (object, relief, edit, AI texture), and the speed section has no GPU timing. → §MOD34

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

- ⏳ **MOD39** (deps: MOD10 ✅, MOD36 ✅, MOD42 ✅, MOD20 ✅) **Without a photo there is nothing to reconstruct: the app cannot make the input image from a prompt** — A chosen candidate has not been through TripoSR, which needs the models MOD20 publishes. → §MOD39
- 📋 **MOD40** (deps: MOD39 ⏳) **A model's texture can only be upscaled; there is no way to make a new texture or material image from a prompt** — A relief or an imported model often needs a different surface (wood, stone, painted metal), and Ideogram can draw a seamless texture for it. → §MOD40

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

## Done when — MOD39

- **A drawn candidate becomes a TripoSR model** On the guest with the published models:
  a candidate chosen on the drop zone is generated with 3D Object, and the PNG it was
  saved as still carries its prompt in an iTXt chunk.

## Done when — MOD34

- **Every Studio tab shows a real result, and speed names a GPU timing** The Studio has
  a tab each for 3D Object, Relief (a face, a building, a landscape, photos credited
  with their licences), Edit and refine, and AI texture, every result made by
  modelado-cli with the published models; the speed table adds one run on a named GPU
  with DirectML.

## Non-goals

- **A Portuguese or multilingual interface** All code, UI text, CLI output, comments,
  tests and docs are written in English, as PhotoBoost's are; a translation layer would
  double every string the cases assert on.
