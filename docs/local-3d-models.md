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

## Side by side on the same photos (MOD127)

`scripts\compare_models.py` runs each model and variant on the photos the site publishes, through
modelado-cli, and rewrites the table below: time, peak RAM, where it ran, triangles, whether the
mesh is closed, and the Chamfer distance to the reference run of the same photo (the fp32 model
on the CPU), as a percentage of the reference's size. Its renders go to `.cache\compare`. A
relief here has no base, so it is an open surface and reads as not closed. A new candidate (an
INT8 encoder, Depth Anything 3, TripoSG) is one more entry in its `RUNS`.

Measured on an Intel Core i7-14700 with an NVIDIA GeForce RTX 4060 (8 GB):

<!-- compare:start -->
| Photo | Model | Ran on | Time | Peak RAM | Triangles | Closed | Chamfer to the reference | Render |
| --- | --- | --- | --: | --: | --: | --- | --: | --- |
| robot.png | TripoSR fp32, CPU | CPU | 14.6 s | 4.2 GB | 86,524 | yes | reference | robot-triposr-fp32-cpu.png |
| robot.png | TripoSR fp16, GPU | DirectML · NVIDIA GeForce RTX 4060, 8 GB | 4.1 s | 1.5 GB | 86,604 | yes | 0.71% | robot-triposr-fp16-gpu.png |
| robot.png | TripoSR fp32, GPU | DirectML · NVIDIA GeForce RTX 4060, 8 GB | 6.0 s | 4.1 GB | 86,532 | yes | 0.12% | robot-triposr-fp32-gpu.png |
| chair.png | TripoSR fp32, CPU | CPU | 12.7 s | 3.6 GB | 84,856 | yes | reference | chair-triposr-fp32-cpu.png |
| chair.png | TripoSR fp16, GPU | DirectML · NVIDIA GeForce RTX 4060, 8 GB | 2.8 s | 1.3 GB | 84,784 | yes | 0.70% | chair-triposr-fp16-gpu.png |
| chair.png | TripoSR fp32, GPU | DirectML · NVIDIA GeForce RTX 4060, 8 GB | 5.2 s | 3.7 GB | 84,856 | yes | 0.01% | chair-triposr-fp32-gpu.png |
| object-hamburger.png | TripoSR fp32, CPU | CPU | 12.7 s | 3.7 GB | 111,700 | yes | reference | object-hamburger-triposr-fp32-cpu.png |
| object-hamburger.png | TripoSR fp16, GPU | DirectML · NVIDIA GeForce RTX 4060, 8 GB | 2.8 s | 1.3 GB | 111,780 | yes | 0.65% | object-hamburger-triposr-fp16-gpu.png |
| object-hamburger.png | TripoSR fp32, GPU | DirectML · NVIDIA GeForce RTX 4060, 8 GB | 5.3 s | 3.7 GB | 111,700 | yes | 0.00% | object-hamburger-triposr-fp32-gpu.png |
| relief-building.jpg | Depth Anything V2 Small fp32, CPU | CPU | 0.7 s | 0.3 GB | 83,640 | no | reference | relief-building-depth-anything-v2-small-fp32-cpu.png |
| relief-building.jpg | Depth Anything V2 Small fp16, GPU | DirectML · NVIDIA GeForce RTX 4060, 8 GB | 1.0 s | 0.4 GB | 83,640 | no | 0.28% | relief-building-depth-anything-v2-small-fp16-gpu.png |
| relief-face.jpg | Depth Anything V2 Small fp32, CPU | CPU | 0.7 s | 0.3 GB | 97,410 | no | reference | relief-face-depth-anything-v2-small-fp32-cpu.png |
| relief-face.jpg | Depth Anything V2 Small fp16, GPU | DirectML · NVIDIA GeForce RTX 4060, 8 GB | 1.0 s | 0.4 GB | 97,410 | no | 0.43% | relief-face-depth-anything-v2-small-fp16-gpu.png |
| relief-landscape.jpg | Depth Anything V2 Small fp32, CPU | CPU | 0.7 s | 0.3 GB | 97,410 | no | reference | relief-landscape-depth-anything-v2-small-fp32-cpu.png |
| relief-landscape.jpg | Depth Anything V2 Small fp16, GPU | DirectML · NVIDIA GeForce RTX 4060, 8 GB | 1.0 s | 0.4 GB | 97,410 | no | 0.43% | relief-landscape-depth-anything-v2-small-fp16-gpu.png |
<!-- compare:end -->
