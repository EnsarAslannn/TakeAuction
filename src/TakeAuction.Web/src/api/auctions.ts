import { http } from "./client";
import type {
  AuctionBidItem,
  AuctionDetail,
  AuctionListItem,
  AuctionStatus,
  CancelAuctionResponse,
  CreateAuctionResponse,
  PagedResult,
  PlaceBidResponse,
  UploadImageResponse,
} from "./types";

export interface GetAuctionsParams {
  page?: number;
  pageSize?: number;
  status?: AuctionStatus;
  sellerId?: string;
  search?: string;
}

export async function getAuctions(params: GetAuctionsParams = {}): Promise<PagedResult<AuctionListItem>> {
  const { data } = await http.get<PagedResult<AuctionListItem>>("/auctions", {
    params: {
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
      status: params.status,
      sellerId: params.sellerId,
      search: params.search || undefined,
    },
  });
  return data;
}

export async function getAuctionById(id: string): Promise<AuctionDetail> {
  const { data } = await http.get<AuctionDetail>(`/auctions/${id}`);
  return data;
}

export async function placeBid(
  auctionId: string,
  amount: number,
  idempotencyKey: string
): Promise<PlaceBidResponse> {
  const { data } = await http.post<PlaceBidResponse>(
    `/auctions/${auctionId}/bids`,
    { amount },
    { headers: { "Idempotency-Key": idempotencyKey } }
  );
  return data;
}

export async function cancelAuction(auctionId: string): Promise<CancelAuctionResponse> {
  const { data } = await http.post<CancelAuctionResponse>(`/auctions/${auctionId}/cancel`);
  return data;
}

export interface CreateAuctionPayload {
  title: string;
  description: string;
  startingPrice: number;
  minimumBidIncrement: number;
  startsAtUtc: string;
  endsAtUtc: string;
  imageUrl?: string | null;
}

export async function createAuction(payload: CreateAuctionPayload): Promise<CreateAuctionResponse> {
  const { data } = await http.post<CreateAuctionResponse>("/auctions", payload);
  return data;
}

export const ACCEPTED_IMAGE_TYPES = ["image/jpeg", "image/png", "image/webp", "image/avif"];
export const MAX_IMAGE_SIZE_BYTES = 5 * 1024 * 1024;

export async function uploadAuctionImage(file: File): Promise<UploadImageResponse> {
  const body = new FormData();
  body.append("file", file);

  const { data } = await http.post<UploadImageResponse>("/media/images", body);

  return data;
}

export async function getAuctionBids(
  auctionId: string,
  params: { page?: number; pageSize?: number } = {}
): Promise<PagedResult<AuctionBidItem>> {
  const { data } = await http.get<PagedResult<AuctionBidItem>>(`/auctions/${auctionId}/bids`, {
    params: { page: params.page ?? 1, pageSize: params.pageSize ?? 20 },
  });
  return data;
}
