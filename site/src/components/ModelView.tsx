import { useEffect, useRef, useState } from "react";
import { asset, words } from "../content";
import type { View, ViewStyle } from "./viewer";

// A model the reader turns (MOD33, shared with MOD34's Studio). The poster, the same model's
// --preview render, is the first paint and stays for whoever cannot or will not load WebGL; three.js
// arrives after it. Nothing moves by itself. With `auto` the model loads once it is in view, unless
// the reader asked for reduced motion; without it, and for that reader, it waits for "Turn the model".

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

interface Props {
  src: string;
  poster: string;
  posterAlt: string;
  auto?: boolean;
  hint?: string;
}

export function ModelView({ src, poster, posterAlt, auto = false, hint }: Props) {
  const w = words.viewer;
  const host = useRef<HTMLDivElement>(null);
  const view = useRef<View | null>(null);
  const [state, setState] = useState<"poster" | "loading" | "ready" | "failed">("poster");
  const [wanted, setWanted] = useState(false);
  const [style, setStyle] = useState<ViewStyle>("colours");

  useEffect(() => {
    if (!auto || window.matchMedia("(prefers-reduced-motion: reduce)").matches || !host.current) return;
    const seen = new IntersectionObserver((entries) => {
      if (entries.some((e) => e.isIntersecting)) {
        seen.disconnect();
        setWanted(true);
      }
    });
    seen.observe(host.current);
    return () => seen.disconnect();
  }, [auto]);

  useEffect(() => {
    if (!wanted || !host.current) return;
    let gone = false;
    setState("loading");
    import("./viewer")
      .then(({ mountView }) => mountView(host.current!, asset(src), cssColour("--vg-brand", "#3b6fe0")))
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
  }, [wanted, src]);

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
    <>
      <div
        ref={host}
        className={`hero-view${ready ? " is-ready" : ""}`}
        tabIndex={ready ? 0 : -1}
        role={ready ? "application" : undefined}
        aria-label={ready ? w.label : undefined}
        onKeyDown={onKey}
      >
        {!ready && <img src={asset(poster)} alt={posterAlt} width={800} height={800} loading={auto ? "eager" : "lazy"} />}
        {state === "poster" && (
          <button type="button" className="hero-turn" onClick={() => setWanted(true)}>
            {w.turn}
          </button>
        )}
        {state === "loading" && <p className="hero-status">{w.loading}</p>}
        {state === "failed" && <p className="hero-status">{w.failed}</p>}
      </div>
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
      {ready && hint && <p className="hero-hint">{hint}</p>}
    </>
  );
}
