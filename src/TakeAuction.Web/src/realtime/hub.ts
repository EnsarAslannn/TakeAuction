import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from "@microsoft/signalr";
import { HUB_URL } from "@/api/client";
import type {
  AuctionStatusChangedNotification,
  BidPlacedNotification,
  OutbidNotification,
} from "@/api/types";

export type ConnectionState = "disconnected" | "connecting" | "connected" | "reconnecting";

const CSRF_COOKIE = "takeauction_csrf";
const CSRF_HEADER = "X-CSRF-TOKEN";

function readCookie(name: string): string | null {
  const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : null;
}

const isConnected = (connection: HubConnection): boolean =>
  connection.state === HubConnectionState.Connected;

type BidHandler = (notification: BidPlacedNotification) => void;
type StatusHandler = (notification: AuctionStatusChangedNotification) => void;
type OutbidHandler = (notification: OutbidNotification) => void;
type StateHandler = (state: ConnectionState) => void;

class AuctionHubClient {
  private connection: HubConnection | null = null;
  private starting: Promise<void> | null = null;

  private readonly bidHandlers = new Set<BidHandler>();
  private readonly statusHandlers = new Set<StatusHandler>();
  private readonly outbidHandlers = new Set<OutbidHandler>();
  private readonly stateHandlers = new Set<StateHandler>();

  private readonly auctionSubscriptions = new Map<string, number>();
  private lobbySubscribers = 0;
  private holders = 0;

  private state: ConnectionState = "disconnected";

  private emitState(state: ConnectionState) {
    this.state = state;
    this.stateHandlers.forEach((handler) => handler(state));
  }

  private build(): HubConnection {
    // The negotiate step is a POST, so it hits the API's CSRF double-submit check
    // as soon as an access-token cookie exists. The WebSocket upgrade itself is a
    // GET and is exempt, but browsers cannot set headers on it anyway.
    const csrf = readCookie(CSRF_COOKIE);

    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL, {
        withCredentials: true,
        headers: csrf ? { [CSRF_HEADER]: csrf } : {},
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("BidPlaced", (notification: BidPlacedNotification) =>
      this.bidHandlers.forEach((handler) => handler(notification))
    );

    connection.on("AuctionStatusChanged", (notification: AuctionStatusChangedNotification) =>
      this.statusHandlers.forEach((handler) => handler(notification))
    );

    // Addressed to this bidder rather than to a group they joined, so there is nothing to
    // subscribe to: the server sends it because of who the connection belongs to.
    connection.on("Outbid", (notification: OutbidNotification) =>
      this.outbidHandlers.forEach((handler) => handler(notification))
    );

    connection.onreconnecting(() => this.emitState("reconnecting"));
    connection.onclose(() => this.emitState("disconnected"));
    connection.onreconnected(async () => {
      this.emitState("connected");
      await this.restoreSubscriptions();
    });

    return connection;
  }

  private async restoreSubscriptions() {
    if (!this.connection) return;

    if (this.lobbySubscribers > 0) {
      await this.connection.invoke("SubscribeToLobby").catch(() => undefined);
    }

    for (const auctionId of this.auctionSubscriptions.keys()) {
      await this.connection.invoke("SubscribeToAuction", auctionId).catch(() => undefined);
    }
  }

  private async ensureStarted(): Promise<HubConnection | null> {
    if (!this.connection) {
      this.connection = this.build();
    }

    const connection = this.connection;

    if (isConnected(connection)) {
      return connection;
    }

    if (!this.starting) {
      this.emitState("connecting");
      this.starting = connection
        .start()
        .then(() => this.emitState("connected"))
        .catch(() => this.emitState("disconnected"))
        .finally(() => {
          this.starting = null;
        });
    }

    await this.starting;

    return isConnected(connection) ? connection : null;
  }

  onBidPlaced(handler: BidHandler): () => void {
    this.bidHandlers.add(handler);
    return () => {
      this.bidHandlers.delete(handler);
    };
  }

  onStatusChanged(handler: StatusHandler): () => void {
    this.statusHandlers.add(handler);
    return () => {
      this.statusHandlers.delete(handler);
    };
  }

  onOutbid(handler: OutbidHandler): () => void {
    this.outbidHandlers.add(handler);
    return () => {
      this.outbidHandlers.delete(handler);
    };
  }

  /**
   * Keeps the connection open without joining anything. A bidder browsing elsewhere in the
   * salon has no lot subscribed, and the message telling them they were outbid is exactly the
   * one they need while they are not looking at it.
   */
  async hold(): Promise<() => void> {
    this.holders += 1;
    await this.ensureStarted();

    return () => {
      this.holders = Math.max(0, this.holders - 1);
    };
  }

  onStateChange(handler: StateHandler): () => void {
    this.stateHandlers.add(handler);

    // The connection outlives any one component, so a subscriber that arrives after it opened
    // would otherwise sit on "disconnected" until something happened to the socket — and on a
    // healthy connection nothing does. Hand it the state as it stands.
    handler(this.state);

    return () => {
      this.stateHandlers.delete(handler);
    };
  }

  async subscribeToAuction(auctionId: string): Promise<() => void> {
    const previous = this.auctionSubscriptions.get(auctionId) ?? 0;
    this.auctionSubscriptions.set(auctionId, previous + 1);

    const connection = await this.ensureStarted();
    if (connection && previous === 0) {
      await connection.invoke("SubscribeToAuction", auctionId).catch(() => undefined);
    }

    return () => {
      const count = (this.auctionSubscriptions.get(auctionId) ?? 1) - 1;

      if (count <= 0) {
        this.auctionSubscriptions.delete(auctionId);
        this.connection?.invoke("UnsubscribeFromAuction", auctionId).catch(() => undefined);
      } else {
        this.auctionSubscriptions.set(auctionId, count);
      }
    };
  }

  /**
   * Tears the connection down so the next subscribe rebuilds it. Call after login or
   * logout: the CSRF header is baked in at build time and would otherwise go stale.
   */
  async reset(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this.starting = null;

    if (connection) {
      await connection.stop().catch(() => undefined);
    }

    this.emitState("disconnected");

    if (this.holders > 0 || this.lobbySubscribers > 0 || this.auctionSubscriptions.size > 0) {
      const restored = await this.ensureStarted();
      if (restored) await this.restoreSubscriptions();
    }
  }

  async subscribeToLobby(): Promise<() => void> {
    const previous = this.lobbySubscribers;
    this.lobbySubscribers += 1;

    const connection = await this.ensureStarted();
    if (connection && previous === 0) {
      await connection.invoke("SubscribeToLobby").catch(() => undefined);
    }

    return () => {
      this.lobbySubscribers = Math.max(0, this.lobbySubscribers - 1);

      if (this.lobbySubscribers === 0) {
        this.connection?.invoke("UnsubscribeFromLobby").catch(() => undefined);
      }
    };
  }
}

export const auctionHub = new AuctionHubClient();
