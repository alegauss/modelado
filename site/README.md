# Modelado site

The public site, in English, at `/modelado/`: a self-contained Vite + React 19 + TypeScript +
Tailwind v4 workspace, after PhotoBoost's. `dotnet build` and `run-tests.cmd` neither build nor
need it.

The chrome is the family's: the nav, footer, buttons and theme toggle come from
`@viglet/viglet-design-system/site`, the colours from its default cool tone (the `--vg-*` tokens),
and the prerender and social card from its `/vite` helpers. The page between the nav and the
footer is Modelado's own.

## Commands

```
npm install        # once
npm run dev        # dev server at /modelado/
npm run build      # tsc, client, SSR, then the prerender: the page, og.png, the touch icon
npm run preview    # serve the built dist/
```

`npm run build` is the gate. The prerender writes the page's title, description, canonical and
og:url by replace-or-throw, so a template that lost an anchor fails the build.

## Where things live

| Path | What |
|---|---|
| `src/content.ts` | **Every word**, and the base and the links, written once. English only: a multilingual interface is a non-goal |
| `src/routes.ts` | The route and its metadata, read by the prerender |
| `src/components/Chrome.tsx` | The design system's nav and footer, with Modelado's brand |
| `src/components/` | The page's own sections |
| `src/components/Hero.tsx`, `viewer.ts` | The hero: the photo beside the model the reader turns. `viewer.ts` is three.js, imported only after the poster has painted, drawn on demand and never spinning by itself; reduced motion keeps the poster until asked |
| `public/demo/` | The hero's real result: TripoSR's example chair (MIT), the model `modelado-cli object --resolution 320` made from it with the published models then `edit --simplify 0.35`, and that model's `--preview` render as the poster. The chair was the closest of six examples tried; remake them with the CLI, not by hand |
| `public/icon.svg` | The mark: the app icon tools/Modelado.Icon draws, on the same grid and in Brand.cs's colours |
| `public/favicon.svg` | The mark at tab size, without the triangle grid, as the app icon leaves it out below 48 px |
| `scripts/prerender.mjs` | The prerender, `og.png` and `apple-touch-icon.png` |
