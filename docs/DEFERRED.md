# Set aside

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

- ⏸ **MOD73** (deps: —) **No MSIX package exists for the identity reserved in Partner Center, so nothing can be uploaded to the Store** — set aside (Guest Developer Mode.): check-msix-vm.ps1 must start Modelado from the registered layout's Start entry on the guest, which refuses it until Developer Mode is on. → §MOD73
- ⏸ **MOD74** (deps: MOD73 ✅) **A Store install would still offer to download and run the Inno installer over itself from About** — set aside (Guest Developer Mode.): MOD78's case on the registered layout must show About without the update offer, which waits on Developer Mode on the guest. → §MOD74

## Block G — Public site for 3D modelling

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

## Block I — Blender integration: AI tools for existing Blender models

- ⏸ **MOD70** (deps: —) **Someone browsing Blender's extensions platform never finds the Modelado add-on** — set aside (Waits on the user's Blender ID upload.): Upload the built zip to extensions.blender.org under the user's Blender ID, or record the platform's reason for refusing it. → §MOD70

## Block J — Local model quality: past TripoSR

