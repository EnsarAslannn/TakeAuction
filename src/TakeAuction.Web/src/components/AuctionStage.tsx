import { Suspense, useState } from "react";
import { Canvas } from "@react-three/fiber";
import { AuctionModel } from "@/three/AuctionModel";
import { Studio } from "@/three/Studio";
import { useDragRotate } from "@/three/useDragRotate";
import { ErrorBoundary } from "@/components/ErrorBoundary";
import type { ShowcaseModel } from "@/content/catalog";

const GLOW =
  "radial-gradient(65% 55% at 50% 45%, rgba(192,160,112,0.22) 0%, transparent 72%)";

interface AuctionStageProps {
  title: string;
  imageUrl: string | null;
  showcase: ShowcaseModel | undefined;
  reducedMotion: boolean;
}

export function AuctionStage({ title, imageUrl, showcase, reducedMotion }: AuctionStageProps) {
  const [imageFailed, setImageFailed] = useState(false);

  if (imageUrl && !imageFailed) {
    return (
      <Frame>
        <div
          aria-hidden
          className="absolute inset-0 scale-110 bg-cover bg-center opacity-25 blur-2xl"
          style={{ backgroundImage: `url(${imageUrl})` }}
        />
        <div aria-hidden className="pointer-events-none absolute inset-0" style={{ background: GLOW }} />
        <img
          src={imageUrl}
          alt={title}
          onError={() => setImageFailed(true)}
          className="absolute inset-0 h-full w-full object-contain p-6 md:p-10"
        />
      </Frame>
    );
  }

  if (showcase) {
    return <ModelStage title={title} showcase={showcase} reducedMotion={reducedMotion} />;
  }

  return (
    <Frame>
      <div aria-hidden className="pointer-events-none absolute inset-0" style={{ background: GLOW }} />
      <div className="absolute inset-6 flex flex-col items-center justify-center gap-4 border border-paper/12 md:inset-10">
        <span className="font-mono text-eyebrow uppercase text-paper/35">Görsel eklenmedi</span>
        <p className="max-w-[28ch] text-center font-sans text-sm leading-relaxed text-paper/45">
          Satıcı bu parça için fotoğraf paylaşmadı. Açıklamadaki ayrıntılar ve satıcı bilgisi aşağıda.
        </p>
      </div>
    </Frame>
  );
}

function ModelStage({
  title,
  showcase,
  reducedMotion,
}: {
  title: string;
  showcase: ShowcaseModel;
  reducedMotion: boolean;
}) {
  const [modelFailed, setModelFailed] = useState(false);
  const { state: dragState, dragging, decay, handlers } = useDragRotate();

  return (
    <Frame>
      <div aria-hidden className="pointer-events-none absolute inset-0" style={{ background: GLOW }} />

      <Canvas
        shadows
        dpr={[1, 1.8]}
        camera={{ position: [0, 0.5, 6.2], fov: 35 }}
        gl={{ antialias: true, alpha: true }}
      >
        <Suspense fallback={null}>
          <Studio float={!reducedMotion} shadowOpacity={0.5}>
            <ErrorBoundary resetKey={showcase.slug} onError={() => setModelFailed(true)}>
              <AuctionModel
                url={showcase.model}
                fit={2.9}
                lift={showcase.lift}
                spin={showcase.spin}
                autoRotate={!reducedMotion}
                rotationSpeed={0.14}
                drag={dragState}
                onDecay={decay}
              />
            </ErrorBoundary>
          </Studio>
        </Suspense>
      </Canvas>

      {modelFailed && (
        <p className="pointer-events-none absolute inset-0 flex items-center justify-center font-mono text-eyebrow uppercase text-paper/35">
          3B model yüklenemedi
        </p>
      )}

      <div
        {...handlers}
        role="application"
        aria-label={`${title} — sürükleyerek döndürün`}
        className={`absolute inset-0 ${dragging ? "cursor-grabbing" : "cursor-grab"}`}
        style={{ touchAction: "pan-y" }}
      />

      <p
        className={`pointer-events-none absolute bottom-5 left-5 font-mono text-eyebrow uppercase text-paper/40 transition-opacity duration-500 ${
          dragging ? "opacity-0" : "opacity-100"
        }`}
      >
        Sürükleyerek döndürün
      </p>
    </Frame>
  );
}

function Frame({ children }: { children: React.ReactNode }) {
  return (
    <div className="relative aspect-square overflow-hidden bg-ink lg:aspect-[4/3]">{children}</div>
  );
}
