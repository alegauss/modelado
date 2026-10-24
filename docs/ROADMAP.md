# Roadmap (active backlog)

## Priority

- MOD20
- Block B
- Block D
- Block C

## Block A — English everywhere

- 📋 **MOD49** (deps: —) **Counts, sizes and timings in the window and the CLI use the system culture, so 980.000 triangles reads as 980** — An English interface with pt-BR separators misstates every number by a factor of a thousand or swaps decimal marks. → §MOD49

## Block B — Windows interface built like PhotoBoost

- 🛠 **MOD8** (deps: MOD5 ✅) **No control has an AutomationId and there are no UI cases, so nothing proves a screen works** — PhotoBoost drives its window with winwright cases (cases\*.cases.json), which is the evidence a UI task ships on. → §MOD8
- 📋 **MOD9** (deps: MOD4 ✅) **The status bar is a bare panel with a provider chip and a text line: no job, last-run or download chips, no About** — PhotoBoost's status bar says where inference runs, how long the last run took and what is in progress, wherever the panel has scrolled to. → §MOD9
- 📋 **MOD10** (deps: MOD4 ✅) **With nothing open there is only a hint over the viewport: no drop zone, no Open button, nowhere to show an open error** — PhotoBoost's open screen is a card with the formats, an Open button and an error line, and the operations panel only appears once something is open. → §MOD10
- 📋 **MOD11** (deps: MOD9) **About is a separate modal window built in code, not a panel that takes the stage's place as in PhotoBoost** — PhotoBoost shows About and the download offer in the window itself, so the status bar and a running download stay visible behind them. → §MOD11
- 📋 **MOD12** (deps: MOD6 ✅) **Undo and Redo move between versions, but nothing shows the list of versions the model went through** — PhotoBoost's History card names every version, so a person can see how far Undo will go before pressing it. → §MOD12
- 📋 **MOD13** (deps: MOD6 ✅) **Export is only a Save dialog that guesses the format from the extension, with no card for format and options** — PhotoBoost's Export card shows the format and the options that apply to it before the dialog, so what will be written is visible up front. → §MOD13
- 📋 **MOD14** (deps: MOD8) **The suite runs on the developer's own desktop: no VMware guest run, so UI cases would take over the operator's desk** — PhotoBoost runs every suite, the winwright cases included, in a VMware guest through winwright's run-tests-vm.cmd, and a UI task ships on that run. → §MOD14

## Block C — Existing code wired into the window

- 📋 **MOD15** (deps: MOD13) **The window always exports STL at 100 mm on its longest side; the size the CLI's --size takes cannot be chosen** — A print has a real size, and a figurine meant to be 40 mm or 200 mm tall needs rescaling in the slicer today. → §MOD15
- 📋 **MOD16** (deps: MOD1 ✅) **Edit amounts are fixed in the window (smooth 3 passes, pieces under 5%, flat base at 3%) though the CLI takes each one** — A small fragment worth keeping, or a model whose base needs more than 3% cut, cannot be handled without dropping to the command line. → §MOD16
- 📋 **MOD17** (deps: MOD1 ✅) **The 3D Object card always samples at coarse step 2 with 2 smoothing passes; the CLI's --step and --smooth are absent** — Step 1 recovers thin parts the coarse pass misses, and a raw unsmoothed mesh is what some people want to sculpt from, so both belong on the card. → §MOD17
- 📋 **MOD18** (deps: MOD11) **Where inference runs (auto, DirectML or CPU) can only be chosen with --provider at launch, not from the window** — On a GPU that fails or is slower than the CPU for a model, the only remedy today is a shortcut with a flag nobody knows exists. → §MOD18
- 📋 **MOD19** (deps: MOD5 ✅) **The CLI's --preview writes a rendered view of the model, but the window cannot save the view it shows as an image** — A picture of the model is what gets shared or put in a listing, and today that means a screen capture of the viewport, cropped by hand. → §MOD19
- 📋 **MOD48** (deps: —) **modelado-cli crashes with a stack trace on an option missing its value, and silently accepts unknown options** — Scripts and the Blender add-on drive the CLI and need a readable error and a non-zero exit, not a crash or a misspelt edit reported as success. → §MOD48

