import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { defineConfig, type Plugin } from "vite";
import react from "@vitejs/plugin-react";
import tailwindcss from "@tailwindcss/vite";
import { siteThemeScript } from "@viglet/viglet-design-system/site";

// GitHub Pages serves a project site under the repository's name, so the site lives at
// https://alegauss.github.io/modelado/ and every asset path carries the prefix. Written here
// and in src/content.ts, and nowhere else.
export const BASE = "/modelado/";

// The key the theme choice is stored under; src/content.ts names the same one for the toggle.
const THEME_KEY = "modelado-theme";

// The pre-paint theme script comes from the design system rather than being typed into
// index.html, so the page and the toggle cannot disagree about the rule. Replace-or-throw: a
// template that lost its anchor would ship a flash of the wrong theme.
function themeScript(): Plugin {
  const anchor = "<!--theme-script-->";
  return {
    name: "theme-script",
    transformIndexHtml(html) {
      if (!html.includes(anchor)) throw new Error(`index.html lost its ${anchor} anchor`);
      return html.replace(anchor, () => `<script>${siteThemeScript(THEME_KEY)}</script>`);
    },
  };
}

// MOD34: what the page says about the download and the models, read once at build time rather
// than typed, so a release or a republished model is what the next build shows. site.yml builds
// again when a release is published or the embedded manifest changes.
const REPO = "alegauss/modelado";

interface Release {
  version: string;
  installer: string;
  url: string;
  bytes: number;
}

// The latest release's installer. Offline, the page links to the releases page and names no version.
async function latestRelease(): Promise<Release | null> {
  try {
    const answer = await fetch(`https://api.github.com/repos/${REPO}/releases/latest`, {
      headers: { "User-Agent": "modelado-site", Accept: "application/vnd.github+json" },
    });
    if (!answer.ok) throw new Error(`GitHub answered ${answer.status}`);
    const release = (await answer.json()) as { tag_name: string; assets: { name: string; size: number; browser_download_url: string }[] };
    const installer = release.assets.find((a) => /^Modelado-Setup-.*\.exe$/.test(a.name));
    if (!installer) throw new Error(`${release.tag_name} has no Modelado-Setup-*.exe`);
    return { version: release.tag_name.replace(/^v/, ""), installer: installer.name, url: installer.browser_download_url, bytes: installer.size };
  } catch (error) {
    console.warn(`site: the latest release could not be read (${(error as Error).message}); the download links to the releases page`);
    return null;
  }
}

interface ListedModel {
  name: string;
  task: string;
  bytes: number;
  license: string;
  licenseUrl: string;
}

// The models the app downloads, from the manifest it embeds: the largest variant's size each.
function listedModels(): ListedModel[] {
  const path = fileURLToPath(new URL("../src/Modelado.Inference/models-manifest.json", import.meta.url));
  const manifest = JSON.parse(readFileSync(path, "utf8")) as {
    models: { name: string; task: string; license: string; license_url: string; variants: Record<string, { bytes: number }> }[];
  };
  return manifest.models.map((m) => ({
    name: m.name,
    task: m.task,
    bytes: Math.max(...Object.values(m.variants).map((v) => v.bytes)),
    license: m.license,
    licenseUrl: m.license_url,
  }));
}

export default defineConfig(async () => ({
  base: BASE,
  plugins: [react(), tailwindcss(), themeScript()],
  define: {
    __RELEASE__: JSON.stringify(await latestRelease()),
    __MODELS__: JSON.stringify(listedModels()),
  },
  build: {
    outDir: "dist",
    emptyOutDir: true,
    // The hero's viewer chunk is three.js, about 630 kB, imported only after the poster has
    // painted (MOD33); the page's own bundle stays far below this.
    chunkSizeWarningLimit: 700,
  },
}));
