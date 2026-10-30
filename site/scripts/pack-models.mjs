// MOD68: the demo models compressed for the web, after the client build has copied public/ to
// dist/. public/demo keeps what the app wrote (compact, KHR_mesh_quantization), which Blender and
// the window open; only the copies the site serves are packed with gltfpack -cc
// (EXT_meshopt_compression), which three.js reads through the MeshoptDecoder the viewer sets.
// Textures pass through as they are.
import { execFileSync } from "node:child_process";
import { readdirSync, renameSync, statSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join, relative } from "node:path";
import { fileURLToPath } from "node:url";

const siteDir = join(dirname(fileURLToPath(import.meta.url)), "..");
const demo = join(siteDir, "dist", "demo");
const gltfpack = createRequire(import.meta.url).resolve("gltfpack/cli.js");

function* models(folder) {
  for (const name of readdirSync(folder)) {
    const path = join(folder, name);
    if (statSync(path).isDirectory()) yield* models(path);
    else if (name.endsWith(".glb")) yield path;
  }
}

let before = 0;
let after = 0;
for (const model of models(demo)) {
  const packed = `${model}.packed.glb`;
  execFileSync(process.execPath, [gltfpack, "-i", model, "-o", packed, "-cc"], { stdio: ["ignore", "ignore", "inherit"] });
  const was = statSync(model).size;
  const is = statSync(packed).size;
  renameSync(packed, model);
  before += was;
  after += is;
  console.log(`packed: ${relative(siteDir, model)}  ${(was / 1e6).toFixed(2)} -> ${(is / 1e6).toFixed(2)} MB`);
}
console.log(`packed: the demo models ${(before / 1e6).toFixed(2)} -> ${(after / 1e6).toFixed(2)} MB`);
