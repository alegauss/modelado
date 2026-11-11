import { Footer, Nav } from "./components/Chrome";
import { Intro, What } from "./components/Intro";
import { PrivacyPolicy } from "./components/Policy";
import { Download, Journey, Models, Privacy, Speed } from "./components/Sections";
import { Studio } from "./components/Studio";
import { words } from "./content";

// The page between the design system's nav and footer is Modelado's own: the first word and the
// hero a reader turns (MOD33), what it makes, then the moves, privacy, speed, the models and the
// download (MOD34). MOD79 adds a second route, the privacy policy, chosen by path: the prerender
// renders each route's path and the client hydrates the one it was served.
export function App({ path = "/" }: { path?: string }) {
  return (
    <>
      <Nav />
      <main>{path === words.policy.path ? <PrivacyPolicy /> : <Home />}</main>
      <Footer />
    </>
  );
}

function Home() {
  return (
    <>
      <Intro />
      <What />
      <Studio />
      <Journey />
      <Privacy />
      <Speed />
      <Models />
      <Download />
    </>
  );
}
