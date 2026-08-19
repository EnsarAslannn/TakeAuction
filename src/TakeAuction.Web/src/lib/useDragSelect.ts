import { useCallback, useRef, useState } from "react";

interface Options {
  step?: number;
  threshold?: number;
  onCommit: (delta: number) => void;
}

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

      if (steps !== 0) onCommit(-steps);
    },
    [step, onCommit]
  );

  return {
    offset,
    dragging,
    didMove: () => moved.current,
    handlers: {
      onPointerDown,
      onPointerMove,
      onPointerUp: end,
      onPointerCancel: end,
    },
  };
}
