import { Suspense, useEffect, useState } from "react";
import { Canvas } from "@react-three/fiber";
import { AdaptiveDpr, Preload } from "@react-three/drei";
import { AuctionModel } from "./AuctionModel";
import { Studio } from "./Studio";
import { ErrorBoundary } from "@/components/ErrorBoundary";
import { useIsCompact, usePrefersReducedMotion } from "@/lib/hooks";
import type { DragState } from "./useDragRotate";
import type { ShowcaseModel } from "@/content/catalog";

interface ShowcaseCanvasProps {
  item: ShowcaseModel;
  drag: React.MutableRefObject<DragState>;
  onDecay: () => void;
  /** Drives the render loop. False parks the renderer while the stage is off-screen. */
  active: boolean;
  className?: string;
}

/**
 * One persistent WebGL context for the whole showcase. Swapping `item` swaps the
 * loaded model inside the same canvas, so switching never tears down the renderer.
 */
export function ShowcaseCanvas({ item, drag, onDecay, active, className }: ShowcaseCanvasProps) {
  const reducedMotion = usePrefersReducedMotion();
  const compact = useIsCompact();
  const [ready, setReady] = useState(false);
  const [modelFailed, setModelFailed] = useState(false);

  useEffect(() => setModelFailed(false), [item.slug]);

  return (
    <div className={className}>
      <Canvas
        shadows
        // Parked while off-screen: the model auto-rotates and casts shadows every
        // frame, and that competes with scrolling for the whole rest of the page.
        frameloop={active ? "always" : "never"}
        dpr={[1, 1.5]}
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
              {/* Scoped to the model alone so a lot that fails to load leaves
                  the renderer and its lighting rig standing. */}
              <ErrorBoundary resetKey={item.slug} onError={() => setModelFailed(true)}>
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
              </ErrorBoundary>
            </group>
          </Studio>
          <Preload all />
        </Suspense>
        <AdaptiveDpr pixelated />
      </Canvas>

      {modelFailed && (
        <p className="pointer-events-none absolute inset-0 flex items-center justify-center font-mono text-eyebrow uppercase text-paper/35">
          3B model yüklenemedi
        </p>
      )}
    </div>
  );
}
