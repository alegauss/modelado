import { Footer, Nav } from "./components/Chrome";
import { Intro, What } from "./components/Intro";
import { Download, Journey, Models, Privacy, Speed } from "./components/Sections";

// The page between the design system's nav and footer is Modelado's own: the first word and the
// hero a reader turns (MOD33), what it makes, then the moves, privacy, speed, the models and the
// download (MOD34).
export function App() {
  return (
    <>
      <Nav />
      <main>
        <Intro />
        <What />
        <Journey />
        <Privacy />
        <Speed />
        <Models />
        <Download />
      </main>
      <Footer />
    </>
  );
}
