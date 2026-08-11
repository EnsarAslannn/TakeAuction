import { useEffect, useRef, useState } from "react";
import { auctionHub, type ConnectionState } from "./hub";
import type { AuctionStatusChangedNotification, BidPlacedNotification } from "@/api/types";

interface Handlers {
  onBidPlaced?: (notification: BidPlacedNotification) => void;
  onStatusChanged?: (notification: AuctionStatusChangedNotification) => void;
}

export function useConnectionState(): ConnectionState {
  const [state, setState] = useState<ConnectionState>("disconnected");
  useEffect(() => auctionHub.onStateChange(setState), []);
  return state;
}

function useStableHandlers(handlers: Handlers) {
  const ref = useRef(handlers);
  ref.current = handlers;
  return ref;
}

export function useAuctionChannel(auctionId: string | undefined, handlers: Handlers) {
  const handlersRef = useStableHandlers(handlers);

  useEffect(() => {
    const offBid = auctionHub.onBidPlaced((notification) => {
      if (!auctionId || notification.auctionId === auctionId) {
        handlersRef.current.onBidPlaced?.(notification);
      }
    });

    const offStatus = auctionHub.onStatusChanged((notification) => {
      if (!auctionId || notification.auctionId === auctionId) {
        handlersRef.current.onStatusChanged?.(notification);
      }
    });

    return () => {
      offBid();
      offStatus();
    };
  }, [auctionId, handlersRef]);

  useEffect(() => {
    if (!auctionId) return;

    let disposed = false;
    let unsubscribe: (() => void) | undefined;

    auctionHub.subscribeToAuction(auctionId).then((fn) => {
      if (disposed) fn();
      else unsubscribe = fn;
    });

    return () => {
      disposed = true;
      unsubscribe?.();
    };
  }, [auctionId]);
}

export function useLobbyChannel(handlers: Handlers) {
  const handlersRef = useStableHandlers(handlers);

  useEffect(() => {
    const offBid = auctionHub.onBidPlaced((n) => handlersRef.current.onBidPlaced?.(n));
    const offStatus = auctionHub.onStatusChanged((n) => handlersRef.current.onStatusChanged?.(n));

    return () => {
      offBid();
      offStatus();
    };
  }, [handlersRef]);

  useEffect(() => {
    let disposed = false;
    let unsubscribe: (() => void) | undefined;

    auctionHub.subscribeToLobby().then((fn) => {
      if (disposed) fn();
      else unsubscribe = fn;
    });

    return () => {
      disposed = true;
      unsubscribe?.();
    };
  }, []);
}
