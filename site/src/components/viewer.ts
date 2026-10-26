// The hero's 3D view (MOD33), imported only once the page has painted its poster, so three.js costs
// the first paint nothing. It draws on demand, when the camera or the style changes, and never
// animates by itself: nothing spins unless the reader turns it.
import {
  AmbientLight,
  Box3,
  DirectionalLight,
  Material,
  Mesh,
  MeshBasicMaterial,
  MeshStandardMaterial,
  PerspectiveCamera,
  Scene,
  Spherical,
  Vector3,
  WebGLRenderer,
} from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";

export type ViewStyle = "colours" | "clay" | "wireframe";

export interface View {
  setStyle(style: ViewStyle): void;
  /** Turns the model by angles in radians, for the arrow keys. */
  turn(horizontal: number, vertical: number): void;
  /** Moves the camera nearer (below 1) or further (above 1). */
  zoom(factor: number): void;
  dispose(): void;
}

export async function mountView(host: HTMLElement, url: string, wireColour: string): Promise<View> {
  const renderer = new WebGLRenderer({ antialias: true, alpha: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  host.appendChild(renderer.domElement);

  const scene = new Scene();
  scene.add(new AmbientLight(0xffffff, 1.4));
  const key = new DirectionalLight(0xffffff, 2.2);
  key.position.set(2, 3, 4);
  scene.add(key);

  const gltf = await new GLTFLoader().loadAsync(url);
  const model = gltf.scene;
  const box = new Box3().setFromObject(model);
  const centre = box.getCenter(new Vector3());
  const radius = box.getSize(new Vector3()).length() / 2;
  model.position.sub(centre);
  scene.add(model);

  const camera = new PerspectiveCamera(35, 1, radius / 100, radius * 100);
  // From the front and a little to the side, as the poster shows it.
  camera.position.setFromSpherical(new Spherical(radius * 3.1, Math.PI * 0.44, Math.PI * 0.14));

  const controls = new OrbitControls(camera, renderer.domElement);
  controls.enablePan = false;
  controls.minDistance = radius * 1.4;
  controls.maxDistance = radius * 8;

  const render = () => renderer.render(scene, camera);
  controls.addEventListener("change", render);

  const meshes: Mesh[] = [];
  model.traverse((node) => {
    if ((node as Mesh).isMesh) meshes.push(node as Mesh);
  });
  const own = new Map<Mesh, Material | Material[]>(meshes.map((m) => [m, m.material]));
  const clay = new MeshStandardMaterial({ color: 0xc8ccd2, roughness: 0.85, metalness: 0 });
  const wire = new MeshBasicMaterial({ color: wireColour, wireframe: true });

  const resize = () => {
    const { clientWidth: w, clientHeight: h } = host;
    if (w === 0 || h === 0) return;
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
    render();
  };
  const watch = new ResizeObserver(resize);
  watch.observe(host);
  resize();

  return {
    setStyle(style) {
      for (const mesh of meshes) {
        mesh.material = style === "clay" ? clay : style === "wireframe" ? wire : own.get(mesh)!;
      }
      render();
    },
    turn(horizontal, vertical) {
      const offset = camera.position.clone().sub(controls.target);
      const at = new Spherical().setFromVector3(offset);
      at.theta -= horizontal;
      at.phi = Math.min(Math.PI - 0.05, Math.max(0.05, at.phi - vertical));
      camera.position.setFromSpherical(at).add(controls.target);
      controls.update();
    },
    zoom(factor) {
      const offset = camera.position.clone().sub(controls.target);
      const distance = Math.min(controls.maxDistance, Math.max(controls.minDistance, offset.length() * factor));
      camera.position.copy(controls.target).add(offset.setLength(distance));
      controls.update();
    },
    dispose() {
      watch.disconnect();
      controls.dispose();
      clay.dispose();
      wire.dispose();
      renderer.dispose();
      renderer.domElement.remove();
    },
  };
}
