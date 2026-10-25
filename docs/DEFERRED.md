# Set aside

## Block A — English everywhere

## Block B — Windows interface built like PhotoBoost

## Block C — Existing code wired into the window

## Block D — Model downloads

## Block E — Publishing custom models to Hugging Face

## Block F — Installer, releases and auto update

- ⏸ **MOD29** (deps: MOD14 ✅, MOD28 ✅) **Nothing publishes a release: no tag-driven build, no GitHub release with the installer and a SHA256SUMS file** — set aside (needs a pushed v-tag): No tag has been pushed yet, so no GitHub release has proved the sums match and a mismatched tag fails the workflow. → §MOD29

## Block G — Public site for 3D modelling

- ⏸ **MOD35** (deps: MOD32 ✅) **Nothing builds and deploys the site when it changes** — set aside (needs Pages enabled and a push): Pages is not switched to GitHub Actions and nothing is pushed, so no deploy has proved a content.ts change goes live. → §MOD35

## Block H — Cloud services: Meshy AI for 3D, Ideogram for images and textures

- ⏸ **MOD37** (deps: MOD36 ✅, MOD42 ✅) **Meshy AI cannot be used: no image-to-3D or text-to-3D through it, beside the local TripoSR** — set aside (a real run spends the owner's credits): No run has used a real key yet: one Meshy job spends about 30 of the owner's credits. → §MOD37

## Block I — Blender integration: AI tools for existing Blender models

- ⏸ **MOD43** (deps: MOD2 ✅, MOD16 ✅) **A model already in Blender cannot use Modelado's tools without exporting it, opening the app and importing back by hand** — set aside (needs Blender on the guest): The Blender extension itself, which needs Blender on the VMware guest to be tested headless. → §MOD43
- ⏸ **MOD46** (deps: MOD12 ✅, MOD43 ✅) **There is no round trip: a Blender object cannot be opened in the Modelado window and sent back to the same object** — set aside (needs Blender, MOD43): Orbiting, comparing and several edits in a row need the window, and the result must land on the object it came from. → §MOD46
