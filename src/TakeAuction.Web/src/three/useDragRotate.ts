import { useCallback, useRef, useState } from "react";

export interface DragState {
  yaw: number;
  pitch: number;
  active: boolean;
  spinning: boolean;
}

const PITCH_LIMIT = Math.PI / 5;
const SENSITIVITY = 0.0075;
const FRICTION = 0.94;
const STOP_THRESHOLD = 0.0004;

export function useDragRotate() {
  const state = useRef<DragState>({ yaw: 0, pitch: 0, active: false, spinning: false });
  const velocity = useRef({ yaw: 0, pitch: 0 });
  const last = useRef({ x: 0, y: 0 });
  const moved = useRef(false);
  const [dragging, setDragging] = useState(false);

  const onPointerDown = useCallback((event: React.PointerEvent<HTMLDivElement>) => {
    if (event.button !== 0) return;

    event.currentTarget.setPointerCapture(event.pointerId);
    state.current.active = true;
    state.current.spinning = false;
    velocity.current = { yaw: 0, pitch: 0 };
    last.current = { x: event.clientX, y: event.clientY };
    moved.current = false;
    setDragging(true);
  }, []);

  const onPointerMove = useCallback((event: React.PointerEvent<HTMLDivElement>) => {
    if (!state.current.active) return;

    const dx = event.clientX - last.current.x;
    const dy = event.clientY - last.current.y;
    last.current = { x: event.clientX, y: event.clientY };

    if (Math.abs(dx) + Math.abs(dy) > 2) moved.current = true;

    velocity.current.yaw = dx * SENSITIVITY;
    velocity.current.pitch = dy * SENSITIVITY;

    state.current.yaw += velocity.current.yaw;
    state.current.pitch = clamp(state.current.pitch + velocity.current.pitch, -PITCH_LIMIT, PITCH_LIMIT);
  }, []);

  const endDrag = useCallback((event: React.PointerEvent<HTMLDivElement>) => {
    if (!state.current.active) return;

    if (event.currentTarget.hasPointerCapture(event.pointerId)) {
      event.currentTarget.releasePointerCapture(event.pointerId);
    }

    state.current.active = false;
    state.current.spinning = true;
    setDragging(false);
  }, []);

  const decay = useCallback(() => {
    const current = state.current;
    if (current.active || !current.spinning) return;

    velocity.current.yaw *= FRICTION;
    velocity.current.pitch *= FRICTION;

    current.yaw += velocity.current.yaw;
    current.pitch = clamp(current.pitch + velocity.current.pitch, -PITCH_LIMIT, PITCH_LIMIT);

    if (Math.abs(velocity.current.yaw) < STOP_THRESHOLD) {
      velocity.current.yaw = 0;
      velocity.current.pitch = 0;
      current.spinning = false;
    }
  }, []);

  const reset = useCallback(() => {
    state.current.yaw = 0;
    state.current.pitch = 0;
    state.current.spinning = false;
    velocity.current = { yaw: 0, pitch: 0 };
  }, []);

  return {
    state,
    dragging,
    reset,
    decay,
    handlers: {
      onPointerDown,
      onPointerMove,
      onPointerUp: endDrag,
      onPointerCancel: endDrag,
    },
  };
}

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));
