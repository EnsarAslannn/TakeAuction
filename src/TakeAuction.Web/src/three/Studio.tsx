import { ContactShadows, Environment, Float, Lightformer } from "@react-three/drei";

interface StudioProps {
  children: React.ReactNode;
  shadowOpacity?: number;
  float?: boolean;
}

/**
 * Shared lighting rig. The environment map is built from in-scene Lightformers
 * rather than a drei preset, so nothing is fetched from a CDN at runtime.
 */
export function Studio({ children, shadowOpacity = 0.42, float = true }: StudioProps) {
  const body = float ? (
    <Float speed={1.1} rotationIntensity={0.12} floatIntensity={0.35} floatingRange={[-0.06, 0.06]}>
      {children}
    </Float>
  ) : (
    children
  );

  return (
    <>
      <ambientLight intensity={0.5} color="#F4F1EB" />
      <directionalLight
        position={[4.5, 6.5, 3.5]}
        intensity={2.2}
        color="#FFF6E8"
        castShadow
        shadow-mapSize={[1024, 1024]}
        shadow-bias={-0.0004}
      >
        <orthographicCamera attach="shadow-camera" args={[-7, 7, 7, -7, 0.1, 32]} />
      </directionalLight>
      <directionalLight position={[-5, 2.5, -2]} intensity={0.8} color="#7E9BBD" />
      <directionalLight position={[0, -3, 4]} intensity={0.28} color="#C0A070" />

      <Environment resolution={256} frames={1}>
        <color attach="background" args={["#2C2823"]} />
        <Lightformer
          form="rect"
          intensity={5}
          color="#FFF4E4"
          position={[0, 5, -6]}
          rotation={[0, 0, 0]}
          scale={[12, 6, 1]}
        />
        <Lightformer
          form="rect"
          intensity={3.2}
          color="#DCE7F2"
          position={[-6, 2, 2]}
          rotation={[0, Math.PI / 2, 0]}
          scale={[9, 6, 1]}
        />
        <Lightformer
          form="rect"
          intensity={2.4}
          color="#F0DFC6"
          position={[6, 2, 2]}
          rotation={[0, -Math.PI / 2, 0]}
          scale={[9, 6, 1]}
        />
        <Lightformer
          form="circle"
          intensity={4}
          color="#FFFFFF"
          position={[0, 7, 1]}
          rotation={[Math.PI / 2, 0, 0]}
          scale={6}
        />
        <Lightformer
          form="rect"
          intensity={1.2}
          color="#C0A070"
          position={[0, -4, 3]}
          rotation={[-Math.PI / 2, 0, 0]}
          scale={[10, 6, 1]}
        />
      </Environment>

      {body}

      <ContactShadows
        position={[0, -1.65, 0]}
        opacity={shadowOpacity}
        scale={13}
        blur={2.8}
        far={5}
        resolution={512}
        color="#52443D"
      />
    </>
  );
}
