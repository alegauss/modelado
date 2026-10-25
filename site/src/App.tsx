import { Footer, Nav } from "./components/Chrome";
import { Intro, What } from "./components/Intro";

// The page between the design system's nav and footer is Modelado's own. MOD32 puts up the
// workspace, the chrome and a first word; the hero a reader turns (MOD33) and the sections on each
// operation, privacy, formats and the download (MOD34) take their places here.
export function App() {
  return (
    <>
      <Nav />
      <main>
        <Intro />
        <What />
      </main>
      <Footer />
    </>
  );
}
