# Roadmap (active backlog)

## Priority

- MOD20
- Block B
- Block D

## Block A — English everywhere

- 📋 **MOD49** (deps: —) **Counts, sizes and timings in the window and the CLI use the system culture, so 980.000 triangles reads as 980** — An English interface with pt-BR separators misstates every number by a factor of a thousand or swaps decimal marks. → §MOD49

## Block B — Windows interface built like PhotoBoost

- 📋 **MOD53** (deps: MOD20) **A reconstruction and a model download have never been seen in the status bar's chips** — MOD9 could only watch an edit there: with no models published nothing can be reconstructed or downloaded on the guest. → §MOD53

## Block C — Existing code wired into the window

## Block D — Model downloads

- 📋 **MOD20** (deps: —) (requires: hf-token) **The embedded models-manifest.json lists no models, so the app cannot download any model and no operation can run** — Nothing was ever exported and published: there is no .cache\models and no published-models.json, so a fresh install has nothing to fetch. → §MOD20

## Block E — Publishing custom models to Hugging Face

- 📋 **MOD24** (deps: MOD20) **Only the models export_models.py builds can be published; a custom ONNX with its own meta.json has no way in** — A fine-tuned TripoSR, a different upscaler or a model someone converted themselves is what a custom repository is for, and today the pipeline has no door for it. → §MOD24
- 📋 **MOD25** (deps: MOD24) **Publishing never checks that a model's license allows redistribution before uploading it to a public repository** — The built-in models are all MIT or Apache-2.0 by choice, but a custom model could be CC BY-NC or unlicensed, and a public upload of it cannot be taken back. → §MOD25
- 📋 **MOD26** (deps: MOD20) **publish_models.py never reads back what it uploaded, so a broken or truncated file is found only by the app** — The app refuses any file whose SHA-256 differs from the manifest, so a bad upload breaks every install until someone republishes. → §MOD26
- 📋 **MOD27** (deps: MOD23 ✅, MOD24) **The app reads a custom model repository only through --manifest at launch; the window cannot add or switch to one** — A custom model published to someone's own Hugging Face repository is useless to them until the window can be pointed at it and say what it holds. → §MOD27

## Block F — Installer, releases and auto update

- 📋 **MOD28** (deps: MOD3 ✅, MOD7 ✅) **There is no installer: Modelado runs only from a build tree, with no Start menu entry, file association or uninstall** — PhotoBoost, claude-tray and freewilly each ship an Inno Setup installer, and auto update is a new installer upgrading in place. → §MOD28
- 📋 **MOD29** (deps: MOD14 ✅, MOD28) **Nothing publishes a release: no tag-driven build, no GitHub release with the installer and a SHA256SUMS file** — The updater can only trust what a release states, and freewilly's release.yml is what makes the tag, the file name and the digest agree. → §MOD29
- 📋 **MOD30** (deps: MOD9 ✅, MOD29) **An installed copy has no way to learn that a newer release exists** — A fix reaches only whoever goes looking for it, which is the gap claude-tray's Updater and freewilly's ReleaseCheck were each written to close. → §MOD30
- 📋 **MOD31** (deps: MOD30) **A newer release found cannot be installed from the app; it has to be downloaded and run by hand** — claude-tray downloads its installer, runs it silently and relaunches, and freewilly verifies the digest first; Modelado should do both. → §MOD31

## Block G — Public site for 3D modelling

- 📋 **MOD32** (deps: MOD3 ✅) **Modelado has no public site: nowhere to see what it makes, try a result or download it** — PhotoBoost's site is how people find and judge it, and Modelado needs the same front door, built around 3D models instead of photos. → §MOD32
- 📋 **MOD33** (deps: MOD20, MOD32) **The site has no hero showing a photo becoming a model the reader can turn, the way PhotoBoost opens on a before/after** — A 3D result is judged by turning it around, and a still image of a mesh hides the back, which is exactly what TripoSR invents. → §MOD33
- 📋 **MOD34** (deps: MOD29, MOD33) **The site has no sections on each operation, privacy, formats or the download** — After the hero, PhotoBoost's page walks a studio of each operation, the moves from input to result, privacy and speed, and ends on the download. → §MOD34
- 📋 **MOD35** (deps: MOD32) **Nothing builds and deploys the site when it changes** — A site deployed by hand drifts from the repository, and freewilly's site.yml builds and publishes on every push that touches site\. → §MOD35

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

- 📋 **MOD36** (deps: MOD11 ✅) **The app has no way to use a cloud 3D or image service: no place for an API key, no provider abstraction** — Meshy AI and Ideogram give results the local models cannot, and more services like them will follow, so the seam comes before the first one. → §MOD36
- 📋 **MOD37** (deps: MOD36, MOD42) **Meshy AI cannot be used: no image-to-3D or text-to-3D through it, beside the local TripoSR** — Meshy returns textured, quad-friendly models with PBR maps that TripoSR cannot, and a person with a Meshy key should get them in the same window. → §MOD37
- 📋 **MOD38** (deps: MOD37) **An open model cannot be remeshed or retextured through Meshy, only edited with the local geometry tools** — A rough TripoSR mesh or an imported model often needs clean topology or a new texture from a prompt, which Meshy offers and the local tools do not. → §MOD38
- 📋 **MOD39** (deps: MOD10 ✅, MOD36, MOD42) **Without a photo there is nothing to reconstruct: the app cannot make the input image from a prompt** — Ideogram can draw the single, centred object on a plain background that TripoSR works best from, turning a text idea into a 3D model in two steps. → §MOD39
- 📋 **MOD40** (deps: MOD39) **A model's texture can only be upscaled; there is no way to make a new texture or material image from a prompt** — A relief or an imported model often needs a different surface (wood, stone, painted metal), and Ideogram can draw a seamless texture for it. → §MOD40
- 📋 **MOD41** (deps: MOD9 ✅, MOD36) **Nothing shows each service's credits, usage or status, so a limit is found only when a job is refused** — Cloud jobs spend money or credits, and a person needs to see how close each service is to its limit before starting one, not after. → §MOD41
- 📋 **MOD42** (deps: MOD41) **A cloud action gives no estimate of what it will cost before it runs** — Credits differ by action and options (texture on or off, polycount, four candidates or one), and a person should see the price before pressing, not on the bill. → §MOD42

## Block I — Blender integration: AI tools for existing Blender models

- 📋 **MOD43** (deps: MOD2 ✅, MOD16 ✅) **A model already in Blender cannot use Modelado's tools without exporting it, opening the app and importing back by hand** — Blender is where most existing models live, and Modelado's cleanup and AI tools are most useful applied to them in place. → §MOD43
- 📋 **MOD44** (deps: MOD20, MOD43) **A photo cannot be turned into a 3D object or relief from inside Blender** — Making a prop from a reference photo is a common Blender task, and today it means leaving Blender, generating in Modelado and importing by hand. → §MOD44
- 📋 **MOD45** (deps: MOD38, MOD40, MOD43) **From Blender, a texture cannot be sharpened with AI or remade from a prompt, nor sent to Meshy to remesh or retexture** — The AI tools are what Blender lacks natively, so they are the reason to reach for Modelado from inside it. → §MOD45
- 📋 **MOD46** (deps: MOD12 ✅, MOD43) **There is no round trip: a Blender object cannot be opened in the Modelado window and sent back to the same object** — Some work needs the window (orbiting, comparing versions, several edits in a row), and the result must land back on the object it came from. → §MOD46

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
