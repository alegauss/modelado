import { SiteButton, SiteFooter, SiteNav, SiteThemeToggle } from "@viglet/viglet-design-system/site";
import { BASE, THEME_KEY, asset, releasesUrl, words } from "../content";

// The nav and the footer, drawn by the design system's site layer so Modelado sits in the same
// family as the other product sites. Everything between them is this product's own.

const brand = { href: BASE, name: "Modelado", logo: asset("icon.svg") };

export function Nav() {
  return (
    <SiteNav
      brand={brand}
      links={words.nav.links}
      actions={
        <>
          <SiteButton href={releasesUrl} rel="noopener">{words.nav.cta}</SiteButton>
          <SiteThemeToggle storageKey={THEME_KEY} label={words.nav.themeLabel} />
        </>
      }
    />
  );
}

export function Footer() {
  return <SiteFooter brand={brand} links={words.footer.links} disclaimer={words.footer.disclaimer} />;
}
