import { BASE, words } from "./content";

// The one page. The table is what the design system's prerender reads.
export const SITE_ORIGIN = "https://alegauss.github.io";

// Absolute, because a platform fetching the social card is not on this origin.
export const OG_IMAGE = `${SITE_ORIGIN}${BASE}og.png`;

export const ROUTES = [{ path: "/", title: words.meta.title, description: words.meta.description }];

/** The canonical and og:url for a route, carrying the base. */
export function canonicalUrl(path: string): string {
  const rel = path === "/" ? "" : `${path.replace(/^\//, "")}/`;
  return `${SITE_ORIGIN}${BASE}${rel}`;
}

/** Where a route's HTML is written, relative to dist/: "" for the root. */
export function outputDir(path: string): string {
  return path === "/" ? "" : path.replace(/^\//, "");
}
