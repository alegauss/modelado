import { words } from "../content";

// MOD79: the privacy policy, a page of its own at /modelado/privacy/ because the Microsoft Store
// listing needs a URL for it. The home page's Privacy section summarises it and links here.
export function PrivacyPolicy() {
  const w = words.policy;
  return (
    <section className="policy">
      <div className="wrap">
        <div className="sec-head">
          <p className="eyebrow">{w.eyebrow}</p>
          <h1>{w.heading}</h1>
          <p className="policy-updated">{w.updated}</p>
        </div>
        {w.sections.map((section) => (
          <div key={section.title} className="policy-part">
            <h2>{section.title}</h2>
            {section.body.map((paragraph) => (
              <p key={paragraph.slice(0, 40)}>{paragraph}</p>
            ))}
          </div>
        ))}
        <p className="policy-contact">
          <a href={w.contactUrl} rel="noopener">
            {w.contact}
          </a>
        </p>
      </div>
    </section>
  );
}