## Block D — Model downloads

- 📋 **MOD20** (deps: —) (requires: hf-token) **The embedded models-manifest.json lists no models, so the app cannot download any model and no operation can run** — Nothing was ever exported and published: there is no .cache\models and no published-models.json, so a fresh install has nothing to fetch. → §MOD20
- 📋 **MOD21** (deps: MOD11) **A first-use download is offered in a MessageBox listing text, not a panel with each file, size, source and license** — PhotoBoost's consent panel gives everything a person needs to say yes to a download, inside the window, before a byte is fetched. → §MOD21
- 📋 **MOD22** (deps: MOD6 ✅, MOD21) **An operation card does not say before it is pressed that it needs a one-time download, or how large it is** — TripoSR's upstream checkpoint alone is 1.7 GB, and PhotoBoost's cards badge the download and its progress so pressing Generate is never a surprise. → §MOD22
- 📋 **MOD23** (deps: MOD11) **Downloaded models can only be removed all at once; About lists no per-model size and no Remove for each** — Someone who only makes reliefs wants TripoSR's disk back without losing Depth Anything, and PhotoBoost lists each download with its own Remove. → §MOD23

## Block E — Publishing custom models to Hugging Face

- 📋 **MOD24** (deps: MOD20) **Only the models export_models.py builds can be published; a custom ONNX with its own meta.json has no way in** — A fine-tuned TripoSR, a different upscaler or a model someone converted themselves is what a custom repository is for, and today the pipeline has no door for it. → §MOD24
- 📋 **MOD25** (deps: MOD24) **Publishing never checks that a model's license allows redistribution before uploading it to a public repository** — The built-in models are all MIT or Apache-2.0 by choice, but a custom model could be CC BY-NC or unlicensed, and a public upload of it cannot be taken back. → §MOD25
- 📋 **MOD26** (deps: MOD20) **publish_models.py never reads back what it uploaded, so a broken or truncated file is found only by the app** — The app refuses any file whose SHA-256 differs from the manifest, so a bad upload breaks every install until someone republishes. → §MOD26
- 📋 **MOD27** (deps: MOD23, MOD24) **The app reads a custom model repository only through --manifest at launch; the window cannot add or switch to one** — A custom model published to someone's own Hugging Face repository is useless to them until the window can be pointed at it and say what it holds. → §MOD27

## Block F — Installer, releases and auto update

- 📋 **MOD28** (deps: MOD3 ✅, MOD7 ✅) **There is no installer: Modelado runs only from a build tree, with no Start menu entry, file association or uninstall** — PhotoBoost, claude-tray and freewilly each ship an Inno Setup installer, and auto update is a new installer upgrading in place. → §MOD28
- 📋 **MOD29** (deps: MOD14, MOD28) **Nothing publishes a release: no tag-driven build, no GitHub release with the installer and a SHA256SUMS file** — The updater can only trust what a release states, and freewilly's release.yml is what makes the tag, the file name and the digest agree. → §MOD29
- 📋 **MOD30** (deps: MOD9, MOD29) **An installed copy has no way to learn that a newer release exists** — A fix reaches only whoever goes looking for it, which is the gap claude-tray's Updater and freewilly's ReleaseCheck were each written to close. → §MOD30
- 📋 **MOD31** (deps: MOD30) **A newer release found cannot be installed from the app; it has to be downloaded and run by hand** — claude-tray downloads its installer, runs it silently and relaunches, and freewilly verifies the digest first; Modelado should do both. → §MOD31

## Block G — Public site for 3D modelling

