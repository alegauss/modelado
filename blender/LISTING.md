# The add-on's listing on extensions.blender.org

What to submit for the Modelado add-on (MOD70). The manifest
(`modelado\blender_manifest.toml`) carries the name, tagline, version, website, platforms
and permissions; this file holds what the platform asks for on its upload form.

## Build and check the package

```
blender --command extension validate blender\modelado
blender --command extension build --source-dir blender\modelado --output-dir artifacts\blender
```

The first runs in `blender\tests\run-blender-tests.cmd` on every gate. The second writes
`artifacts\blender\modelado-<version>.zip`, the file to upload.

## Description

Modelado's mesh tools and photo-to-3D inside Blender. The add-on is a front end to
[Modelado](https://alegauss.github.io/modelado/), a free Windows app that runs AI models on
your own PC, so **Modelado must be installed** and the add-on works on **Windows x64 only**.
The add-on finds an installed Modelado by itself; its preferences take another
`modelado-cli.exe` and `Modelado.exe`.

- **Edit the selected mesh:** Simplify, Subdivide, Smooth, Fill holes, Remove loose pieces,
  Flat base. Each is one step of Blender's undo and keeps the object's name, transform and
  materials.
- **Generate from photo:** a 3D Object (TripoSR) or a Relief (Depth Anything V2) at the 3D
  cursor. The models run locally; their first download is shown, sized and confirmed first.
- **Edit in Modelado:** opens the mesh in the Modelado window; **Send to Blender** there puts
  each version back on the same object.
- **Optional cloud jobs:** with a Meshy AI or Ideogram key stored in Modelado, 3D Object with
  Meshy AI, Remesh, Retexture and a new texture from a prompt. Each shows what is sent and
  its price first. Without a key nothing leaves the PC.

## Permissions, as the reviewers read them

- **files:** the photo chosen, and temporary GLB files the add-on and `modelado-cli` swap.
- **network:** only the optional cloud jobs above, made by `modelado-cli` after the dialog's
  yes. The add-on itself opens no connection.

## Screenshots

1. The Modelado panel in the sidebar, with an edit's amount.
2. Edit in Modelado: the Modelado window holding the mesh, its **Send to Blender** button.
3. The mesh back in Blender after Send to Blender.
4. Generate from photo: the photo and the 3D Object beside it.

## If the platform refuses it

An add-on that drives an external program may be refused. The reason goes in the roadmap's
MOD70 entry, and the add-on stays in the release, installed from disk.
