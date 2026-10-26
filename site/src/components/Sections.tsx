import { SiteButton } from "@viglet/viglet-design-system/site";
import { releasesUrl, words } from "../content";

// MOD34: after the hero and what it makes, the moves from a photo to a file, privacy, speed, the
// models and the download. The release and the models are read at build time (vite.config.ts), so
// a new release or a republished model is what the next build shows.

// Whole megabytes, and one decimal below ten so TripoSR's 0.2 MB decoder is not "0 MB".
const megabytes = (bytes: number) => `${(bytes / 1e6).toFixed(bytes < 10e6 ? 1 : 0)} MB`;

export function Journey() {
  const w = words.journey;
  return (
    <section id="journey" className="journey">
      <div className="wrap">
        <div className="sec-head">
          <p className="eyebrow">{w.eyebrow}</p>
          <h2>{w.title}</h2>
        </div>
        <ol className="journey-steps">
          {w.steps.map((step, i) => (
            <li key={step.title} className="journey-step">
              <span className="journey-n" aria-hidden="true">
                {i + 1}
              </span>
              <h3>{step.title}</h3>
              <p>{step.body}</p>
            </li>
          ))}
        </ol>
      </div>
    </section>
  );
}

export function Privacy() {
  const w = words.privacy;
  return (
    <section id="privacy" className="what">
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

export function Speed() {
  const w = words.speed;
  return (
    <section id="speed">
      <div className="wrap narrow">
        <div className="sec-head">
          <p className="eyebrow">{w.eyebrow}</p>
          <h2>{w.title}</h2>
        </div>
        <p className="sec-lead">{w.lead}</p>
        <table className="facts">
          <tbody>
            {w.rows.map((row) => (
              <tr key={row.what}>
                <th scope="row">{row.what}</th>
                <td>{row.time}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <p className="sec-note">{w.note}</p>
      </div>
    </section>
  );
}

export function Models() {
  const w = words.models;
  return (
    <section id="models" className="what">
      <div className="wrap narrow">
        <div className="sec-head">
          <p className="eyebrow">{w.eyebrow}</p>
          <h2>{w.title}</h2>
        </div>
        <p className="sec-lead">{w.lead}</p>
        <table className="facts">
          <tbody>
            {__MODELS__.map((model) => (
              <tr key={model.name}>
                <th scope="row">
                  {model.name}
                  <span className="facts-sub">{w.tasks[model.task] ?? model.task}</span>
                </th>
                <td>{megabytes(model.bytes)}</td>
                <td>
                  <a href={model.licenseUrl} rel="noopener">
                    {model.license}
                  </a>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

export function Download() {
  const w = words.download;
  const release = __RELEASE__;
  return (
    <section id="download" className="download">
      <div className="wrap narrow">
        <div className="sec-head">
          <p className="eyebrow">{w.eyebrow}</p>
          <h2>{w.title}</h2>
        </div>
        <p className="sec-lead">{w.lead}</p>
        <div className="download-actions">
          <SiteButton href={release?.url ?? releasesUrl} rel="noopener">
            {release ? w.button(release.version, megabytes(release.bytes)) : w.fallback}
          </SiteButton>
        </div>
        <p className="sec-note">{w.sums}</p>
      </div>
    </section>
  );
}