- 📋 **MOD32** (deps: MOD3 ✅) **Modelado has no public site: nowhere to see what it makes, try a result or download it** — PhotoBoost's site is how people find and judge it, and Modelado needs the same front door, built around 3D models instead of photos. → §MOD32
- 📋 **MOD33** (deps: MOD20, MOD32) **The site has no hero showing a photo becoming a model the reader can turn, the way PhotoBoost opens on a before/after** — A 3D result is judged by turning it around, and a still image of a mesh hides the back, which is exactly what TripoSR invents. → §MOD33
- 📋 **MOD34** (deps: MOD29, MOD33) **The site has no sections on each operation, privacy, formats or the download** — After the hero, PhotoBoost's page walks a studio of each operation, the moves from input to result, privacy and speed, and ends on the download. → §MOD34
- 📋 **MOD35** (deps: MOD32) **Nothing builds and deploys the site when it changes** — A site deployed by hand drifts from the repository, and freewilly's site.yml builds and publishes on every push that touches site\. → §MOD35

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

- 📋 **MOD36** (deps: MOD11) **The app has no way to use a cloud 3D or image service: no place for an API key, no provider abstraction** — Meshy AI and Ideogram give results the local models cannot, and more services like them will follow, so the seam comes before the first one. → §MOD36
- 📋 **MOD37** (deps: MOD36, MOD42) **Meshy AI cannot be used: no image-to-3D or text-to-3D through it, beside the local TripoSR** — Meshy returns textured, quad-friendly models with PBR maps that TripoSR cannot, and a person with a Meshy key should get them in the same window. → §MOD37
- 📋 **MOD38** (deps: MOD37) **An open model cannot be remeshed or retextured through Meshy, only edited with the local geometry tools** — A rough TripoSR mesh or an imported model often needs clean topology or a new texture from a prompt, which Meshy offers and the local tools do not. → §MOD38
- 📋 **MOD39** (deps: MOD10, MOD36, MOD42) **Without a photo there is nothing to reconstruct: the app cannot make the input image from a prompt** — Ideogram can draw the single, centred object on a plain background that TripoSR works best from, turning a text idea into a 3D model in two steps. → §MOD39
- 📋 **MOD40** (deps: MOD39) **A model's texture can only be upscaled; there is no way to make a new texture or material image from a prompt** — A relief or an imported model often needs a different surface (wood, stone, painted metal), and Ideogram can draw a seamless texture for it. → §MOD40
- 📋 **MOD41** (deps: MOD9, MOD36) **Nothing shows each service's credits, usage or status, so a limit is found only when a job is refused** — Cloud jobs spend money or credits, and a person needs to see how close each service is to its limit before starting one, not after. → §MOD41
- 📋 **MOD42** (deps: MOD41) **A cloud action gives no estimate of what it will cost before it runs** — Credits differ by action and options (texture on or off, polycount, four candidates or one), and a person should see the price before pressing, not on the bill. → §MOD42

## Block I — Blender integration: AI tools for existing Blender models

- 📋 **MOD43** (deps: MOD2 ✅, MOD16) **A model already in Blender cannot use Modelado's tools without exporting it, opening the app and importing back by hand** — Blender is where most existing models live, and Modelado's cleanup and AI tools are most useful applied to them in place. → §MOD43
- 📋 **MOD44** (deps: MOD20, MOD43) **A photo cannot be turned into a 3D object or relief from inside Blender** — Making a prop from a reference photo is a common Blender task, and today it means leaving Blender, generating in Modelado and importing by hand. → §MOD44
- 📋 **MOD45** (deps: MOD38, MOD40, MOD43) **From Blender, a texture cannot be sharpened with AI or remade from a prompt, nor sent to Meshy to remesh or retexture** — The AI tools are what Blender lacks natively, so they are the reason to reach for Modelado from inside it. → §MOD45
- 📋 **MOD46** (deps: MOD12, MOD43) **There is no round trip: a Blender object cannot be opened in the Modelado window and sent back to the same object** — Some work needs the window (orbiting, comparing versions, several edits in a row), and the result must land back on the object it came from. → §MOD46

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
