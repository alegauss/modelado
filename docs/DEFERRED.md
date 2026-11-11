# Set aside

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

- ⏸ **MOD74** (deps: MOD73 ✅) **A Store install would still offer to download and run the Inno installer over itself from About** — set aside (Guest Developer Mode.): MOD78's case on the registered layout must show About without the update offer, which waits on Developer Mode on the guest. → §MOD74
- ⏸ **MOD75** (deps: MOD73 ✅) **Open with in Explorer would not list Modelado for .glb, .obj, .ply or .stl once it comes from the Store** — set aside (Guest Developer Mode): On the registered layout, Open with on a .glb and an .stl must list Modelado and show the model; it waits on Developer Mode on the guest. → §MOD75
- ⏸ **MOD76** (deps: MOD73 ✅) **The Blender add-on and a terminal cannot start Modelado or modelado-cli from a Store install** — set aside (Guest Developer Mode): On the registered layout, modelado-cli --version from a fresh shell and the add-on's Edit in Modelado; waits on Developer Mode on the guest. → §MOD76
- ⏸ **MOD77** (deps: MOD73 ✅, MOD74 ✅) **Under a package About would open an empty models folder, since writes to %LOCALAPPDATA%\Modelado are redirected** — set aside (Guest Developer Mode): On the registered layout a model downloads, About opens its folder, and keys decrypt after a restart. → §MOD77

## Block G — Public site for 3D modelling

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

## Block I — Blender integration: AI tools for existing Blender models

- ⏸ **MOD70** (deps: —) **Someone browsing Blender's extensions platform never finds the Modelado add-on** — set aside (Waits on the user's Blender ID upload.): Upload the built zip to extensions.blender.org under the user's Blender ID, or record the platform's reason for refusing it. → §MOD70

## Block J — Local model quality: past TripoSR

- ⏸ **MOD80** (deps: —) **TripoSG has no ONNX export and has never been measured against TripoSR on the test photos** — set aside (Needs a go-ahead for 7.5 GB and a GPU session): It is the one newer open model that passes the licence rule, and only a measured run says whether it fits 8 GB and beats TripoSR. → §MOD80
