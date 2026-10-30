// MOD68: every model the built site serves, opened in a real browser by the three.js the site
// ships, and compared with the file it was packed from. Run after `npm run build`:
//
//   npm run check:models
//
// A local server serves the site folder. A blank page with an import map loads each
// dist/demo/**/*.glb through GLTFLoader with the MeshoptDecoder the viewer sets, and the
// matching public/demo file without it. Both must give the same triangles, the same texture size
// and the same bounds within a quantization step, with no error in the console. Then the Studio's download, which
// is every dist/demo model, must be under 4 MB.
import { createReadStream, readdirSync, statSync } from "node:fs";
import { createServer } from "node:http";
import { dirname, extname, join, relative, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";

const siteDir = join(dirname(fileURLToPath(import.meta.url)), "..");
const LIMIT = 4_000_000;

function* models(folder) {
  for (const name of readdirSync(folder)) {
    const path = join(folder, name);
    if (statSync(path).isDirectory()) yield* models(path);
    else if (name.endsWith(".glb")) yield path;
  }
}

const packed = [...models(join(siteDir, "dist", "demo"))];
if (packed.length === 0) throw new Error("dist/demo holds no model: run npm run build first");
const total = packed.reduce((sum, path) => sum + statSync(path).size, 0);

const types = { ".html": "text/html", ".js": "text/javascript", ".glb": "model/gltf-binary", ".wasm": "application/wasm" };
const server = createServer((request, response) => {
  const path = join(siteDir, decodeURIComponent(new URL(request.url, "http://x").pathname));
  if (!path.startsWith(siteDir + sep) || !statSync(path, { throwIfNoEntry: false })?.isFile()) {
    response.writeHead(404).end();
    return;
  }
  response.writeHead(200, { "Content-Type": types[extname(path)] ?? "application/octet-stream" });
  createReadStream(path).pipe(response);
});
await new Promise((ready) => server.listen(0, "127.0.0.1", ready));
const root = `http://127.0.0.1:${server.address().port}`;

const page = `<!doctype html><script type="importmap">{"imports":{
  "three": "/node_modules/three/build/three.module.js",
  "three/addons/": "/node_modules/three/examples/jsm/"}}</script>
<script type="module">
  import { Box3 } from "three";
  import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
  import { MeshoptDecoder } from "three/addons/libs/meshopt_decoder.module.js";
  window.measure = async (url, packed) => {
    const loader = new GLTFLoader();
    if (packed) loader.setMeshoptDecoder(MeshoptDecoder);
    const scene = (await loader.loadAsync(url)).scene;
    let triangles = 0;
    let texture = "";
    scene.traverse((node) => {
      if (!node.isMesh) return;
      const geometry = node.geometry;
      triangles += (geometry.index ? geometry.index.count : geometry.attributes.position.count) / 3;
      const map = node.material.map?.image;
      if (map) texture = map.width + " x " + map.height;
      else if (geometry.attributes.color) texture = "vertex colours";
    });
    const box = new Box3().setFromObject(scene);
    return { triangles, texture, min: box.min.toArray(), max: box.max.toArray() };
  };
  window.ready = true;
</script>`;

const browser = await chromium.launch();
const failures = [];
try {
  const tab = await browser.newPage();
  const errors = [];
  tab.on("console", (message) => message.type() === "error" && errors.push(message.text()));
  tab.on("pageerror", (error) => errors.push(error.message));
  await tab.route(`${root}/check.html`, (route) => route.fulfill({ contentType: "text/html", body: page }));
  await tab.goto(`${root}/check.html`);
  await tab.waitForFunction(() => window.ready === true);

  for (const model of packed) {
    const name = relative(join(siteDir, "dist"), model).split(sep).join("/");
    const served = await tab.evaluate(([url]) => window.measure(url, true), [`/dist/${name}`]);
    const source = await tab.evaluate(([url]) => window.measure(url, false), [`/public/${name}`]);
    const size = Math.max(...source.max.map((v, i) => v - source.min[i]));
    const moved = Math.max(...[...served.min, ...served.max].map((v, i) => Math.abs(v - [...source.min, ...source.max][i])));
    console.log(`${name}: ${served.triangles} triangles (source ${source.triangles}), texture ${served.texture || "none"} (source ${source.texture || "none"}), bounds within ${(moved / size).toExponential(1)} of the size`);
    if (served.triangles !== source.triangles) failures.push(`${name} has ${served.triangles} triangles, its source ${source.triangles}`);
    if (served.texture !== source.texture) failures.push(`${name}'s texture is ${served.texture || "none"}, its source's ${source.texture || "none"}`);
    if (moved > size * 1e-3) failures.push(`${name}'s bounds moved ${moved} on a size of ${size}`);
  }

  if (errors.length > 0) failures.push(`the browser reported: ${errors.join("; ")}`);
} finally {
  await browser.close();
  server.close();
}

console.log(`the Studio's models: ${(total / 1e6).toFixed(2)} MB, limit ${(LIMIT / 1e6).toFixed(0)} MB`);
if (total >= LIMIT) failures.push(`the served models weigh ${total} bytes, over ${LIMIT}`);
if (failures.length > 0) {
  console.error(failures.map((f) => `FAIL ${f}`).join("\n"));
  process.exit(1);
}
console.log(`every one of the ${packed.length} served models opens as its source does`);
