import { Component, type ReactNode } from "react";

interface ErrorBoundaryProps {
  children: ReactNode;
  fallback?: ReactNode;
  /** Changing this clears a caught error, so the next selection gets a fresh try. */
  resetKey?: string;
  onError?: () => void;
}

interface ErrorBoundaryState {
  failed: boolean;
}

/**
 * A render error anywhere below this point would otherwise unmount the whole
 * app — a single missing GLB is enough to blank the page. Catching it here
 * keeps the failure local to whatever was wrapped.
 */
export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { failed: false };

  static getDerivedStateFromError(): ErrorBoundaryState {
    return { failed: true };
  }

  componentDidCatch(error: unknown) {
    console.error(error);
    this.props.onError?.();
  }

  componentDidUpdate(previous: ErrorBoundaryProps) {
    if (this.state.failed && previous.resetKey !== this.props.resetKey) {
      this.setState({ failed: false });
    }
  }

  render() {
    if (this.state.failed) {
      return this.props.fallback ?? null;
    }

    return this.props.children;
  }
}
