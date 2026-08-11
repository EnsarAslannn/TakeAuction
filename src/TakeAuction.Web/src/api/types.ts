export type AuctionStatus = "Scheduled" | "Active" | "Ended" | "Cancelled";

export type UserRole = "Bidder" | "Seller" | "Admin";

export interface AuctionListItem {
  id: string;
  title: string;
  startingPrice: number;
  currentPrice: number;
  status: AuctionStatus;
  startsAtUtc: string;
  endsAtUtc: string;
  sellerId: string;
}

export interface AuctionDetail {
  id: string;
  title: string;
  description: string;
  startingPrice: number;
  currentPrice: number;
  minimumBidIncrement: number;
  minimumAcceptableBid: number;
  bidCount: number;
  status: AuctionStatus;
  startsAtUtc: string;
  endsAtUtc: string;
  createdAtUtc: string;
  sellerId: string;
  sellerDisplayName: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
}

export interface PlaceBidResponse {
  bidId: string;
  auctionId: string;
  amount: number;
  currentPrice: number;
  minimumNextBid: number;
  bidCount: number;
  placedAtUtc: string;
}

export interface AuthenticatedUser {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
  expiresAtUtc: string;
}

export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
  createdAtUtc: string;
  lastLoginAtUtc: string | null;
}

export interface CreateAuctionResponse {
  id: string;
  status: AuctionStatus;
  startsAtUtc: string;
  endsAtUtc: string;
  createdAtUtc: string;
}

export interface BidPlacedNotification {
  auctionId: string;
  bidId: string;
  bidderId: string;
  amount: number;
  previousPrice: number;
  outbidBidderId: string | null;
  endsAtUtc: string;
  occurredAtUtc: string;
}

export interface AuctionStatusChangedNotification {
  auctionId: string;
  status: AuctionStatus;
  currentPrice: number;
  leadingBidderId: string | null;
  endsAtUtc: string;
  occurredAtUtc: string;
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  errors?: Record<string, string[]>;
}
