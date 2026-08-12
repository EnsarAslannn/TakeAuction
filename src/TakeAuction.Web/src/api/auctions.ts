import { http } from "./client";
import type {
  AuctionDetail,
  AuctionListItem,
  AuctionStatus,
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

export async function placeBid(auctionId: string, amount: number): Promise<PlaceBidResponse> {
  const { data } = await http.post<PlaceBidResponse>(`/auctions/${auctionId}/bids`, { amount });
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

  // Content-Type is left unset on purpose: the browser has to add the multipart boundary.
  const { data } = await http.post<UploadImageResponse>("/media/images", body);

  return data;
}
