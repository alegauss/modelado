import { useState } from "react";
import { asset, words } from "../content";
import { ModelView } from "./ModelView";

// MOD34: a tab per operation, each on a real photo and its real result. A model loads only when the
// reader asks to turn it, since a relief carries its photo as a texture and weighs megabytes.

type Tab = "object" | "relief" | "edit" | "texture";
const TABS: Tab[] = ["object", "relief", "edit", "texture"];

interface Pair {
  photo: string;
  photoAlt: string;
  model: string;
  poster: string;
  posterAlt: string;
  facts: string;
  credit: string;
  creditUrl: string;
}

function PhotoAndModel({ item }: { item: Pair }) {
  return (
    <div className="studio-pair">
      <figure className="studio-card">
        <img src={asset(item.photo)} alt={item.photoAlt} loading="lazy" />
      </figure>
      <figure className="studio-card">
        <ModelView key={item.model} src={item.model} poster={item.poster} posterAlt={item.posterAlt} hint={words.studio.turnHint} />
        <figcaption className="hero-facts">{item.facts}</figcaption>
      </figure>
      <p className="hero-credit">
        <a href={item.creditUrl} rel="noopener">
          {item.credit}
        </a>
      </p>
    </div>
  );
}

function BeforeAfter(props: { before: string; beforeAlt: string; beforeLabel: string; after: string; afterAlt: string; afterLabel: string; wide?: boolean }) {
  return (
    <div className={`studio-pair${props.wide ? " is-wide" : ""}`}>
      <figure className="studio-card">
        <img src={asset(props.before)} alt={props.beforeAlt} loading="lazy" />
        <figcaption>{props.beforeLabel}</figcaption>
      </figure>
      <figure className="studio-card">
        <img src={asset(props.after)} alt={props.afterAlt} loading="lazy" />
        <figcaption>{props.afterLabel}</figcaption>
      </figure>
    </div>
  );
}

export function Studio() {
  const w = words.studio;
  const [tab, setTab] = useState<Tab>("object");
  const [relief, setRelief] = useState<string>(w.relief.items[0].id);
  const chosen = w.relief.items.find((i) => i.id === relief) ?? w.relief.items[0];

  // Arrow keys move along the tabs, as a tab list is read.
  const onKey = (e: React.KeyboardEvent) => {
    const step = e.key === "ArrowRight" ? 1 : e.key === "ArrowLeft" ? -1 : 0;
    if (!step) return;
    e.preventDefault();
    // From the tab that has focus, which a click elsewhere may have left apart from the selected one.
    const from = TABS.findIndex((t) => `studio-tab-${t}` === (e.target as HTMLElement).id);
    const next = TABS[((from < 0 ? TABS.indexOf(tab) : from) + step + TABS.length) % TABS.length];
    setTab(next);
    document.getElementById(`studio-tab-${next}`)?.focus();
  };

  return (
    <section id="studio" className="studio">
      <div className="wrap">
        <div className="sec-head">
          <p className="eyebrow">{w.eyebrow}</p>
          <h2>{w.title}</h2>
        </div>
        <div className="studio-tabs" role="tablist" aria-label={w.tabsLabel} onKeyDown={onKey}>
          {TABS.map((t) => (
            <button
              key={t}
              id={`studio-tab-${t}`}
              type="button"
              role="tab"
              aria-selected={tab === t}
              aria-controls="studio-panel"
              tabIndex={tab === t ? 0 : -1}
              className={tab === t ? "is-on" : undefined}
              onClick={() => setTab(t)}
            >
              {w[t].tab}
            </button>
          ))}
        </div>
        <div id="studio-panel" role="tabpanel" aria-labelledby={`studio-tab-${tab}`} className="studio-panel">
          <p className="studio-lead">{w[tab].lead}</p>
          {tab === "object" && <PhotoAndModel item={w.object} />}
          {tab === "relief" && (
            <>
              <div className="hero-styles studio-choices" role="radiogroup" aria-label={w.relief.choicesLabel}>
                {w.relief.items.map((i) => (
                  <button key={i.id} type="button" role="radio" aria-checked={relief === i.id} className={relief === i.id ? "is-on" : undefined} onClick={() => setRelief(i.id)}>
                    {i.label}
                  </button>
                ))}
              </div>
              <PhotoAndModel item={chosen} />
            </>
          )}
          {tab === "edit" && <BeforeAfter {...w.edit} />}
          {tab === "texture" && <BeforeAfter {...w.texture} wide />}
        </div>
      </div>
    </section>
  );
}
