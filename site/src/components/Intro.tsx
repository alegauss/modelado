import { SiteButton } from "@viglet/viglet-design-system/site";
import { releasesUrl, repoUrl, words } from "../content";

// The first word: what Modelado is and where to get it. MOD33's hero, a model the reader turns,
// replaces the plain heading here.
export function Intro() {
  const w = words.intro;
  return (
    <section className="intro">
      <div className="wrap">
        <p className="eyebrow">{w.eyebrow}</p>
        <h1>{w.title}</h1>
        <p className="intro-lead">{w.lead}</p>
        <div className="intro-actions">
          <SiteButton href={releasesUrl} rel="noopener">{w.download}</SiteButton>
          <SiteButton variant="ghost" href={repoUrl} rel="noopener">{w.source}</SiteButton>
        </div>
      </div>
    </section>
  );
}

export function What() {
  const w = words.what;
  return (
    <section id="what" className="what">
      <div className="wrap">
        <div className="sec-head">
          <p className="eyebrow">{w.eyebrow}</p>
          <h2>{w.title}</h2>
        </div>
        <ul className="what-grid">
          {w.items.map((item) => (
            <li key={item.title} className="what-item">
              <h3>{item.title}</h3>
              <p>{item.body}</p>
            </li>
          ))}
        </ul>
      </div>
    </section>
  );
}
