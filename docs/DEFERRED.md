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

## Block I — Blender integration: AI tools for existing Blender models
