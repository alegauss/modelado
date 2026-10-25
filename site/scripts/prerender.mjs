// The prerender, the social card and the touch icon, after the client and SSR builds (MOD32,
// after PhotoBoost's scripts/prerender.mjs).
//
// prerenderSite writes the rendered page into dist/index.html, patching the <head> by
// replace-or-throw, and writes sitemap.xml and robots.txt from the same route table. ogCardSvg
// draws the family's social card in the cool tone with the Modelado mark, and renderOgCard
// rasterises it to dist/og.png at 1200 x 630. The 180 px touch icon is rasterised from
// public/icon.svg, since an Apple device will not take an SVG for it.
import { readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { Resvg } from "@resvg/resvg-js";
import { ogCardSvg, prerenderSite, renderOgCard } from "@viglet/viglet-design-system/vite";

import { render, ROUTES, canonicalUrl, outputDir, OG_IMAGE } from "../dist-server/entry-server.js";

const siteDir = join(dirname(fileURLToPath(import.meta.url)), "..");
const distDir = join(siteDir, "dist");
const icon = readFileSync(join(siteDir, "public", "icon.svg"), "utf8");

const card = await renderOgCard({
  svg: ogCardSvg({
    eyebrow: "Modelado · for Windows",
    title: ["A photo becomes"],
    titleAccent: "a 3D model.",
    line: ["A whole object or a relief, cleaned up and exported,", "with AI on your own PC. Nothing is uploaded."],
    mark: icon,
    tone: "cool",
  }),
  out: join(distDir, "og.png"),
});
console.log(`og-image: dist/og.png  ${card.width}x${card.height}  (${(card.bytes / 1024).toFixed(0)} kB)`);

const touch = new Resvg(icon, { fitTo: { mode: "width", value: 180 } }).render().asPng();
writeFileSync(join(distDir, "apple-touch-icon.png"), touch);
console.log(`touch icon: dist/apple-touch-icon.png  180x180  (${(touch.length / 1024).toFixed(0)} kB)`);

const written = prerenderSite({
  distDir,
  routes: ROUTES,
  render,
  canonicalUrl,
  outputDir,
  ogImage: OG_IMAGE,
  git: { cwd: siteDir },
  log: console.log,
});

console.log(`prerender: ${written.length} route(s), sitemap.xml and robots.txt written to dist/`);
