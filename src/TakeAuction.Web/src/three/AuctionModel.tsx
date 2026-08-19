import { useLayoutEffect, useMemo, useRef } from "react";
import { useFrame } from "@react-three/fiber";
import { useGLTF } from "@react-three/drei";
import { clone as cloneSkinned } from "three/examples/jsm/utils/SkeletonUtils.js";
import * as THREE from "three";
import type { DragState } from "./useDragRotate";

export const DRACO_PATH = "/draco/";

interface AuctionModelProps {
  url: string;
  fit?: number;
  lift?: number;
  spin?: number;
  autoRotate?: boolean;
  rotationSpeed?: number;
  drag?: React.MutableRefObject<DragState>;
  onDecay?: () => void;
}

export function AuctionModel({
  url,
  fit = 2.6,
  lift = 0,
  spin = 0,
  autoRotate = true,
  rotationSpeed = 0.22,
  drag,
  onDecay,
}: AuctionModelProps) {
  const { scene } = useGLTF(url, DRACO_PATH);
  const pivot = useRef<THREE.Group>(null);
  const idleSpin = useRef(0);

  const cloned = useMemo(() => {
    const copy = cloneSkinned(scene);

    copy.traverse((node) => {
      const mesh = node as THREE.Mesh;
      if (!mesh.isMesh) return;

      mesh.castShadow = true;
      mesh.receiveShadow = true;
      mesh.frustumCulled = false;
    });

    return copy;
  }, [scene]);

  useLayoutEffect(() => {
    const box = new THREE.Box3().setFromObject(cloned);
    const size = box.getSize(new THREE.Vector3());
    const center = box.getCenter(new THREE.Vector3());
    const largest = Math.max(size.x, size.y, size.z) || 1;
    const scale = fit / largest;

    cloned.position.set(-center.x, -center.y, -center.z);
    cloned.scale.setScalar(scale);
    cloned.position.multiplyScalar(scale);
    cloned.position.y += lift;
  }, [cloned, fit, lift]);

  useFrame((_, delta) => {
    const group = pivot.current;
    if (!group) return;

    onDecay?.();

    const state = drag?.current;
    const idle = !state?.active;

    if (autoRotate && idle && !state?.spinning) {
      idleSpin.current += delta * rotationSpeed;
    }

    group.rotation.y = spin + idleSpin.current + (state?.yaw ?? 0);
    group.rotation.x = state?.pitch ?? 0;
  });

  return (
    <group ref={pivot}>
      <primitive object={cloned} />
    </group>
  );
}

export function preloadShowcase(urls: string[]) {
  urls.forEach((url) => useGLTF.preload(url, DRACO_PATH));
}
