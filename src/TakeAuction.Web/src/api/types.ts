export type AuctionStatus = "Scheduled" | "Active" | "Ended" | "Cancelled";

export type UserRole = "Bidder" | "Seller" | "Admin";

export interface AuctionListItem {
  id: string;
  title: string;
  imageUrl: string | null;
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
  imageUrl: string | null;
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
  maxAmount: number;
  minimumNextBid: number;
  bidCount: number;
  isLeading: boolean;
  answeredByProxy: boolean;
  placedAtUtc: string;
  endsAtUtc: string;
  auctionExtended: boolean;
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
  imageUrl: string | null;
}

export interface CancelAuctionResponse {
  id: string;
  status: AuctionStatus;
  cancelledAtUtc: string;
}

export interface UploadImageResponse {
  url: string;
  sizeInBytes: number;
}

export interface BidPlacedNotification {
  auctionId: string;
  bidId: string;
  bidderId: string;
  amount: number;
  previousPrice: number;
  automatic: boolean;
  outbidBidderId: string | null;
  endsAtUtc: string;
  auctionExtended: boolean;
  occurredAtUtc: string;
}

export interface OutbidNotification {
  auctionId: string;
  auctionTitle: string;
  currentPrice: number;
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

export interface AuctionBidItem {
  id: string;
  amount: number;
  isAutomatic: boolean;
  placedAtUtc: string;
  bidderId: string;
}
