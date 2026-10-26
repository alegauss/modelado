import { useEffect, useRef, useState } from "react";
import { asset, words } from "../content";
import type { View, ViewStyle } from "./viewer";

// MOD33: the photo beside the model Modelado made from it, which the reader turns. The poster (the
// same model's --preview render) is the first paint and stays for whoever cannot or will not load
// WebGL; three.js arrives after it. Nothing moves by itself, and a reader who asked for reduced
// motion keeps the poster until they choose to turn the model.

const STEP = Math.PI / 18; // ten degrees an arrow key

// Any CSS colour, oklch included, as the rgb() three.js reads: drawn on one pixel and read back.
function cssColour(name: string, fallback: string): string {
  const probe = document.createElement("canvas").getContext("2d");
  if (!probe) return fallback;
  probe.fillStyle = fallback;
  probe.fillStyle = getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback;
  probe.fillRect(0, 0, 1, 1);
  const [r, g, b] = probe.getImageData(0, 0, 1, 1).data;
  return `rgb(${r}, ${g}, ${b})`;
}

export function Hero() {
  const w = words.hero;
  const host = useRef<HTMLDivElement>(null);
  const view = useRef<View | null>(null);
  const [state, setState] = useState<"poster" | "loading" | "ready" | "failed">("poster");
  const [wanted, setWanted] = useState(false);
  const [style, setStyle] = useState<ViewStyle>("colours");

  // Without reduced motion the model loads once the hero is in view, after the poster has painted.
  useEffect(() => {
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches || !host.current) return;
    const seen = new IntersectionObserver((entries) => {
      if (entries.some((e) => e.isIntersecting)) {
        seen.disconnect();
        setWanted(true);
      }
    });
    seen.observe(host.current);
    return () => seen.disconnect();
  }, []);

  useEffect(() => {
    if (!wanted || !host.current) return;
    let gone = false;
    setState("loading");
    import("./viewer")
      .then(({ mountView }) => mountView(host.current!, asset(w.model.src), cssColour("--vg-brand", "#3b6fe0")))
      .then((mounted) => {
        if (gone) return mounted.dispose();
        view.current = mounted;
        setState("ready");
      })
      .catch(() => !gone && setState("failed"));
    return () => {
      gone = true;
      view.current?.dispose();
      view.current = null;
    };
  }, [wanted, w.model.src]);

  useEffect(() => view.current?.setStyle(style), [style, state]);

  const onKey = (e: React.KeyboardEvent) => {
    const v = view.current;
    if (!v) return;
    const moves: Record<string, () => void> = {
      ArrowLeft: () => v.turn(-STEP, 0),
      ArrowRight: () => v.turn(STEP, 0),
      ArrowUp: () => v.turn(0, STEP),
      ArrowDown: () => v.turn(0, -STEP),
      "+": () => v.zoom(0.85),
      "=": () => v.zoom(0.85),
      "-": () => v.zoom(1 / 0.85),
    };
    const move = moves[e.key];
    if (move) {
      e.preventDefault();
      move();
    }
  };

  const ready = state === "ready";
  return (
    <div className="hero-stage">
      <figure className="hero-card">
        <img src={asset(w.photo.src)} alt={w.photo.alt} width={512} height={512} />
        <figcaption>{w.photo.label}</figcaption>
      </figure>
      <span className="hero-arrow" aria-hidden="true">
        {w.arrow}
      </span>
      <figure className="hero-card hero-model">
        <div
          ref={host}
          className={`hero-view${ready ? " is-ready" : ""}`}
          tabIndex={ready ? 0 : -1}
          role={ready ? "application" : undefined}
          aria-label={ready ? w.viewerLabel : undefined}
          onKeyDown={onKey}
        >
          {!ready && <img src={asset(w.model.poster)} alt={w.model.posterAlt} width={800} height={800} />}
          {state === "poster" && (
            <button type="button" className="hero-turn" onClick={() => setWanted(true)}>
              {w.turn}
            </button>
          )}
          {state === "loading" && <p className="hero-status">{w.loading}</p>}
          {state === "failed" && <p className="hero-status">{w.failed}</p>}
        </div>
        <figcaption>
          {w.model.label}
          <span className="hero-facts">{w.model.facts}</span>
        </figcaption>
        {ready && (
          <div className="hero-styles" role="radiogroup" aria-label={w.stylesLabel}>
            {w.styles.map((s) => (
              <button
                key={s.id}
                type="button"
                role="radio"
                aria-checked={style === s.id}
                className={style === s.id ? "is-on" : undefined}
                onClick={() => setStyle(s.id)}
              >
                {s.label}
              </button>
            ))}
          </div>
        )}
        {ready && <p className="hero-hint">{w.hint}</p>}
      </figure>
      <p className="hero-credit">
        <a href={w.creditUrl} rel="noopener">
          {w.credit}
        </a>
      </p>
    </div>
  );
}
