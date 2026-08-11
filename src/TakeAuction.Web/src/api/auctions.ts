import { http } from "./client";
import type {
  AuctionDetail,
  AuctionListItem,
  AuctionStatus,
  CreateAuctionResponse,
  PagedResult,
  PlaceBidResponse,
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
}

export async function createAuction(payload: CreateAuctionPayload): Promise<CreateAuctionResponse> {
  const { data } = await http.post<CreateAuctionResponse>("/auctions", payload);
  return data;
}
