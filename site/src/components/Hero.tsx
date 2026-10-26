import { asset, words } from "../content";
import { ModelView } from "./ModelView";

// MOD33: the photo beside the model Modelado made from it, which the reader turns. The model loads
// by itself once the hero is in view (ModelView's `auto`), after the poster has painted.
export function Hero() {
  const w = words.hero;
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
        <ModelView src={w.model.src} poster={w.model.poster} posterAlt={w.model.posterAlt} hint={w.hint} auto />
        <figcaption>
          {w.model.label}
          <span className="hero-facts">{w.model.facts}</span>
        </figcaption>
      </figure>
      <p className="hero-credit">
        <a href={w.creditUrl} rel="noopener">
          {w.credit}
        </a>
      </p>
    </div>
  );
}
