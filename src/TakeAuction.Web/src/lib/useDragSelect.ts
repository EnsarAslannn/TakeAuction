import { useCallback, useRef, useState } from "react";

interface Options {
  /** Pixels of horizontal travel that commit one step. */
  step?: number;
  /** Movement below this is treated as a click, not a drag. */
  threshold?: number;
  onCommit: (delta: number) => void;
}

/**
 * Horizontal drag that reports a live pixel offset for feedback and commits a
 * discrete step count on release.
 *
 * The pointer is captured only once movement passes the threshold. Capturing on
 * pointerdown would retarget the subsequent `click` to the capturing element,
 * which silently breaks clicks on the cards underneath.
 */
export function useDragSelect({ step = 190, threshold = 6, onCommit }: Options) {
  const [offset, setOffset] = useState(0);
  const [dragging, setDragging] = useState(false);

  const startX = useRef(0);
  const pressed = useRef(false);
  const moved = useRef(false);

  const onPointerDown = useCallback((event: React.PointerEvent<HTMLDivElement>) => {
    if (event.button !== 0) return;

    startX.current = event.clientX;
    pressed.current = true;
    moved.current = false;
  }, []);

  const onPointerMove = useCallback(
    (event: React.PointerEvent<HTMLDivElement>) => {
      if (!pressed.current) return;

      const dx = event.clientX - startX.current;

      if (!moved.current) {
        if (Math.abs(dx) < threshold) return;

        moved.current = true;
        setDragging(true);
        event.currentTarget.setPointerCapture(event.pointerId);
      }

      // Rubber-band so the strip resists rather than tracking the pointer 1:1.
      setOffset(Math.sign(dx) * Math.min(Math.abs(dx) * 0.55, step * 1.2));
    },
    [step, threshold]
  );

  const end = useCallback(
    (event: React.PointerEvent<HTMLDivElement>) => {
      if (!pressed.current) return;
      pressed.current = false;

      if (event.currentTarget.hasPointerCapture(event.pointerId)) {
        event.currentTarget.releasePointerCapture(event.pointerId);
      }

      if (!moved.current) {
        setDragging(false);
        return;
      }

      const dx = event.clientX - startX.current;
      const steps = Math.trunc(dx / (step * 0.5));

      setDragging(false);
      setOffset(0);

      // Dragging left (negative dx) advances forward.
      if (steps !== 0) onCommit(-steps);
    },
    [step, onCommit]
  );

  return {
    offset,
    dragging,
    /** True when the last gesture actually moved — use to suppress click-through. */
    didMove: () => moved.current,
    handlers: {
      onPointerDown,
      onPointerMove,
      onPointerUp: end,
      onPointerCancel: end,
    },
  };
}
