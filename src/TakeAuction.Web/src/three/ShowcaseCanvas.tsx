import { Suspense, useState } from "react";
import { Canvas } from "@react-three/fiber";
import { AdaptiveDpr, Preload } from "@react-three/drei";
import { AuctionModel } from "./AuctionModel";
import { Studio } from "./Studio";
import { useIsCompact, usePrefersReducedMotion } from "@/lib/hooks";
import type { DragState } from "./useDragRotate";
import type { ShowcaseModel } from "@/content/catalog";

interface ShowcaseCanvasProps {
  item: ShowcaseModel;
  drag: React.MutableRefObject<DragState>;
  onDecay: () => void;
  className?: string;
}

/**
 * One persistent WebGL context for the whole showcase. Swapping `item` swaps the
 * loaded model inside the same canvas, so switching never tears down the renderer.
 */
export function ShowcaseCanvas({ item, drag, onDecay, className }: ShowcaseCanvasProps) {
  const reducedMotion = usePrefersReducedMotion();
  const compact = useIsCompact();
  const [ready, setReady] = useState(false);

  return (
    <div className={className}>
      <Canvas
        shadows
        dpr={[1, 1.8]}
        gl={{ antialias: true, alpha: true, preserveDrawingBuffer: false }}
        camera={{ position: [0, 0.4, 6.4], fov: 34, near: 0.1, far: 60 }}
        onCreated={() => setReady(true)}
        style={{ opacity: ready ? 1 : 0, transition: "opacity 900ms cubic-bezier(0.16,1,0.3,1)" }}
      >
        <Suspense fallback={null}>
          <Studio float={!reducedMotion}>
            {/* On narrow screens the stage shares its height with the caption,
                so the model shrinks and rides higher to stay clear of it. */}
            <group position={[0, compact ? 1.15 : -0.15, 0]}>
              <AuctionModel
                key={item.slug}
                url={item.model}
                fit={(compact ? 1.7 : 2.9) * item.scale}
                lift={item.lift}
                spin={item.spin}
                autoRotate={!reducedMotion}
                rotationSpeed={0.18}
                drag={drag}
                onDecay={onDecay}
              />
            </group>
          </Studio>
          <Preload all />
        </Suspense>
        <AdaptiveDpr pixelated />
      </Canvas>
    </div>
  );
}
