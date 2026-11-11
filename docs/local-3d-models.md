# Local image-to-3D models newer than TripoSR

An evaluation for MOD71, made on 11/11/2026. It asks whether a newer open model should become
Modelado's 3D Object, or sit beside TripoSR as a high-quality option. It is a desk evaluation:
licences come from each model's own card or licence text, and memory figures are the ones the
authors publish. Nothing below was exported to ONNX or timed here. That is MOD80.

## What a candidate must meet

- **Licence:** commercial use allowed without conditions on who or where, as for every model
  Modelado ships today. `scripts\publish_models.py` refuses anything but MIT, Apache-2.0,
  BSD-2-Clause, BSD-3-Clause and CC-BY-4.0.
- **Runtime:** an ONNX export that ONNX Runtime runs with DirectML, so no custom CUDA kernel
  on the path from photo to mesh.
- **Memory:** usable on an 8 GB GPU, the size the site promises, with the CPU as the fallback.

## The candidates

| Model | Licence | Output | Published memory | ONNX and DirectML | Fits |
| --- | --- | --- | --- | --- | --- |
| TripoSR (today) | MIT | Shape and vertex colour | about 4 GB (the README's figure) | Exported; runs on DirectML and the CPU | Yes |
| TripoSG (VAST, 2025) | MIT | Shape only | "over 8 GB" | Plausible: flow transformer, SDF VAE, DINOv2, all standard ops | To measure |
| Step1X-3D geometry (StepFun, 2025) | Apache-2.0 | Shape (texture is a second, SDXL-based stage) | 27 to 29 GB for both stages | Shape stage plausible; texture stage too large | Shape only, second choice |
| TRELLIS.2-4B (Microsoft, 2025) | MIT | Shape and PBR texture | at least 24 GB | No: sparse voxel structures compiled against CUDA | No |
| Hunyuan3D 2.x (Tencent) | Tencent community licence, excluding the EU, the UK and South Korea | Shape and texture | 6 GB (mini, shape) and up | Shape plausible | No: licence |
| Stable Fast 3D (Stability AI) | Stability community licence, free under US$1M revenue | Shape and UV texture | about 6 GB | Custom UV unwrapper and baker in C++ | No: licence |

## Verdict

Keep TripoSR as the default. Measure TripoSG as a "high quality" shape option beside it. It is
the only newer model that passes the licence rule with nothing to compute outside ONNX. It makes
no colour, and Modelado already projects the photo onto the front of the mesh (the **Sharp photo
colours** step), so a TripoSG shape can still be coloured from the photo. Step1X-3D's geometry
stage is the fallback if TripoSG does not export. Look at the texture-capable models again when
one ships under a permissive licence within 8 GB.

## Sources

- TripoSG: huggingface.co/VAST-AI/TripoSG (MIT; the card says "over 8 GB" of VRAM)
- TRELLIS.2: huggingface.co/microsoft/TRELLIS.2-4B (MIT; at least 24 GB; CUDA Toolkit to build)
- Hunyuan3D: the LICENSE in huggingface.co/tencent/Hunyuan3D-2 (the territory excludes the EU,
  the UK and South Korea)
- Stable Fast 3D: huggingface.co/stabilityai/stable-fast-3d (Stability AI Community License)
- Step1X-3D: github.com/stepfun-ai/Step1X-3D (Apache-2.0; memory from its README)
